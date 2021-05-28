using System.Collections.Generic;

namespace QP.GraphQL.Interfaces.Articles
{
    public class QpArticle
    {
        public QpArticle(int contentId)
        {
            ContentId = contentId;
            AllFields = new Dictionary<string, object>();
        }
        public int ContentId { get; private set; }
        public int Id { get; set; }
        public Dictionary<string, object> AllFields { get; set; }
    }
}
