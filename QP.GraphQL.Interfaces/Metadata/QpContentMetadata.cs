using System.Collections.Generic;

namespace QP.GraphQL.Interfaces.Metadata
{
    public class QpContentMetadata
    {
        public int Id { get; set; }
        public string FriendlyName { get; set; }
        public string AliasSingular { get; set; }
        public string AliasPlural { get; set; }
        public string Description { get; set; }
        public IList<QpContentAttributeMetadata> Attributes { get; set; }
    }
}
