using GraphQL;
using GraphQL.Conversion;
using GraphQL.Instrumentation;
using GraphQL.Introspection;
using GraphQL.Types;
using GraphQL.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QP.GraphQL.App.Schema
{
    public class SchemaDecorator : ISchema, IDisposable
    {
        public readonly ISchema _schema;
        public readonly ISchemaFactory _factory;

        public SchemaDecorator(ISchemaFactory factory)
        {
            _factory = factory;
            _schema = _factory.GetSchema();
        }

        public ExperimentalFeatures Features { get => _schema.Features; set => _schema.Features = value; }

        public bool Initialized => _schema.Initialized;

        public INameConverter NameConverter => _schema.NameConverter;

        public IFieldMiddlewareBuilder FieldMiddleware => _schema.FieldMiddleware;

        public IObjectGraphType Query { get => _schema.Query; set => _schema.Query = value; }
        public IObjectGraphType Mutation { get => _schema.Mutation; set => _schema.Mutation = value; }
        public IObjectGraphType Subscription { get => _schema.Subscription; set => _schema.Subscription = value; }

        public SchemaDirectives Directives => _schema.Directives;

        public SchemaTypes AllTypes => _schema.AllTypes;

        public IEnumerable<Type> AdditionalTypes => _schema.AdditionalTypes;

        public IEnumerable<IGraphType> AdditionalTypeInstances => _schema.AdditionalTypeInstances;

        public IEnumerable<(Type clrType, Type graphType)> TypeMappings => _schema.TypeMappings;

        public IEnumerable<(Type clrType, Type graphType)> BuiltInTypeMappings => _schema.BuiltInTypeMappings;

        public ISchemaFilter Filter { get => _schema.Filter; set => _schema.Filter = value; }
        public ISchemaComparer Comparer { get => _schema.Comparer; set => _schema.Comparer = value; }

        public FieldType SchemaMetaFieldType => _schema.SchemaMetaFieldType;

        public FieldType TypeMetaFieldType => _schema.TypeMetaFieldType;

        public FieldType TypeNameMetaFieldType => _schema.TypeNameMetaFieldType;

        public Dictionary<string, object> Metadata => _schema.Metadata;

        public string Description { get => _schema.Description; set => _schema.Description = value; }

        public TType GetMetadata<TType>(string key, TType defaultValue = default)
        {
            return _schema.GetMetadata(key, defaultValue);
        }

        public TType GetMetadata<TType>(string key, Func<TType> defaultValueFactory)
        {
            return _schema.GetMetadata(key, defaultValueFactory);
        }

        public bool HasMetadata(string key)
        {
            return _schema.HasMetadata(key);
        }

        public void Initialize()
        {
            _schema.Initialize();
        }

        public void RegisterType(IGraphType type)
        {
            _schema.RegisterType(type);
        }

        public void RegisterType(Type type)
        {
            _schema.RegisterType(type);
        }

        public void RegisterTypeMapping(Type clrType, Type graphType)
        {
            _schema.RegisterTypeMapping(clrType, graphType);
        }

        public void RegisterVisitor(ISchemaNodeVisitor visitor)
        {
            _schema.RegisterVisitor(visitor);
        }

        public void RegisterVisitor(Type type)
        {
            _schema.RegisterVisitor(type);
        }

        public void Dispose()
        {
            _factory.OnDispose(_schema);
        }
    }
}
