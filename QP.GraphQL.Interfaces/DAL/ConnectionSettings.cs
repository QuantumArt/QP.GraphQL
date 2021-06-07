using System;
using System.Data.Common;

namespace QP.GraphQL.Interfaces.DAL
{
    public class ConnectionSettings
    {
        public string QPConnectionString { get; set; }
        public DatabaseType Type { get; set; }
    }
}
