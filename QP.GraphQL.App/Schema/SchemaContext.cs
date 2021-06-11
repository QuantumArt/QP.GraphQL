using System;

namespace QP.GraphQL.App.Schema
{
    public class SchemaContext
    {
        public DateTime LoadDate { get; set; }
        public SchemaState State { get; set; }
        public SchemaSettings Settings { get; set; }
    }
}
