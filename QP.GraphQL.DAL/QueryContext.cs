using QP.GraphQL.Interfaces.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace QP.GraphQL.DAL
{
    public class QueryContext : IQueryContext
    {
        public static implicit operator string(QueryContext c) => c.Query;
        public static QueryContext EmptyWhere => new QueryContext("1=1");

        public QueryContext(string query, DbParameter param)
        {
            Query = query;
            Parameters = new List<DbParameter> { param };
        }

        public QueryContext(string query, params IQueryContext[] nested)
        {
            Query = query;
            Parameters = nested == null ? new List<DbParameter>() : nested.SelectMany(x => x.Parameters)
                .GroupBy(p => p.ParameterName)
                .Select(g => g.First())
                .ToList();
        }

        public string Query { get; set; }
        public List<DbParameter> Parameters { get; set; }

        public override string ToString() => Query;

        public DbCommand GetCommand(DbConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = Query;
            var commandParameters = Parameters.Cast<ICloneable>()
                .Select(x => x.Clone())
                .ToArray();
            command.Parameters.AddRange(commandParameters);
            return command;
        }
    }
}
