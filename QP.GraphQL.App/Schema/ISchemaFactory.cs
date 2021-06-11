using GraphQL.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QP.GraphQL.App.Schema
{
    public interface ISchemaFactory
    {
        ISchema GetSchema();
        void OnDispose(ISchema schema);
        SchemaContext ReloadSchema();
        SchemaContext Context { get; }
    }
}
