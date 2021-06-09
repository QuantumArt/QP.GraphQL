using System.Data.Common;

namespace QP.GraphQL.Interfaces.DAL
{
    public interface IConnectionFactory
    {
        DbConnection GetConnection();
        DatabaseType Type { get; }
    }
}
