using Microsoft.Extensions.Logging;
using QP.GraphQL.Interfaces.Articles;
using System.Collections.Generic;
using System.Data.Common;

namespace QP.GraphQL.DAL
{
    public class QpArticlesAccessorPostgres : QpArticlesAccessorBase
    {
        public QpArticlesAccessorPostgres(DbConnection connection, ILogger<QpArticlesAccessorPostgres> logger) : base(connection, logger)
        {

        }

        protected override string BuildIdsFieldClause()
        {
            return "array_to_string(array_agg(l_item_id), ',')";
        }

        protected override string BuildLimitClause(int contentId, string whereClause, string pagingWhereClause, IList<string> orderBy, int count, bool reverse, QpArticleState state)
        {
            return $"select * from {GetContentTable(contentId, state)} where {whereClause} and {pagingWhereClause} order by {BuildOrderbyClause(orderBy, reverse)} limit {count}";
        }
    }
}
