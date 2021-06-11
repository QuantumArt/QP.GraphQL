using Microsoft.Extensions.Configuration;
using QP.GraphQL.App.Schema;
using QP.GraphQL.Interfaces.DAL;

namespace QP.GraphQL.App
{
    public static class ServicesExstension
    {
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
    }
}
