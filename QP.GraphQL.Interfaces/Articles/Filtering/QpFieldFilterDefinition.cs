namespace QP.GraphQL.Interfaces.Articles.Filtering
{
    public class QpFieldFilterDefinition
    {
        public string QpFieldName { get; set; }
        public string QpFieldType { get; set; }
        public FilterOperator Operator { get; set; }
    }
}
