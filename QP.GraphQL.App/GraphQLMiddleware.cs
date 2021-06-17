using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Instrumentation;
using GraphQL.SystemTextJson;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using QP.GraphQL.App.Schema;
using QP.GraphQL.DAL;
using QP.GraphQL.Interfaces.Articles;

namespace QP.GraphQL.App
{
    public class GraphQLMiddleware
    {
        public const string QpArticleStateField = "state";
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
            if (!IsGraphQLRequest(context))
            {
                await _next(context);
                return;
            }

            await ExecuteAsync(context, schema);
        }

        private bool IsGraphQLRequest(HttpContext context)
        {
            return context.Request.Path.StartsWithSegments(_settings.GraphQLPath)
                && string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase);
        }

        private IDictionary<string, object> GetUserContext(HttpContext context)
        {
            var path = context.Request.Path;
            path.StartsWithSegments(_settings.GraphQLPath, out PathString s);
            var state = s.StartsWithSegments("/stage") ? QpArticleState.Stage : QpArticleState.Live;

            return new Dictionary<string, object>
            {
                { QpArticleStateField, state }
            };
        }

        private async Task ExecuteAsync(HttpContext context, ISchema schema)
        {
            var start = DateTime.UtcNow;
            var request = await context.Request.Body.FromJsonAsync<GraphQLRequest>(context.RequestAborted);
            var userContext = GetUserContext(context);

            var result = await _executer.ExecuteAsync(options =>
            {
                options.Schema = schema;
                options.Query = request.Query;
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

            await WriteResponseAsync(context, result, context.RequestAborted);
        }

        private async Task WriteResponseAsync(HttpContext context, ExecutionResult result, CancellationToken cancellationToken)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200; // OK

            await _writer.WriteAsync(context.Response.Body, result, cancellationToken);
        }
    }
}
