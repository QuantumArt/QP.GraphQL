using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QP.GraphQL.Interfaces.Articles;
using QP.GraphQL.Interfaces.Articles.Filtering;
using QP.GraphQL.Interfaces.Articles.Paging;
using QP.GraphQL.Interfaces.DAL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QP.GraphQL.DAL
{
    public abstract class QpArticlesAccessorBase : IQpArticlesAccessor
    {
        public QpArticlesAccessorBase(DbConnection connection, IQueryService queryService, IOptions<QpArticlesAccessorSettings> options, ILogger logger)
        {
            Connection = connection;
            QueryService = queryService;
            Settings = options.Value;
            Logger = logger;
        }

        public DbConnection Connection { get; }
        protected IQueryService QueryService { get; private set; }
        protected QpArticlesAccessorSettings Settings { get; private set; }
        protected ILogger Logger { get; private set; }

        public async Task<IDictionary<int, QpArticle>> GetArticlesByIdList(int contentId, IDictionary<int, string> extensionMap, IEnumerable<int> articleIds, QpArticleState state)
        {
            if (!articleIds.Any())
                return new Dictionary<int, QpArticle>();

            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            var command = Connection.CreateCommand();

            command.CommandText = $"select * from {GetContentTable(contentId, state)} where content_item_id in (select id from {QueryService.GetIdTable("@articleds")})";
            command.CommandType = CommandType.Text;
            command.Parameters.Add(QueryService.GetIdParam("@articleds", articleIds));

            using (var reader = await command.ExecuteReaderAsync())
            {
                return ParseQpArticleReader(reader, contentId).ToDictionary(a => a.Id);
            }
        }

        public async Task<ILookup<int, QpArticle>> GetRelatedM2mArticlesByIdList(int contentId,
            IDictionary<int, string> extensionMap,
            IEnumerable<int> articleIds,
            int relationId,
            bool isBackward,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            QpArticleState state)
        {
            var whereContext = BuildWhereContext(where);

            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            var query = @$"
                select m2m.item_ids, t.*
                from
                (select
                    {BuildIdsFieldClause(relationId, state, isBackward)} as item_ids,
                    linked_id 
                 from {GetLinkTable(relationId, state, isBackward)}
                 where id in (select id from {QueryService.GetIdTable("@articleds")})
                 group by linked_id) as m2m
                 join {GetContentTable(contentId, state)} t on t.content_item_id = m2m.linked_id
                   where {whereContext} {(orderBy != null && orderBy.Any() ? "order by " + BuildOrderbyClause(orderBy, false) : "")}";

            var context = new QueryContext(query, whereContext);
            context.Parameters.Add(QueryService.GetIdParam("@articleds", articleIds));


            using (var reader = await context.GetCommand(Connection).ExecuteReaderAsync())
            {
                return ParseReaderForM2mLookup(reader, contentId);
            }
        }


        public async Task<ILookup<int, QpArticle>> GetRelatedM2oArticlesByIdList(int contentId,
            IDictionary<int, string> extensionMap,
            IEnumerable<int> articleIds,
            string backwardFieldname,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            QpArticleState state)
        {
            var whereContext = BuildWhereContext(where);

            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            var query = @$"
                select *
                from {GetContentTable(contentId, state)}
                where {AddDelimiter(backwardFieldname)} in (select id from {QueryService.GetIdTable("@articleds")}) and {whereContext} {(orderBy != null && orderBy.Any() ? "order by " + BuildOrderbyClause(orderBy, false) : "")}";


            var context = new QueryContext(query, whereContext);
            context.Parameters.Add(QueryService.GetIdParam("@articleds", articleIds));

            using (var reader = await context.GetCommand(Connection).ExecuteReaderAsync())
            {
                return ParseReaderForM2oLookup(reader, contentId, backwardFieldname);
            }
        }

        public async Task<RelayPaginationResult> GetPagedArticles(int contentId,
            IDictionary<int, string> extensionMap,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            RelayPaginationArgs paginationArgs,
            bool calcTotalCount,
            QpArticleState state)
        {
            string query;
            var whereContext = BuildWhereContext(where);
            var pagingWhereContext = QueryContext.EmptyWhere;
            var contentTable = GetContentTable(contentId, state);

            if (paginationArgs.Skip.HasValue && paginationArgs.First.HasValue)
            {
                var parameters = new List<string>();

                if (paginationArgs.Skip.Value < 0)
                    parameters.Add("skip");

                if (paginationArgs.First.Value <= 0)
                    parameters.Add("first");

                if (parameters.Any())
                    throw new ArgumentException($"Pagination parameter(s) {string.Join(", ", parameters)} must be positive/nonnegative");

                orderBy = PrepareOrderBy(orderBy);
                query = BuildTakeSkipClause(contentId, whereContext, orderBy, paginationArgs.First.Value, paginationArgs.Skip.Value, state);
            }
            else if (paginationArgs.First.HasValue || paginationArgs.Last.HasValue)
            {
                bool takeRowsFromBeginning = paginationArgs.First.HasValue;
                string cursor = takeRowsFromBeginning ? paginationArgs.After : paginationArgs.Before;
                int count = takeRowsFromBeginning ? paginationArgs.First.Value : paginationArgs.Last.Value;
                if (count <= 0)
                    throw new ArgumentException($"Pagination parameter {(takeRowsFromBeginning ? "first" : "last")} must be positive");

                orderBy = PrepareOrderBy(orderBy);

                if (cursor != null)
                {
                    pagingWhereContext = BuildPagingWhereContext(contentId, orderBy, cursor, !takeRowsFromBeginning, state);
                }

                if (takeRowsFromBeginning)
                {

                    query = BuildLimitClause(contentId, whereContext, pagingWhereContext, orderBy, count + 1, false, state);
                }
                else
                {
                    query = $@" select * from (
                        {BuildLimitClause(contentId, whereContext, pagingWhereContext, orderBy, count + 1, true, state)}
                    ) tbl order by {BuildOrderbyClause(orderBy, false)}";
                }
            }
            else
            {
                if (orderBy != null)
                    query = $"select * from {contentTable} where {whereContext} order by {BuildOrderbyClause(orderBy, false)}";
                else
                    query = $"select * from {contentTable} where {whereContext}";
            }


            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            int? totalCount = null;
            if (calcTotalCount)
            {
                //считаем общее кол-во записей только если клиент попросил
                var totalCountQuery = $"select count(*) from {contentTable} where {whereContext}";
                var commandForTotalCount = new QueryContext(totalCountQuery, whereContext).GetCommand(Connection);
                var totalCountObj = await commandForTotalCount.ExecuteScalarAsync();
                totalCount = Convert.ToInt32(totalCountObj);
            }

            var command = new QueryContext(query, whereContext, pagingWhereContext).GetCommand(Connection);
            IList<QpArticle> articles = null;

            using (var reader = await command.ExecuteReaderAsync())
            {
                articles = ParseQpArticleReader(reader, contentId);
            }

            var result = new RelayPaginationResult
            {
                TotalCount = totalCount,
                Articles = articles
            };

            // TODO: calculate HasNextPage, HasPreviousPage for Skip mode

            if (paginationArgs.First.HasValue && !paginationArgs.Skip.HasValue)
            {
                result.HasNextPage = result.Articles.Count > paginationArgs.First.Value;//за счёт того, что в базе запрашивается First + 1 запись

                if (result.HasNextPage)
                {
                    //надо обрезать из результирующей выборки последнюю запись - она лишняя, т.к. запросили в базе на 1 больше, чем надо
                    result.Articles.RemoveAt(result.Articles.Count - 1);
                }

                result.HasPreviousPage = await HasOtherPage(contentId, whereContext, orderBy, state, result.Articles.FirstOrDefault()?.Id.ToString(), paginationArgs, false);
            }
            else if (paginationArgs.Last.HasValue)
            {
                result.HasPreviousPage = result.Articles.Count > paginationArgs.Last.Value;//за счёт того, что в базе запрашивается Last + 1 запись
                if (result.HasPreviousPage)
                {
                    //надо обрезать из результирующей выборки первую запись - она лишняя, т.к. запросили в базе на 1 больше, чем надо
                    result.Articles.RemoveAt(0);
                }

                result.HasNextPage = await HasOtherPage(contentId, whereContext, orderBy, state, result.Articles.LastOrDefault()?.Id.ToString(), paginationArgs, true);
            }
            else
            {
                result.HasNextPage = false;
                result.HasPreviousPage = false;
            }

            return result;
        }

        protected abstract string BuildIdsFieldClause(int linkId, QpArticleState state, bool isBackward);
        protected abstract string BuildLimitClause(int contentId, string whereClause, string pagingWhereClause, IList<string> orderBy, int count, bool reverse, QpArticleState state);
        protected abstract string AddDelimiter(string identifier);

        protected virtual string BuildTakeSkipClause(int contentId, string whereClause, IList<string> orderBy, int take, int skip, QpArticleState state)
        {
            var query = $"select * from {GetContentTable(contentId, state)} where {whereClause}";

            if (orderBy != null)
            {
                query = $"{query} order by {BuildOrderbyClause(orderBy, false)}";
            }

            return query;
        }

        /// <summary>
        /// Cпецификация по cursor-based пагинации, она же Relay: https://relay.dev/graphql/connections.htm
        /// в случае cursor-based пагинации, нужно делать финальную сортировку по id для консистентности результата
        /// </summary>
        /// <param name="orderBy"></param>
        /// <returns></returns>
        private IList<string> PrepareOrderBy(IList<string> orderBy)
        {
            if (orderBy == null)
                orderBy = new List<string>();

            orderBy.Insert(orderBy.Count, "content_item_id");

            return orderBy;
        }

        private async Task<bool> HasOtherPage(int contentId, QueryContext whereContext, IList<string> orderBy, QpArticleState state, string cursor, RelayPaginationArgs paginationArgs, bool checkNext)
        {
            if ((checkNext && paginationArgs.Last.HasValue && paginationArgs.Before == null) || (!checkNext && paginationArgs.First.HasValue && paginationArgs.After == null))
            {
                return false;
            }
            else if (Settings.CalculatePagingData && cursor != null)
            {
                var pagingWhereContext = BuildPagingWhereContext(contentId, orderBy, cursor, !checkNext, state);
                var query = BuildLimitClause(contentId, whereContext, pagingWhereContext, orderBy, 1, !checkNext, state);
                query = $"select count(*) from ({query}) tbl";

                var commandForNextPageCount = new QueryContext(query, whereContext, pagingWhereContext).GetCommand(Connection);

                var totalCountObj = await commandForNextPageCount.ExecuteScalarAsync();
                var totalCount = Convert.ToInt32(totalCountObj);
                return totalCount > 0;
            }
            else
            {
                return false;
            }
        }

        private List<QpArticle> ParseQpArticleReader(DbDataReader reader, int contentId)
        {
            var result = new List<QpArticle>();
            while (reader.Read())
            {
                var article = new QpArticle(contentId);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var column = reader.GetName(i).ToLowerInvariant();

                    if (!ParseSystemFields(reader, article, i))
                    {
                        var val = reader.GetValue(i);
                        article.AllFields.Add(column, val is DBNull ? null : val);
                    }
                }

                if (article.Id > 0) result.Add(article);
            }

            return result;
        }

        private ILookup<int, QpArticle> ParseReaderForM2mLookup(DbDataReader reader, int contentId)
        {
            var result = new List<Tuple<int[], QpArticle>>();
            while (reader.Read())
            {
                var article = new QpArticle(contentId);
                int[] ids = reader.GetString(0).Split(',').Select(Int32.Parse).ToArray(); ;
                for (var i = 1; i < reader.FieldCount; i++)
                {
                    var column = reader.GetName(i).ToLowerInvariant();
                    if (!ParseSystemFields(reader, article, i))
                    {
                        var val = reader.GetValue(i);
                        article.AllFields.Add(column, val is DBNull ? null : val);
                    }
                }

                if (article.Id > 0 && ids != null)
                    result.Add(new Tuple<int[], QpArticle>(ids, article));
            }

            return result.SelectMany(t => t.Item1.Select(id => new Tuple<int, QpArticle>(id, t.Item2)))
                .ToLookup(t => t.Item1, t => t.Item2);
        }


        private ILookup<int, QpArticle> ParseReaderForM2oLookup(DbDataReader reader, int contentId, string backwardFieldname)
        {
            var result = new List<Tuple<int, QpArticle>>();
            while (reader.Read())
            {
                var article = new QpArticle(contentId);
                int backward_id = 0;
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var column = reader.GetName(i).ToLowerInvariant();

                    if (!ParseSystemFields(reader, article, i))
                    {
                        if (string.Equals(column, backwardFieldname, StringComparison.OrdinalIgnoreCase))
                        {
                            backward_id = reader.GetInt32(i);
                        }

                        var val = reader.GetValue(i);
                        article.AllFields.Add(column, val is DBNull ? null : val);
                    }
                }

                if (article.Id > 0 && backward_id > 0)
                {
                    result.Add(new Tuple<int, QpArticle>(backward_id, article));
                }
            }

            return result.ToLookup(t => t.Item1, t => t.Item2);
        }

        protected static string BuildOrderbyClause(IList<string> orderBy, bool reverse)
        {
            StringBuilder orderByClauseBuilder = new StringBuilder();
            foreach (var orderByToken in orderBy)
            {
                if (orderByClauseBuilder.Length > 0)
                    orderByClauseBuilder.Append(", ");

                if (orderByToken.Contains("^"))
                    orderByClauseBuilder.Append($"{orderByToken.Replace("^", "")} {(reverse ? "asc" : "desc")}");
                else
                    orderByClauseBuilder.Append($"{orderByToken} {(reverse ? "desc" : "asc")}");
            }

            return orderByClauseBuilder.ToString();
        }

        private static bool ParseSystemFields(DbDataReader reader, QpArticle article, int position)
        {
            var column = reader.GetName(position).ToLowerInvariant();
            if (string.Equals(column, QpSystemFieldsDescriptor.Id.DBName, StringComparison.OrdinalIgnoreCase))
                article.Id = reader.GetInt32(position);
            else if (string.Equals(column, QpSystemFieldsDescriptor.StatusTypeId.DBName, StringComparison.OrdinalIgnoreCase))
                article.StatusTypeId = reader.GetInt32(position);
            else if (string.Equals(column, QpSystemFieldsDescriptor.Created.DBName, StringComparison.OrdinalIgnoreCase))
                article.Created = reader.GetDateTime(position);
            else if (string.Equals(column, QpSystemFieldsDescriptor.Modified.DBName, StringComparison.OrdinalIgnoreCase))
                article.Modified = reader.GetDateTime(position);
            else if (string.Equals(column, QpSystemFieldsDescriptor.LastModifiedBy.DBName, StringComparison.OrdinalIgnoreCase))
                article.LastModifiedBy = reader.GetInt32(position);
            else
            {
                return false;
            }

            return true;
        }

        private QueryContext BuildPagingWhereContext(int contentId, IList<string> orderBy, string cursor, bool reverse, QpArticleState state)
        {
            //для понимания структуры выражения where, которое строится здесь, надо изучить
            //https://stackoverflow.com/questions/56989560/how-to-get-a-cursor-for-pagination-in-graphql-from-a-database

            //курсор здесь, в отличие от большинства реализаций курсор-пагинаций, просто content_item_id, посчитал, что приводить его к base64 просто лишняя работа
            //альтернативно можно бы сделать курсор как content_item_id + все значения, участвующих в запросе полей сортировки, 
            //так бы это более соответствовало стандарту в части того, что курсор должен быть "opaque"
            if (!Int32.TryParse(cursor, out _))
                throw new ArgumentException("Cursor must be integer");

            var cursorParam = QueryService.GetParameter("cursor", SqlDbType.NVarChar, cursor);

            StringBuilder whereClauseBuilder = new StringBuilder($"(content_item_id {(reverse ? "<" : ">")} {cursor})");

            foreach (var orderByToken in orderBy.Reverse())
            {
                if (orderByToken == "content_item_id")
                    continue;

                string orderByColumn;
                bool ascending;
                if (orderByToken.Contains("^"))
                {
                    orderByColumn = orderByToken.Replace("^", "");
                    ascending = false;
                }
                else
                {
                    orderByColumn = orderByToken;
                    ascending = true;
                }

                whereClauseBuilder.Insert(0, @$"
({orderByColumn} {(ascending ^ reverse ? ">" : "<")} (select {orderByColumn} from {GetContentTable(contentId, state)} where content_item_id={cursorParam.ParameterName}) or 
({orderByColumn} = (select {orderByColumn} from {GetContentTable(contentId, state)} where content_item_id={cursorParam.ParameterName}) and ");
                whereClauseBuilder.Append("))");
            }
            
            return new QueryContext(whereClauseBuilder.ToString(), cursorParam);
        }

        private QueryContext BuildWhereContext(IEnumerable<QpFieldFilterClause> where)
        {
            var actualWhere = where?.Where(w => w.Value != null);

            if (actualWhere == null || !actualWhere.Any())
                return QueryContext.EmptyWhere;

            var contexts = actualWhere.Select(QueryService.GetQueryContext).ToArray();
            var query = string.Join<IQueryContext>(" and ", contexts);
            return new QueryContext(query, contexts);
        }

        protected static string GetLinkTable(int linkId, QpArticleState state, bool isBackward)
        {
            string backward = isBackward ? "_rev" : null;

            return state switch
            {
                QpArticleState.Live => $"item_link_{linkId}{backward}",
                QpArticleState.Stage => $"item_link_{linkId}_united{backward}",
                _ => null
            };
        }

        protected static string GetContentTable(int contentId, QpArticleState state)
        {
            return state switch
            {
                QpArticleState.Live => $"content_{contentId}_live_new",
                QpArticleState.Stage => $"content_{contentId}_stage_new",
                _ => null
            };
        }
    }
}
