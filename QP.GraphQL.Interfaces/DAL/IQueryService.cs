using System.Collections.Generic;
using System.Data.Common;

namespace QP.GraphQL.Interfaces.DAL
{
    public interface IQueryService
    {
        DbParameter GetIdParam(string name, IEnumerable<int> ids);
        string GetIdTable(string name, string alias = "i");
    }
}
