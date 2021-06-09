using Microsoft.Extensions.Options;
using Npgsql;
using QP.GraphQL.Interfaces.DAL;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace QP.GraphQL.DAL
{
    public class ConnectionFactory : IConnectionFactory
    {
        private readonly ConnectionSettings _settings;

        public ConnectionFactory(ConnectionSettings settings)
        {
            _settings = settings;
        }

        public ConnectionFactory(IOptions<ConnectionSettings> options)
        {
            _settings = options.Value;
        }

        public DatabaseType Type => _settings.Type;

        public DbConnection GetConnection()
        {
            return _settings.Type switch
            {
                DatabaseType.SqlServer => new SqlConnection(_settings.QPConnectionString),
                DatabaseType.Postgres => new NpgsqlConnection(_settings.QPConnectionString),
                _ => null
            };
        }
    }
}
