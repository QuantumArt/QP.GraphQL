namespace QP.GraphQL.Interfaces.Articles.Filtering
{
    public class QpFieldRelationFilterClause : QpFieldFilterClause
    {
        public string BackwardTable { get; set; }
        public string LinkTable { get; set; }
        public string BackwardField { get; set; }
        public string TableAlias { get; set; }
    }

}
