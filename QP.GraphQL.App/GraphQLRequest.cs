using GraphQL;

namespace QP.GraphQL.App
{
    public class GraphQLRequest
    {
        public string OperationName { get; set; }

        public string Query { get; set; }

        public Inputs Variables { get; set; }
    }
}
