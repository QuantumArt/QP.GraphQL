namespace QP.GraphQL.Interfaces.Articles
{
    public class FieldContext
    {
        public int ContentId { get; set; }
        public string QueryAlias => $"cid_{ContentId}_{Alias}";
        public string Alias { get; set; }
        public FieldContext BackwardField { get; set; }
        public bool? M2mIsBackward { get; set; }
        public int? M2mRelationId { get; set; }
        public int? RelatedO2mContentId { get; set; }
    }
}
