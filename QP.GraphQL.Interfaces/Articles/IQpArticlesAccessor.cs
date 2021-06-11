using QP.GraphQL.Interfaces.Articles.Filtering;
using QP.GraphQL.Interfaces.Articles.Paging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QP.GraphQL.Interfaces.Articles
{
    public interface IQpArticlesAccessor
    {
        Task<IDictionary<int, QpArticle>> GetArticlesByIdList(int contentId, IEnumerable<int> articleIds, QpArticleState state);
        Task<RelayPaginationResult> GetPagedArticles(int contentId,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            RelayPaginationArgs paginationArgs,
            bool calcTotalCount,
            QpArticleState state);
        Task<ILookup<int, QpArticle>> GetRelatedM2mArticlesByIdList(int contentId,
            IEnumerable<int> articleIds,
            int relationId,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            QpArticleState state);
    }
}
