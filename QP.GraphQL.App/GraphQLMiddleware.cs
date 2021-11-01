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
using System.Threading;
using System.Threading.Tasks;

namespace QP.GraphQL.App
{
    public class GraphQLMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly GraphQLSettings _settings;
        private readonly IDocumentExecuter _executer;
        private readonly IDocumentWriter _writer;
        private readonly DataLoaderDocumentListener _dataLoaderDocumentListener;

        public GraphQLMiddleware(
            RequestDelegate next,
            IOptions<GraphQLSettings> options,
            IDocumentExecuter executer,
            IDocumentWriter writer,
            DataLoaderDocumentListener dataLoaderDocumentListener)
        {
            _next = next;
            _settings = options.Value;
            _executer = executer;
            _writer = writer;
            _dataLoaderDocumentListener = dataLoaderDocumentListener;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "ASP.NET Core convention")]
        public async Task Invoke(HttpContext context, ISchema schema)
        {
            ComplexityConfiguration complexityConfiguration = null;
            var start = DateTime.UtcNow;
            var request = await context.Request.Body.FromJsonAsync<GraphQLRequest>(context.RequestAborted);
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

                var result = await _executer.ExecuteAsync(options =>
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

                if (_settings.EnableMetrics)
                {
                    result.EnrichWithApolloTracing(start);
                }

                await WriteResponseAsync(context, result, context.RequestAborted, StatusCodes.Status200OK);
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
    }
}
