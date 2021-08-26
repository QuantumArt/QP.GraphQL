namespace QP.GraphQL.Interfaces.Articles
{
    public class ContentContext
    {
        public int ContentId { get; set; }
        public string TableALias => $"cid_{ContentId}";
        public FieldContext[] Fields { get; set; }
    }
}
