using System;

namespace QP.GraphQL.App
{
    public class GraphQLSettings
    {
        public bool EnableMetrics { get; set; }
        public bool ExposeExceptions { get; set; }
        public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromMinutes(5);
        public bool EnableGraphqlUI { get; set; }
    }
}
