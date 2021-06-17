using Microsoft.Extensions.Logging;
using QP.GraphQL.Interfaces.Articles;
using System.Collections.Generic;
using System.Data.Common;

namespace QP.GraphQL.DAL
{
    public class QpArticlesAccessorSqlServer : QpArticlesAccessorBase
    {
        public QpArticlesAccessorSqlServer(DbConnection connection, ILogger<QpArticlesAccessorSqlServer> logger) : base(connection, logger)
        {

        }

        protected override string AddDelimiter(string identifier) => $"[{identifier}]";
 
        protected override string BuildIdsFieldClause()
        {
            return @$"
                STUFF((select ', ' + CONVERT(varchar(max),t.l_item_id)
                from item_to_item t 
                where t.r_item_id = item_to_item.r_item_id
                FOR XML PATH('')
              ),1,1,'')";
        }

        protected override string BuildLimitClause(int contentId, string whereClause, string pagingWhereClause, IList<string> orderBy, int count, bool reverse, QpArticleState state)
        {
            return $"select top({count}) * from {GetContentTable(contentId, state)} where {whereClause} and {pagingWhereClause} order by {BuildOrderbyClause(orderBy, reverse)}";
        }
    }
}
