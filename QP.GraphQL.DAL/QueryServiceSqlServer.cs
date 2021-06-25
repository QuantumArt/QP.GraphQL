using Microsoft.Data.SqlClient;
using QP.GraphQL.Interfaces.DAL;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace QP.GraphQL.DAL
{
    public class QueryServiceSqlServer : IQueryService
    {
        private const string IdsTypeName = "Ids";

        public DbParameter GetIdParam(string name, IEnumerable<int> ids)
        {
            return new SqlParameter(name, SqlDbType.Structured)
            {
                TypeName = IdsTypeName,
                Value = IdsToDataTable(ids)
            };
        }

        private static DataTable IdsToDataTable(IEnumerable<int> ids)
        {
            var dt = new DataTable();
            dt.Columns.Add("id");

            if (ids != null)
            {
                foreach (var id in ids)
                {
                    dt.Rows.Add(id);
                }
            }
           
            return dt;
        }

        public string GetIdTable(string name, string alias = "i")
        {
            return $"{name} {alias}";
        }
    }
}
