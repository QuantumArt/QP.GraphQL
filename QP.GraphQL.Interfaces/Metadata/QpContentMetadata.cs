using System.Collections.Generic;
using System.Linq;

namespace QP.GraphQL.Interfaces.Metadata
{
    public class QpContentMetadata
    {
        public int Id { get; set; }
        public QpSiteMetadata Site { get; set; }
        public string FriendlyName { get; set; }
        public string AliasSingular { get; set; }
        public string AliasPlural { get; set; }
        public string Description { get; set; }
        public IList<QpContentAttributeMetadata> Attributes { get; set; }
        public bool HasExtensions => Attributes.Any(a => a.IsClassifier);
        public bool IsExtension => Attributes.Any(a => a.ClassifierAttributeId.HasValue);
        public IList<QpContentMetadata> Extensions { get; set; }
        public Dictionary<int, string> ExtensionMap =>
            Extensions.ToDictionary(
                e => e.Id,
                e => e.Attributes.First(a => a.ClassifierAttributeId.HasValue).Alias);
    }
}
