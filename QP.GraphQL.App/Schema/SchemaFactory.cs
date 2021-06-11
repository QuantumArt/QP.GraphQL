using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace QP.GraphQL.App.Schema
{
    public class SchemaFactory : ISchemaFactory, IDisposable
    {        
        private readonly IServiceProvider _serviceProvider;
        private QpContentsSchemaDynamic _schema;
        private IServiceScope _scope;
        private readonly ConcurrentDictionary<Guid, ScopeContext> _scopeMap;

        public SchemaContext Context { get; private set; }
        private object _locker = new object();

        public SchemaFactory(IServiceProvider serviceProvider, IOptions<SchemaSettings> options)
        {
            _serviceProvider = serviceProvider;
            _scopeMap = new ConcurrentDictionary<Guid, ScopeContext>();
            Context = new SchemaContext
            {
                State = SchemaState.None,
                LoadDate = DateTime.Now,
                Settings = options.Value
            };
            ReloadSchema();
        }

        public ISchema GetSchema()
        {
            if (_scopeMap.TryGetValue(_schema.Id, out var context))
            {
                context.OnCreate();
            }
            return _schema;
        }


        public SchemaContext ReloadSchema()
        {
            lock (_locker)
            {
                Context.State = _schema == null ? SchemaState.Loading : SchemaState.Reloading;
                
                _scope = _serviceProvider.CreateScope();
                _schema = _scope.ServiceProvider.GetRequiredService<QpContentsSchemaDynamic>();
                _scopeMap.TryAdd(_schema.Id, new ScopeContext(_schema.Id, _scope));

                var items = _scopeMap.Where(item => item.Value.Id != _schema.Id && item.Value.CanDisposeScope).ToArray();

                foreach(var item in items)
                {
                    item.Value.ServiceScope.Dispose();
                    _scopeMap.TryRemove(item);
                }

                Context.LoadDate = DateTime.Now;
                Context.State = SchemaState.Active;

                return Context;
            }
        }

        public void OnDispose(ISchema schema)
        {
            var id = ((QpContentsSchemaDynamic)schema).Id;
            if (_scopeMap.TryGetValue(id, out var context))
            {
                context.OnDispose();
            }
        }

        public void Dispose()
        {
            foreach (var item in _scopeMap)
            {
                item.Value.ServiceScope.Dispose();
                _scopeMap.TryRemove(item);
            }
        }
    }
}
