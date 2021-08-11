using Npgsql;
using NpgsqlTypes;
using QP.GraphQL.Interfaces.DAL;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace QP.GraphQL.DAL
{
    public class QueryServicePostgres : QueryServiceBase, IQueryService
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

        public override DbParameter GetParameter(string name, SqlDbType type, object value)
        {
            return new NpgsqlParameter(GetParamName(name), type)
            {
                Value = value
            };
        }
    }
}
