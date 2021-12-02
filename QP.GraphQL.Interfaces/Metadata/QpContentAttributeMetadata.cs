namespace QP.GraphQL.Interfaces.Metadata
{
    public class QpContentAttributeMetadata
    {
        public int Id { get; set; }
        public int ContentId { get; set; }
        public string FriendlyName { get; set; }
        public string Alias { get; set; }
        public string SchemaAlias { get; set; }
        public string TypeName { get; set; }
        public bool Indexed { get; set; }
        public bool Required { get; set; }

        public int? RelatedO2mContentId { get; set; }
        public int? RelatedM2mContentId { get; set; }
        public int? M2mRelationId { get; set; }
        public bool? M2mIsBackward { get; set; }
        public int? RelatedM2oContentId { get; set; }
        public string RelatedM2oBackwardField { get; set; }
        public int? ClassifierAttributeId { get; set; }
        public bool IsClassifier { get; set; }
        public string SubFolder { get; set; }
        public bool UseSiteLibrary { get; set; }
        public int? SourceAttributeId { get; set; }

        public QpContentMetadata Content { get; set; }
    }
}
