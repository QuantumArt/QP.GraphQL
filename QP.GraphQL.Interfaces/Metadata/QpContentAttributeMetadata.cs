namespace QP.GraphQL.Interfaces.Metadata
{
    public class QpContentAttributeMetadata
    {
        public int Id { get; set; }
        public int ContentId { get; set; }
        public string FriendlyName { get; set; }
        public string Alias { get; set; }
        public string TypeName { get; set; }
        public bool Indexed { get; set; }

        public int? RelatedO2mContentId { get; set; }
        public int? RelatedM2mContentId { get; set; }
        public int? M2mRelationId { get; set; }
        public int? RelatedM2oContentId { get; set; }
        public string RelatedM2oBackwardField { get; set; }
    }
}
