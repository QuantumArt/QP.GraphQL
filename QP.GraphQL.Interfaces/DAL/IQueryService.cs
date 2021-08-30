using QP.GraphQL.Interfaces.Articles.Filtering;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace QP.GraphQL.Interfaces.DAL
{
    public interface IQueryService
    {
        DbParameter GetIdParam(string name, IEnumerable<int> ids);
        DbParameter GetParameter(string name, SqlDbType type, object value);
        string GetIdTable(string name, string alias = "i");
        IQueryContext GetQueryContext(QpFieldFilterClause clause);
        string AsBool(bool value);
    }
}
