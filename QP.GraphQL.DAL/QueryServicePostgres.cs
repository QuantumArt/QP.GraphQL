using Npgsql;
using NpgsqlTypes;
using QP.GraphQL.Interfaces.DAL;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace QP.GraphQL.DAL
{
    public class QueryServicePostgres : IQueryService
    {
        public DbParameter GetIdParam(string name, IEnumerable<int> ids)
        {
            return new NpgsqlParameter(name, NpgsqlDbType.Array | NpgsqlDbType.Integer)
            {
                Value = ids?.ToArray() ?? new int[0]
            };
        }

        public string GetIdTable(string name, string alias = "i")
        {
            return $"unnest({name}) {alias}(id)";
        }
    }
}
