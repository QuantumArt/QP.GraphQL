namespace QP.GraphQL.App.QpContract
{
    public class QpPluginField
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public QpPluginFieldValueType ValueType { get; set; }
        public QpPluginFieldRelationType RelationType { get; set; }
        public int SortOrder { get; set; }
        public bool Required { get; set; }
    }
}
