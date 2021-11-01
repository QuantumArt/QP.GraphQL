using System;
using System.Collections.Generic;
using System.Text;

namespace QP.GraphQL.Interfaces.Metadata
{
    public class QpPluginSiteMetadata
    {
        public string ApiKey { get; set; }
        public int? MaxDepth { get; set; }
        public int? MaxComplexity { get; set; }
        public double? FieldImpact { get; set; }
        public int MaxRecursionCount { get; set; }
    }
}