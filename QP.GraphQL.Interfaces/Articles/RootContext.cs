namespace QP.GraphQL.Interfaces.Articles
{
    public class RootContext : ContentContext
    {
        public ExtensionContext[] Extensions { get; set; }
        public FieldContext Classifier { get; set; }

    }
}
