using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QP.GraphQL.Interfaces.Articles;
using QP.GraphQL.Interfaces.DAL;
using System.Collections.Generic;
using System.Data.Common;

namespace QP.GraphQL.DAL
{
    public class QpArticlesAccessorSqlServer : QpArticlesAccessorBase
    {
        public QpArticlesAccessorSqlServer(DbConnection connection, IQueryService queryService, IOptions<QpArticlesAccessorSettings> options, ILogger<QpArticlesAccessorSqlServer> logger)
            : base(connection, queryService, options, logger)
        {

        }

        protected override string AddDelimiter(string identifier) => $"[{identifier}]";
 
        protected override string BuildIdsFieldClause(int linkId, QpArticleState state, bool isBackward)
        {
            return @$"
                STUFF((select ', ' + CONVERT(varchar(max),t.id)
                from {GetLinkTable(linkId, state, isBackward)} t 
                where t.linked_id = {GetLinkTable(linkId, state, isBackward)}.linked_id
                FOR XML PATH('')
              ),1,1,'')";
        }

        protected override string BuildLimitClause(int contentId, string whereClause, string pagingWhereClause, IList<string> orderBy, int count, bool reverse, QpArticleState state)
        {
            return $"select top({count}) * from {GetContentTable(contentId, state)} where {whereClause} and {pagingWhereClause} order by {BuildOrderbyClause(orderBy, reverse)}";
        }

        protected override string BuildTakeSkipClause(int contentId, string whereClause, IList<string> orderBy, int take, int skip, QpArticleState state)
        {
            var query = base.BuildTakeSkipClause(contentId, whereClause, orderBy, take, skip, state);

            query = $"{query} offset {skip} rows fetch next {take} rows only";

            return query;
        }
    }
}
