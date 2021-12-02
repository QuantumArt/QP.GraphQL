using Microsoft.Data.SqlClient;
using QP.GraphQL.Interfaces.DAL;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace QP.GraphQL.DAL
{
    public class QueryServiceSqlServer : QueryServiceBase, IQueryService
    {
        private const string IdsTypeName = "Ids";

        public override DbParameter GetIdParam(string name, IEnumerable<int> ids)
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

        public override string GetIdTable(string name, string alias = "i")
        {
            return $"{name} {alias}";
        }

        public override DbParameter GetParameter(string name, SqlDbType type, object value)
        {
            return new SqlParameter(GetParamName(name), type)
            {
                Value = value
            };
        }

        public string AsBool(bool value)
        {
            return value ? "1" : "0";
        }

    }
}