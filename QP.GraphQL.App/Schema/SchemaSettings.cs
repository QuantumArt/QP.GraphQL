using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QP.GraphQL.App.Schema
{
    public class SchemaSettings
    {
        public bool SchemaAutoReload { get; set; }
        public TimeSpan SchemaReloadInterval { get; set; }
    }
}
