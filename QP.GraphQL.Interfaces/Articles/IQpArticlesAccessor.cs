using QP.GraphQL.Interfaces.Articles.Filtering;
using QP.GraphQL.Interfaces.Articles.Paging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QP.GraphQL.Interfaces.Articles
{
    public interface IQpArticlesAccessor
    {
        Task<IDictionary<int, QpArticle>> GetArticlesByIdList(int contentId, IEnumerable<int> articleIds);
        Task<RelayPaginationResult> GetPagedArticles(int contentId,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            RelayPaginationArgs paginationArgs,
            bool calcTotalCount);
        Task<ILookup<int, QpArticle>> GetRelatedM2mArticlesByIdList(int contentId,
            IEnumerable<int> articleIds,
            int relationId,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where);
    }
}
