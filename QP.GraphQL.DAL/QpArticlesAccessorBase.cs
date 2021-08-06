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
using System.Globalization;
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

        public async Task<IDictionary<int, QpArticle>> GetArticlesByIdList(int contentId, IEnumerable<int> articleIds, QpArticleState state)
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
            IEnumerable<int> articleIds,
            int relationId,
            bool isBackward,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            QpArticleState state)
        {
            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            var command = Connection.CreateCommand();

            command.CommandText = @$"
                select m2m.item_ids, t.*
                from
                (select
                    {BuildIdsFieldClause(relationId, state, isBackward)} as item_ids,
                    linked_id 
                 from {GetLinkTable(relationId, state, isBackward)}
                 where id in (select id from {QueryService.GetIdTable("@articleds")})
                 group by linked_id) as m2m
                 join {GetContentTable(contentId, state)} t on t.content_item_id = m2m.linked_id
                   where {BuildWhereClause(where)} {(orderBy != null && orderBy.Any() ? "order by " + BuildOrderbyClause(orderBy, false) : "")}";
            command.CommandType = CommandType.Text;
            command.Parameters.Add(QueryService.GetIdParam("@articleds", articleIds));

            using (var reader = await command.ExecuteReaderAsync())
            {
                return ParseReaderForM2mLookup(reader, contentId);
            }
        }


        public async Task<ILookup<int, QpArticle>> GetRelatedM2oArticlesByIdList(int contentId,
            IEnumerable<int> articleIds,
            string backwardFieldname,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            QpArticleState state)
        {
            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            var command = Connection.CreateCommand();

            command.CommandText = @$"
                select *
                from {GetContentTable(contentId, state)}
                where {AddDelimiter(backwardFieldname)} in (select id from {QueryService.GetIdTable("@articleds")}) and {BuildWhereClause(where)} {(orderBy != null && orderBy.Any() ? "order by " + BuildOrderbyClause(orderBy, false) : "")}";
            command.CommandType = CommandType.Text;
            command.Parameters.Add(QueryService.GetIdParam("@articleds", articleIds));

            using (var reader = await command.ExecuteReaderAsync())
            {
                return ParseReaderForM2oLookup(reader, contentId, backwardFieldname);
            }
        }

        public async Task<RelayPaginationResult> GetPagedArticles(int contentId,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            RelayPaginationArgs paginationArgs,
            bool calcTotalCount,
            QpArticleState state)
        {
            string query;
            string whereClause = BuildWhereClause(where);

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
                query = BuildTakeSkipClause(contentId, whereClause, orderBy, paginationArgs.First.Value, paginationArgs.Skip.Value, state);
            }
            else if (paginationArgs.First.HasValue || paginationArgs.Last.HasValue)
            {                
                bool takeRowsFromBeginning = paginationArgs.First.HasValue;
                string cursor = takeRowsFromBeginning ? paginationArgs.After : paginationArgs.Before;
                int count = takeRowsFromBeginning ? paginationArgs.First.Value : paginationArgs.Last.Value;
                if (count <= 0)
                    throw new ArgumentException($"Pagination parameter {(takeRowsFromBeginning ? "first" : "last")} must be positive");
                
                orderBy = PrepareOrderBy(orderBy);

                var pagingWhereClause = cursor != null ? BuildPagingWhereClause(contentId, orderBy, cursor, !takeRowsFromBeginning, state) : "(1=1)";

                if (takeRowsFromBeginning)
                {
                    
                    query = BuildLimitClause(contentId, whereClause, pagingWhereClause, orderBy, count + 1, false, state);
                }
                else
                {
                    query = $@" select * from (
                        {BuildLimitClause(contentId, whereClause, pagingWhereClause, orderBy, count + 1, true, state)}
                    ) tbl order by {BuildOrderbyClause(orderBy, false)}";
                }
            }
            else
            {
                if (orderBy != null)
                    query = $"select * from {GetContentTable(contentId, state)} where {whereClause} order by {BuildOrderbyClause(orderBy, false)}";
                else
                    query = $"select * from {GetContentTable(contentId, state)} where {whereClause}";
            }

            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            int? totalCount = null;
            if (calcTotalCount)
            {
                //считаем общее кол-во записей только если клиент попросил
                var commandForTotalCount = Connection.CreateCommand();

                commandForTotalCount.CommandText = $"select count(*) from {GetContentTable(contentId, state)} where {whereClause}";
                commandForTotalCount.CommandType = CommandType.Text;

                var totalCountObj = await commandForTotalCount.ExecuteScalarAsync();
                totalCount = Convert.ToInt32(totalCountObj);
            }

            var command = Connection.CreateCommand();

            command.CommandText = query;
            command.CommandType = CommandType.Text;
            IList<QpArticle> articles =null;

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

                result.HasPreviousPage = await HasOtherPage(contentId, whereClause, orderBy, state, result.Articles.FirstOrDefault()?.Id.ToString(), paginationArgs, false);
            }
            else if (paginationArgs.Last.HasValue)
            {                    
                result.HasPreviousPage = result.Articles.Count > paginationArgs.Last.Value;//за счёт того, что в базе запрашивается Last + 1 запись
                if (result.HasPreviousPage)
                {
                    //надо обрезать из результирующей выборки первую запись - она лишняя, т.к. запросили в базе на 1 больше, чем надо
                    result.Articles.RemoveAt(0);
                }

                result.HasNextPage = await HasOtherPage(contentId, whereClause, orderBy, state, result.Articles.LastOrDefault()?.Id.ToString(), paginationArgs, true);
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

        private async Task<bool> HasOtherPage(int contentId, string whereClause, IList<string> orderBy, QpArticleState state, string cursor, RelayPaginationArgs paginationArgs, bool checkNext)
        {
            if ((checkNext && paginationArgs.Last.HasValue && paginationArgs.Before == null) || (!checkNext && paginationArgs.First.HasValue && paginationArgs.After == null))
            {
                return false;
            }
            else if (Settings.CalculatePagingData && cursor != null)
            {
                var commandForNextPageCount = Connection.CreateCommand();
                var pagingWhereClause = BuildPagingWhereClause(contentId, orderBy, cursor, !checkNext, state);
                var query = BuildLimitClause(contentId, whereClause, pagingWhereClause, orderBy, 1, !checkNext, state);
                query = $"select count(*) from ({query}) tbl";                

                commandForNextPageCount.CommandText = query;
                commandForNextPageCount.CommandType = CommandType.Text;

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
                    if (string.Equals(column, "content_item_id", StringComparison.OrdinalIgnoreCase))
                        article.Id = reader.GetInt32(i);
                    else
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
                    if (string.Equals(column, "content_item_id", StringComparison.OrdinalIgnoreCase))
                    {
                        article.Id = reader.GetInt32(i);
                    }
                    else
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

                    if (string.Equals(column, "content_item_id", StringComparison.OrdinalIgnoreCase))
                    {
                        article.Id = reader.GetInt32(i);
                    }
                    else
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

        private static string BuildPagingWhereClause(int contentId, IList<string> orderBy, string cursor, bool reverse, QpArticleState state)
        {
            //для понимания структуры выражения where, которое строится здесь, надо изучить
            //https://stackoverflow.com/questions/56989560/how-to-get-a-cursor-for-pagination-in-graphql-from-a-database

            //курсор здесь, в отличие от большинства реализаций курсор-пагинаций, просто content_item_id, посчитал, что приводить его к base64 просто лишняя работа
            //альтернативно можно бы сделать курсор как content_item_id + все значения, участвующих в запросе полей сортировки, 
            //так бы это более соответствовало стандарту в части того, что курсор должен быть "opaque"
            if (!Int32.TryParse(cursor, out _))
                throw new ArgumentException("Cursor must be integer");

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
({orderByColumn} {(ascending ^ reverse ? ">" : "<")} (select {orderByColumn} from {GetContentTable(contentId, state)} where content_item_id={cursor}) or 
({orderByColumn} = (select {orderByColumn} from {GetContentTable(contentId, state)} where content_item_id={cursor}) and ");
                whereClauseBuilder.Append("))");
            }

            return whereClauseBuilder.ToString();
        }

        private static string BuildWhereClause(IEnumerable<QpFieldFilterClause> where)
        {
            var actualWhere = where?.Where(w => w.Value != null);

            if (actualWhere == null || !actualWhere.Any())
                return "1=1";

            var numericFormat = new NumberFormatInfo { NumberDecimalSeparator = "." };
            StringBuilder whereBuilder = new StringBuilder();
            foreach (var clause in actualWhere)
            {
                if (whereBuilder.Length > 0)
                    whereBuilder.Append(" and ");

                string leftPart = clause.FilterDefinition.QpFieldType switch
                {
                    "Time" => $"{clause.FilterDefinition.QpFieldName}::time",
                    _ => clause.FilterDefinition.QpFieldName,
                };
                string op = clause.FilterDefinition.Operator switch
                {
                    FilterOperator.Equal => "=",
                    FilterOperator.NotEqual => "!=",
                    FilterOperator.GreaterThan => ">",
                    FilterOperator.GreaterOrEqual => ">=",
                    FilterOperator.LessThan => "<",
                    FilterOperator.LessOrEqual => "<=",
                    FilterOperator.Like => "like",
                    FilterOperator.NotLike => "not like",
                    _ => throw new NotImplementedException($"Unsupported operator {clause.FilterDefinition.Operator}")
                };
                string rightPart = clause.FilterDefinition.QpFieldType switch
                {
                    "Numeric" => Convert.ToDecimal(clause.Value).ToString(numericFormat),
                    "Boolean" => Convert.ToBoolean(clause.Value) ? "1" : "0",
                    "Date" => $"'{Convert.ToDateTime(clause.Value):yyyy-MM-dd}'",
                    "DateTime" => $"'{Convert.ToDateTime(clause.Value):O}'",
                    "Time" => $"'{Convert.ToDateTime(clause.Value):HH:mm:ss}'",
                    "Relation" => Convert.ToInt32(clause.Value).ToString(), //только для o2m связей
                    _ => $"'{clause.Value}'",
                };

                if (clause.FilterDefinition.Operator == FilterOperator.Like || clause.FilterDefinition.Operator == FilterOperator.NotLike)
                {
                    //хак для регистронезависимости
                    leftPart = $"lower({leftPart})";
                    rightPart = rightPart.ToLowerInvariant();
                }

                whereBuilder.Append($"({leftPart} {op} {rightPart})");
            }
            return whereBuilder.ToString();
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
