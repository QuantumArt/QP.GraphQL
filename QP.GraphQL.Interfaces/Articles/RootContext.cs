namespace QP.GraphQL.Interfaces.Articles
{
    public class RootContext : ContentContext
    {
        public ExtensionContext[] Extensions { get; set; }
        public FieldContext Classifier { get; set; }

    }

    public class ExtensionContext : ContentContext
    {
        public string ReferenceToBase { get; set; }
    }

    public class ContentContext
    {
        public int ContentId { get; set; }
        public string TableALias => $"cid_{ContentId}";
        public FieldContext[] Fields { get; set; }
    }

    public class FieldContext
    {
        public int ContentId { get; set; }
        public string QueryAlias => $"cid_{ContentId}_{Alias}";
        public string Alias { get; set; }
    }
}
