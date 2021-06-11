using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;

namespace QP.GraphQL.App.Schema
{
    public class ScopeContext
    {

        public ScopeContext(Guid id, IServiceScope serviceScope)
        {
            Id = id;
            ServiceScope = serviceScope;
        }

        public Guid Id { get; private set; }
        public IServiceScope ServiceScope { get; private set; }
        private int _count = 0;

        public void OnCreate()
        {
            Interlocked.Increment(ref _count);
        }

        public void OnDispose()
        {
            Interlocked.Decrement(ref _count);
        }

        public bool CanDisposeScope => _count <= 0;
    }
}
