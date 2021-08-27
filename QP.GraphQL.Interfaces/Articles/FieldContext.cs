namespace QP.GraphQL.Interfaces.Articles
{
    public class FieldContext
    {
        public int ContentId { get; set; }
        public string QueryAlias => $"cid_{ContentId}_{Alias}";
        public string Alias { get; set; }
    }
}
