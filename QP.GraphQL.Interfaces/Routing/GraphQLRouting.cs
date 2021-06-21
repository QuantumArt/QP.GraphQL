using QP.GraphQL.Interfaces.Articles;
using System.Collections.Generic;

namespace QP.GraphQL.Interfaces.Routing
{
    public class GraphQLRouting
    {
        public string BasePath {get; set;}
        public Dictionary<QpArticleState, StateSettings> States { get; set; }
    }
}
