using Microsoft.Extensions.Logging;
using QP.GraphQL.Interfaces.Articles;
using QP.GraphQL.Interfaces.Articles.Filtering;
using QP.GraphQL.Interfaces.Articles.Paging;
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
        public QpArticlesAccessorBase(DbConnection connection, ILogger logger)
        {
            Connection = connection;
            Logger = logger;
        }

        public DbConnection Connection { get; }
        protected ILogger Logger { get; private set; }

        public async Task<IDictionary<int, QpArticle>> GetArticlesByIdList(int contentId, IEnumerable<int> articleIds)
        {
            if (!articleIds.Any())
                return new Dictionary<int, QpArticle>();

            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            var command = Connection.CreateCommand();

            command.CommandText = $"select * from content_{contentId}_live_new where content_item_id in ({String.Join(",", articleIds)})";
            command.CommandType = CommandType.Text;

            using (var reader = await command.ExecuteReaderAsync())
            {
                return ParseQpArticleReader(reader, contentId).ToDictionary(a => a.Id);
            }
        }

        public async Task<ILookup<int, QpArticle>> GetRelatedM2mArticlesByIdList(int contentId,
            IEnumerable<int> articleIds,
            int relationId,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where)
        {
            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            var command = Connection.CreateCommand();

            command.CommandText = @$"
                select m2m.l_item_ids, t.*
                from
                (select
                    {BuildIdsFieldClause()} as l_item_ids,
                    r_item_id 
                 from item_to_item  
                 where link_id={relationId} and l_item_id in ({String.Join(",", articleIds)})
                 group by r_item_id) as m2m
                 join content_{contentId}_live_new t on t.content_item_id = m2m.r_item_id
                   where {BuildWhereClause(where)} {(orderBy != null && orderBy.Any() ? "order by " + BuildOrderbyClause(orderBy, false) : "")}";
            command.CommandType = CommandType.Text;

            using (var reader = await command.ExecuteReaderAsync())
            {
                return ParseReaderForM2mLookup(reader, contentId);
            }
        }


        public async Task<ILookup<int, QpArticle>> GetRelatedM2oArticlesByIdList(int contentId, IEnumerable<int> articleIds, string backwardFieldname, IList<string> orderBy, IEnumerable<QpFieldFilterClause> where)
        {
            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            var command = Connection.CreateCommand();

            command.CommandText = @$"
                select [{backwardFieldname}] backward_id, *
                from content_{contentId}_live_new
                where [{backwardFieldname}] in ({String.Join(",", articleIds)}) and {BuildWhereClause(where)} {(orderBy != null && orderBy.Any() ? "order by " + BuildOrderbyClause(orderBy, false) : "")}";
            command.CommandType = CommandType.Text;

            using (var reader = await command.ExecuteReaderAsync())
            {
                return ParseReaderForM2oLookup(reader, contentId);
            }
        }

        public async Task<RelayPaginationResult> GetPagedArticles(int contentId,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            RelayPaginationArgs paginationArgs,
            bool calcTotalCount)
        {
            string query;
            string whereClause = BuildWhereClause(where);

            if (paginationArgs.First.HasValue || paginationArgs.Last.HasValue)
            {
                //спецификация по cursor-based пагинации, она же Relay: https://relay.dev/graphql/connections.htm
                //в случае cursor-based пагинации, нужно делать финальную сортировку по id для консистентности результата
                if (orderBy == null)
                    orderBy = new List<string>();

                orderBy.Insert(orderBy.Count, "content_item_id");

                bool takeRowsFromBeginning = paginationArgs.First.HasValue;
                string cursor = takeRowsFromBeginning ? paginationArgs.After : paginationArgs.Before;
                int count = takeRowsFromBeginning ? paginationArgs.First.Value : paginationArgs.Last.Value;
                if (count <= 0)
                    throw new ArgumentException($"Pagination parameter {(takeRowsFromBeginning ? "first" : "last")} must be positive");

                var pagingWhereClause = cursor != null ? BuildPagingWhereClause(contentId, orderBy, cursor, !takeRowsFromBeginning) : "(1=1)";

                if (takeRowsFromBeginning)
                {
                    
                    query = BuildLimitClause(contentId, whereClause, pagingWhereClause, orderBy, count + 1, false);
                }
                else
                {
                    query = $@" select * from (
                        {BuildLimitClause(contentId, whereClause, pagingWhereClause, orderBy, count + 1, true)}
                    ) tbl order by {BuildOrderbyClause(orderBy, false)}";
                }
            }
            else
            {
                if (orderBy != null)
                    query = $"select * from content_{contentId}_live_new where {whereClause} order by {BuildOrderbyClause(orderBy, false)}";
                else
                    query = $"select * from content_{contentId}_live_new where {whereClause}";
            }

            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            int? totalCount = null;
            if (calcTotalCount)
            {
                //считаем общее кол-во записей только если клиент попросил
                var commandForTotalCount = Connection.CreateCommand();

                commandForTotalCount.CommandText = $"select count(*) from content_{contentId}_live_new where {whereClause}";
                commandForTotalCount.CommandType = CommandType.Text;

                var totalCountObj = await commandForTotalCount.ExecuteScalarAsync();
                totalCount = Convert.ToInt32(totalCountObj);
            }

            var command = Connection.CreateCommand();

            command.CommandText = query;
            command.CommandType = CommandType.Text;

            using (var reader = await command.ExecuteReaderAsync())
            {
                var result = new RelayPaginationResult
                {
                    TotalCount = totalCount,
                    Articles = ParseQpArticleReader(reader, contentId)
                };

                if (paginationArgs.First.HasValue)
                {
                    result.HasNextPage = result.Articles.Count > paginationArgs.First.Value;//за счёт того, что в базе запрашивается First + 1 запись
                    result.HasPreviousPage = false;
                    if (result.HasNextPage)
                    {
                        //надо обрезать из результирующей выборки последнюю запись - она лишняя, т.к. запросили в базе на 1 больше, чем надо
                        result.Articles.RemoveAt(result.Articles.Count - 1);
                    }
                }
                else if (paginationArgs.Last.HasValue)
                {
                    result.HasNextPage = false;
                    result.HasPreviousPage = result.Articles.Count > paginationArgs.Last.Value;//за счёт того, что в базе запрашивается Last + 1 запись
                    if (result.HasPreviousPage)
                    {
                        //надо обрезать из результирующей выборки первую запись - она лишняя, т.к. запросили в базе на 1 больше, чем надо
                        result.Articles.RemoveAt(0);
                    }
                }
                else
                {
                    result.HasNextPage = false;
                    result.HasPreviousPage = false;
                }

                return result;
            }
        }

        protected abstract string BuildIdsFieldClause();
        protected abstract string BuildLimitClause(int contentId, string whereClause, string pagingWhereClause, IList<string> orderBy, int count, bool reverse);

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
                int[] ids = null;
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var column = reader.GetName(i).ToLowerInvariant();
                    if (string.Equals(column, "content_item_id", StringComparison.OrdinalIgnoreCase))
                        article.Id = reader.GetInt32(i);
                    else if (string.Equals(column, "l_item_ids", StringComparison.OrdinalIgnoreCase))
                    {
                        ids = reader.GetString(i).Split(',').Select(Int32.Parse).ToArray();
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


        private ILookup<int, QpArticle> ParseReaderForM2oLookup(DbDataReader reader, int contentId)
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
                    else if (string.Equals(column, "backward_id", StringComparison.OrdinalIgnoreCase))
                    {
                        backward_id = reader.GetInt32(i);
                    }
                    else
                    {
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

        private static string BuildPagingWhereClause(int contentId, IList<string> orderBy, string cursor, bool reverse)
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
({orderByColumn} {(ascending ^ reverse ? ">" : "<")} (select {orderByColumn} from content_{contentId}_live_new where content_item_id={cursor}) or 
({orderByColumn} = (select {orderByColumn} from content_{contentId}_live_new where content_item_id={cursor}) and ");
                whereClauseBuilder.Append("))");
            }

            return whereClauseBuilder.ToString();
        }

        private static string BuildWhereClause(IEnumerable<QpFieldFilterClause> where)
        {
            if (where == null || !where.Any())
                return "1=1";

            var numericFormat = new NumberFormatInfo { NumberDecimalSeparator = "." };
            StringBuilder whereBuilder = new StringBuilder();
            foreach (var clause in where)
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
    }
}
