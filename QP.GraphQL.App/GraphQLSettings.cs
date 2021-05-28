using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace QP.GraphQL.App
{
    public class GraphQLSettings
    {
        public PathString GraphQLPath { get; set; }
        public bool EnableMetrics { get; set; }
        public bool ExposeExceptions { get; set; }
    }
}
