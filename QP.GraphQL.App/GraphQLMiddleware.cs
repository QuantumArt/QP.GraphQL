using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Instrumentation;
using GraphQL.SystemTextJson;
using GraphQL.Types;
using GraphQL.Validation.Complexity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using QP.GraphQL.Interfaces.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Execution;
using QA.DotNetCore.Caching.Interfaces;

namespace QP.GraphQL.App
{
    public class GraphQLMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly GraphQLSettings _settings;
        private readonly IDocumentExecuter _executer;
        private readonly IDocumentWriter _writer;
        private readonly DataLoaderDocumentListener _dataLoaderDocumentListener;

        private readonly Regex _redundantCharactersRegex = new("\\t|\\n|\\\\r\\\\n", RegexOptions.Compiled);
        private readonly Regex _multipleSpacesRegex = new("[\\s]{2,}", RegexOptions.Compiled);

        public GraphQLMiddleware(
            RequestDelegate next,
            IOptions<GraphQLSettings> options,
            IDocumentExecuter executer,
            IDocumentWriter writer,
            DataLoaderDocumentListener dataLoaderDocumentListener
        )
        {
            _next = next;
            _settings = options.Value;
            _executer = executer;
            _writer = writer;
            _dataLoaderDocumentListener = dataLoaderDocumentListener;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ASP.NET Core convention")]
        public async Task Invoke(HttpContext context, ISchema schema, ICacheProvider cache)
        {
            ComplexityConfiguration complexityConfiguration = null;
            var start = DateTime.UtcNow;
            byte[] bodyBuffer = new byte[context.Request.ContentLength ?? throw new InvalidOperationException("Unknown body size")];
            _ = await context.Request.Body.ReadAsync(bodyBuffer, context.RequestAborted);
            string requestString = Encoding.UTF8.GetString(bodyBuffer);
            var request = requestString.FromJson<GraphQLRequest>();
            var pluginSiteMetadata = schema.GetMetadata<QpPluginSiteMetadata>("PLUGINSITEMETADATA");
            var isIntrospectionQuery =  IsIntrospectionQuery(request);

            if (isIntrospectionQuery || await ValidateApiKey(context, schema, pluginSiteMetadata))
            {
                var userContext = GetUserContext(context);

                if (!isIntrospectionQuery)
                {
                    complexityConfiguration = new ComplexityConfiguration
                    {
                        FieldImpact = pluginSiteMetadata.FieldImpact,
                        MaxComplexity = pluginSiteMetadata.MaxComplexity,
                        MaxDepth = pluginSiteMetadata.MaxDepth
                    };

                    if (pluginSiteMetadata.MaxRecursionCount > 0)
                    {
                        complexityConfiguration.MaxRecursionCount = pluginSiteMetadata.MaxRecursionCount;
                    }
                }

                string cacheKey = BuildCacheKey(requestString);
                ExecutionResult result = null;
                RootExecutionNode cachedData = await cache.GetOrAddAsync(cacheKey, TimeSpan.FromMinutes(5), GetData);

                result ??= new()
                {
                    Executed = true,
                    Data = cachedData,
                    Query = request.Query
                };

                if (_settings.EnableMetrics)
                {
                    result.EnrichWithApolloTracing(start);
                }

                await WriteResponseAsync(context, result, context.RequestAborted, StatusCodes.Status200OK);

                async Task<RootExecutionNode> GetData()
                {
                    result = await _executer.ExecuteAsync(options =>
                    {
                        options.Schema = schema;
                        options.Query = request.Query;
                        options.ComplexityConfiguration = complexityConfiguration;
                        options.OperationName = request.OperationName;
                        options.Inputs = request.Variables;
                        options.UserContext = userContext;
                        options.EnableMetrics = _settings.EnableMetrics;
                        options.RequestServices = context.RequestServices;
                        options.CancellationToken = context.RequestAborted;
                        options.Listeners.Add(_dataLoaderDocumentListener);
                    });

                    return (RootExecutionNode)result.Data;
                }
            }
        }

        private bool IsIntrospectionQuery(GraphQLRequest request)
        {
            return request.Query.TrimStart().StartsWith("query IntrospectionQuery");
        }

        private async Task<bool> ValidateApiKey(HttpContext context, ISchema schema, QpPluginSiteMetadata qpPluginSiteMetadata)
        {
            var error = schema.GetMetadata<string>("ERROR");

            if (error != null)
            {
                var result = GetErrorResult(error);
                await WriteResponseAsync(context, result, context.RequestAborted, StatusCodes.Status401Unauthorized);
                return false;
            }

            var apiKey = qpPluginSiteMetadata.ApiKey;
            context.Request.Headers.TryGetValue("APIKEY", out StringValues value);

            if (value == apiKey)
            {
                return true;
            }
            else
            {
                var result = GetErrorResult("Invalid API Key");
                await WriteResponseAsync(context, result, context.RequestAborted, StatusCodes.Status401Unauthorized);
                return false;
            }
        }

        private ExecutionResult GetErrorResult(string error)
        {
            var result = new ExecutionResult
            {
                Executed = false,
                Errors = new ExecutionErrors()
            };

            result.Errors.Add(new ExecutionError(error));
            return result;
        }

        private IDictionary<string, object> GetUserContext(HttpContext context)
        {
            return new Dictionary<string, object>
            {
                { ServicesExstension.QpArticleStateField, context.GetState() }
            };
        }

        private async Task WriteResponseAsync(HttpContext context, ExecutionResult result, CancellationToken cancellationToken, int statusCode)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            await _writer.WriteAsync(context.Response.Body, result, cancellationToken);
        }

        private string BuildCacheKey(string request)
        {
            const string replacement = " ";
            string cleaned = _redundantCharactersRegex.Replace(request, replacement);
            string unSpaced = _multipleSpacesRegex.Replace(cleaned, replacement);
            string lowered = unSpaced.ToLowerInvariant();
            return string.Join("", SHA1.HashData(Encoding.UTF8.GetBytes(lowered)).Select(x => x.ToString("x2")));
        }
    }
}
