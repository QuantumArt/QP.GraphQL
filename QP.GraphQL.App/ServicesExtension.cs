using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using QP.GraphQL.App.Schema;
using QP.GraphQL.Interfaces.Articles;
using QP.GraphQL.Interfaces.DAL;
using QP.GraphQL.Interfaces.Routing;
using System;
using System.Linq;

namespace QP.GraphQL.App
{
    public static class ServicesExstension
    {
        public const string QpArticleStateField = "state";

        public static bool SchemaAutoReload(this IConfiguration configuration)
        {
            return configuration.Get<SchemaSettings>().SchemaAutoReload;
        }

        public static DatabaseType GetDatabaseType(this IConfiguration configuration)
        {
            return configuration.GetConnectionSection().Get<ConnectionSettings>().Type;
        }

        public static IConfigurationSection GetConnectionSection(this IConfiguration configuration)
        {
            return configuration.GetSection("ConnectionStrings");
        }

        public static IEndpointConventionBuilder MapGraphQL(this IEndpointRouteBuilder endpoints, IConfiguration configuration)
        {
            var routing = configuration.GetSection("GraphQLRouting").Get<GraphQLRouting>();            
            var pipeline = endpoints.CreateApplicationBuilder().UseMiddleware<GraphQLMiddleware>().Build();

            var allowedStates = routing.States.Where(item => item.Value.Allow)
                .Select(item => item.Key.ToString().ToLowerInvariant())
                .ToArray();

            var defaultItem = routing.States.FirstOrDefault(item => item.Value.Allow && item.Value.Default);
            string defaultValue = defaultItem.Value == null ? null : $"={defaultItem.Key.ToString().ToLowerInvariant()}";
            var routePattern = $"{routing.BasePath}/{{{QpArticleStateField}:regex(^{string.Join('|', allowedStates)}$){defaultValue}}}";

            return endpoints.Map(routePattern, pipeline).WithDisplayName("GraphQL");
        }

        public static QpArticleState GetState(this HttpContext context)
        {
            if (context.GetRouteData().Values.TryGetValue(QpArticleStateField, out object value))
            {
                if (Enum.TryParse<QpArticleState>(value as string, true, out QpArticleState state))
                {
                    return state;
                }
            }

            throw new ArgumentException($"HttpContext does not contans {QpArticleStateField}");
        }
    }
}
