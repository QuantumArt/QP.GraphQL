using System.Collections.Generic;
using System.Data.Common;

namespace QP.GraphQL.Interfaces.DAL
{
    public interface IQueryContext
    {
        string Query { get; set; }
        List<DbParameter> Parameters { get; set; }
        DbCommand GetCommand(DbConnection connection);
    }
}
