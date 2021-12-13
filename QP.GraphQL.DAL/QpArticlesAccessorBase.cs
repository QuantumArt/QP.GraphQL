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

        public async Task<IDictionary<int, QpArticle>> GetArticlesByIdList(int contentId, RootContext rootContext, IEnumerable<int> articleIds, QpArticleState state)
        {
            if (!articleIds.Any())
                return new Dictionary<int, QpArticle>();

            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            var command = Connection.CreateCommand();

            var fields = GetContentFields(rootContext);
            command.CommandText = $"select {fields} from {GetContentTable(state, rootContext)} where {rootContext.TableALias}.{QpSystemFieldsDescriptor.Id.DBName} in (select id from {QueryService.GetIdTable("@articleds")})";
            command.CommandType = CommandType.Text;
            command.Parameters.Add(QueryService.GetIdParam("@articleds", articleIds));

            using (var reader = await command.ExecuteReaderAsync())
            {
                return ParseQpArticleReader(reader, rootContext).ToDictionary(a => a.Id);
            }
        }

        public async Task<ILookup<int, QpArticle>> GetRelatedM2mArticlesByIdList(int contentId,
            RootContext rootContext,
            IEnumerable<int> articleIds,
            int relationId,
            bool isBackward,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            QpArticleState state)
        {
            var whereContext = BuildWhereContext(where, state, rootContext);

            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            var fields = GetContentFields(rootContext);

            var query = @$"
                select m2m.item_ids, {fields}
                from
                (select
                    {BuildIdsFieldClause(relationId, state, isBackward)} as item_ids,
                    linked_id 
                 from {GetLinkTable(relationId, state, isBackward)}
                 where id in (select id from {QueryService.GetIdTable("@articleds")})
                 group by linked_id) as m2m
                 join {GetContentTable(state, rootContext, $" on {rootContext.TableALias}.{QpSystemFieldsDescriptor.Id.DBName} = m2m.linked_id")}
                   where {whereContext} {(orderBy != null && orderBy.Any() ? "order by " + BuildOrderbyClause(orderBy, false, rootContext) : "")}";

            var context = new QueryContext(query, whereContext);
            context.Parameters.Add(QueryService.GetIdParam("@articleds", articleIds));


            using (var reader = await context.GetCommand(Connection).ExecuteReaderAsync())
            {
                return ParseReaderForM2mLookup(reader, rootContext);
            }
        }


        public async Task<ILookup<int, QpArticle>> GetRelatedM2oArticlesByIdList(int contentId,
            RootContext rootContext,
            IEnumerable<int> articleIds,
            string backwardFieldname,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            QpArticleState state)
        {
            var whereContext = BuildWhereContext(where, state, rootContext);

            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            var fields = GetContentFields(rootContext);

            var query = @$"
                select {fields}
                from {GetContentTable(state, rootContext)}
                where {AddDelimiter(backwardFieldname)} in (select id from {QueryService.GetIdTable("@articleds")}) and {whereContext} {(orderBy != null && orderBy.Any() ? "order by " + BuildOrderbyClause(orderBy, false, rootContext) : "")}";


            var context = new QueryContext(query, whereContext);
            context.Parameters.Add(QueryService.GetIdParam("@articleds", articleIds));

            using (var reader = await context.GetCommand(Connection).ExecuteReaderAsync())
            {
                return ParseReaderForM2oLookup(reader, rootContext, backwardFieldname);
            }
        }

        public async Task<RelayPaginationResult> GetPagedArticles(int contentId,
            RootContext rootContext,
            IList<string> orderBy,
            IEnumerable<QpFieldFilterClause> where,
            RelayPaginationArgs paginationArgs,
            bool calcTotalCount,
            QpArticleState state)
        {
            string query;




            var whereContext = BuildWhereContext(where, state, rootContext);
            var pagingWhereContext = QueryContext.EmptyWhere;
            var contentTable = GetContentTable(state, rootContext);
            var fields = GetContentFields(rootContext);

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
                query = BuildTakeSkipClause(rootContext, fields, whereContext, orderBy, paginationArgs.First.Value, paginationArgs.Skip.Value, state);
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
                    pagingWhereContext = BuildPagingWhereContext(rootContext, orderBy, cursor, !takeRowsFromBeginning, state);
                }

                if (takeRowsFromBeginning)
                {

                    query = BuildLimitClause(rootContext, fields, whereContext, pagingWhereContext, orderBy, count + 1, false, state);
                }
                else
                {
                    query = $@" select * from (
                        {BuildLimitClause(rootContext, fields, whereContext, pagingWhereContext, orderBy, count + 1, true, state)}
                    ) tbl order by {BuildOrderbyClause(orderBy, false, rootContext, isExternalQuery:true)}";
                }
            }
            else
            {
                if (orderBy != null)
                    query = $"select {fields} from {contentTable} where {whereContext} order by {BuildOrderbyClause(orderBy, false, rootContext)}";
                else
                    query = $"select {fields} from {contentTable} where {whereContext}";
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
                articles = ParseQpArticleReader(reader, rootContext);
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

                result.HasPreviousPage = await HasOtherPage(rootContext, whereContext, orderBy, state, result.Articles.FirstOrDefault()?.Id.ToString(), paginationArgs, false);
            }
            else if (paginationArgs.Last.HasValue)
            {
                result.HasPreviousPage = result.Articles.Count > paginationArgs.Last.Value;//за счёт того, что в базе запрашивается Last + 1 запись
                if (result.HasPreviousPage)
                {
                    //надо обрезать из результирующей выборки первую запись - она лишняя, т.к. запросили в базе на 1 больше, чем надо
                    result.Articles.RemoveAt(0);
                }

                result.HasNextPage = await HasOtherPage(rootContext, whereContext, orderBy, state, result.Articles.LastOrDefault()?.Id.ToString(), paginationArgs, true);
            }
            else
            {
                result.HasNextPage = false;
                result.HasPreviousPage = false;
            }

            return result;
        }

        protected abstract string BuildIdsFieldClause(int linkId, QpArticleState state, bool isBackward);
        protected abstract string BuildLimitClause(RootContext context, string fields, string whereClause, string pagingWhereClause, IList<string> orderBy, int count, bool reverse, QpArticleState state);
        protected abstract string AddDelimiter(string identifier);

        protected virtual string BuildTakeSkipClause(RootContext context, string fields, string whereClause, IList<string> orderBy, int take, int skip, QpArticleState state)
        {
            var query = $"select {fields} from {GetContentTable(state, context)} where {whereClause}";

            if (orderBy != null)
            {
                query = $"{query} order by {BuildOrderbyClause(orderBy, false, context)}";
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

        private async Task<bool> HasOtherPage(RootContext context, QueryContext whereContext, IList<string> orderBy, QpArticleState state, string cursor, RelayPaginationArgs paginationArgs, bool checkNext)
        {
            if ((checkNext && paginationArgs.Last.HasValue && paginationArgs.Before == null) || (!checkNext && paginationArgs.First.HasValue && paginationArgs.After == null))
            {
                return false;
            }
            else if (Settings.CalculatePagingData && cursor != null)
            {
                var pagingWhereContext = BuildPagingWhereContext(context, orderBy, cursor, !checkNext, state);
                var query = BuildLimitClause(context, "*", whereContext, pagingWhereContext, orderBy, 1, !checkNext, state);
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

        private QpArticle ParseSingleQpArticleReader(DbDataReader reader, RootContext rootContext)
        {
            var article = new QpArticle(rootContext.ContentId);

            article.Id = (int)reader[QpSystemFieldsDescriptor.Id.DBName];
            article.StatusTypeId = (int)reader[QpSystemFieldsDescriptor.StatusTypeId.DBName];
            article.Created = (DateTime)reader[QpSystemFieldsDescriptor.Created.DBName];
            article.Modified = (DateTime)reader[QpSystemFieldsDescriptor.Modified.DBName];
            article.LastModifiedBy = (int)reader[QpSystemFieldsDescriptor.LastModifiedBy.DBName];

            if (rootContext.Classifier != null)
            {
                var classifier = reader[rootContext.Classifier.QueryAlias];
                article.ExtensionContentId = classifier is DBNull ? (int?)null : (int)classifier;

                var extension = rootContext.Extensions.FirstOrDefault(e => e.ContentId == article.ExtensionContentId);

                if (extension != null)
                {
                    foreach (var f in extension.Fields)
                    {
                        var value = reader[f.QueryAlias];
                        value = value is DBNull ? null : value;
                        article.AllFields.Add(f.QueryAlias.ToLowerInvariant(), value);
                    }
                }

            }

            foreach (var f in rootContext.Fields)
            {
                var value = reader[f.QueryAlias];
                value = value is DBNull ? null : value;
                article.AllFields.Add(f.QueryAlias.ToLowerInvariant(), value);
            }

            return article;
        }

        private List<QpArticle> ParseQpArticleReader(DbDataReader reader, RootContext rootContext)
        {
            var result = new List<QpArticle>();

            while (reader.Read())
            {
                var article = ParseSingleQpArticleReader(reader, rootContext);
                result.Add(article);
            }
            
            return result;
        }

        private ILookup<int, QpArticle> ParseReaderForM2mLookup(DbDataReader reader, RootContext rootContext)
        {
            var result = new List<Tuple<int[], QpArticle>>();
            while (reader.Read())
            {
                int[] ids = reader.GetString(0).Split(',').Select(Int32.Parse).ToArray();

                var article = ParseSingleQpArticleReader(reader, rootContext);

                if (article.Id > 0 && ids != null)
                    result.Add(new Tuple<int[], QpArticle>(ids, article));
            }

            return result.SelectMany(t => t.Item1.Select(id => new Tuple<int, QpArticle>(id, t.Item2)))
                .ToLookup(t => t.Item1, t => t.Item2);
        }


        private ILookup<int, QpArticle> ParseReaderForM2oLookup(DbDataReader reader, RootContext rootContext, string backwardFieldname)
        {
            var result = new List<Tuple<int, QpArticle>>();
            while (reader.Read())
            {
                var article = ParseSingleQpArticleReader(reader, rootContext);
                var backwardFieldAlias = rootContext.Fields.FirstOrDefault(f => f.Alias == backwardFieldname)?.QueryAlias;
                int backward_id = backwardFieldAlias == null ? 0 : (int)reader[backwardFieldAlias];                

                if (article.Id > 0 && backward_id > 0)
                {
                    result.Add(new Tuple<int, QpArticle>(backward_id, article));
                }
            }

            return result.ToLookup(t => t.Item1, t => t.Item2);
        }

        protected string BuildOrderbyClause(IList<string> orderBy, bool reverse, RootContext context, bool isExternalQuery = false)
        {
            StringBuilder orderByClauseBuilder = new StringBuilder();
            foreach (var orderByToken in orderBy)
            {
                if (orderByClauseBuilder.Length > 0)
                    orderByClauseBuilder.Append(", ");

                bool desc = orderByToken.Contains("^");
                string order = desc ? orderByToken.Replace("^", "") : orderByToken;

                if (isExternalQuery)
                {
                    var fieldorder = context.Fields.FirstOrDefault(f => f.Alias.Equals(order, StringComparison.InvariantCultureIgnoreCase))?.QueryAlias;
                    order = fieldorder == null ? order : fieldorder;
                    order = AddDelimiter(order);
                }
                else
                {
                    order = $"{context.TableALias}.{AddDelimiter(order)}";
                }

                if (desc)
                    orderByClauseBuilder.Append($"{order} {(reverse ? "asc" : "desc")}");
                else
                    orderByClauseBuilder.Append($"{order} {(reverse ? "desc" : "asc")}");
            }

            return orderByClauseBuilder.ToString();
        }

        private QueryContext BuildPagingWhereContext(RootContext rootContext, IList<string> orderBy, string cursor, bool reverse, QpArticleState state)
        {
            //для понимания структуры выражения where, которое строится здесь, надо изучить
            //https://stackoverflow.com/questions/56989560/how-to-get-a-cursor-for-pagination-in-graphql-from-a-database

            //курсор здесь, в отличие от большинства реализаций курсор-пагинаций, просто content_item_id, посчитал, что приводить его к base64 просто лишняя работа
            //альтернативно можно бы сделать курсор как content_item_id + все значения, участвующих в запросе полей сортировки, 
            //так бы это более соответствовало стандарту в части того, что курсор должен быть "opaque"
            if (!Int32.TryParse(cursor, out int cursorValue))
                throw new ArgumentException("Cursor must be integer");

            var cursorParam = QueryService.GetParameter("cursor", SqlDbType.Int, cursorValue);

            StringBuilder whereClauseBuilder = new StringBuilder($"({rootContext.TableALias}.{QpSystemFieldsDescriptor.Id.DBName} {(reverse ? "<" : ">")} {cursorParam.ParameterName})");

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
({rootContext.TableALias}.{AddDelimiter(orderByColumn)} {(ascending ^ reverse ? ">" : "<")} (select {orderByColumn} from {GetContentTable(rootContext.ContentId, state)} where content_item_id={cursorParam.ParameterName}) or 
({rootContext.TableALias}.{AddDelimiter(orderByColumn)} = (select {orderByColumn} from {GetContentTable(rootContext.ContentId, state)} where content_item_id={cursorParam.ParameterName}) and ");
                whereClauseBuilder.Append("))");
            }
            
            return new QueryContext(whereClauseBuilder.ToString(), cursorParam);
        }

        private QueryContext BuildWhereContext(IEnumerable<QpFieldFilterClause> where, QpArticleState state, RootContext rootContext)
        {
            var actualWhere = where?.Where(w => w.Value != null);

            if (actualWhere == null || !actualWhere.Any())
                return QueryContext.EmptyWhere;

            var contexts = actualWhere.Select(w => GetRelationFilterClause(w, state, rootContext))
                .Select(QueryService.GetQueryContext)
                .ToArray();

            var query = string.Join<IQueryContext>(" and ", contexts);
            return new QueryContext(query, contexts);
        }

        private static QpFieldRelationFilterClause GetRelationFilterClause(QpFieldFilterClause clause, QpArticleState state, RootContext rootContext)
        {
            var field = rootContext.Fields.FirstOrDefault(f => f.Alias == clause.FilterDefinition.QpFieldName);
            string backwardTable = null;
            string linkTable = null;

            if (field?.BackwardField != null)
            {
                backwardTable = GetContentTable(field.BackwardField.ContentId, state);
            }

            if (field != null && field.M2mIsBackward.HasValue && field.M2mRelationId.HasValue)
            {
                linkTable = GetLinkTable(field.M2mRelationId.Value, state, field.M2mIsBackward.Value);
            }

            return new QpFieldRelationFilterClause
            {
                FilterDefinition = clause.FilterDefinition,
                Value = clause.Value,
                BackwardTable = backwardTable,
                LinkTable = linkTable,
                BackwardField = field?.BackwardField?.Alias,
                TableAlias = rootContext.TableALias
            };
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

        protected static string GetContentTable(QpArticleState state, RootContext rootContext, string on = null)
        {
            var table = GetContentTable(rootContext.ContentId, state);
            var result = new StringBuilder($"{table} {rootContext.TableALias}{on}");

                foreach (var item in rootContext.Extensions)
                {
                    var extensionTable = GetContentTable(item.ContentId, state);
                    result.Append($" left join {extensionTable} {item.TableALias} on {rootContext.TableALias}.{QpSystemFieldsDescriptor.Id.DBName} = {item.TableALias}.{item.ReferenceToBase}");
                }

            return result.ToString();
        }

        private string GetContentFields(RootContext rootContext)
        {
            var result = new StringBuilder();

            foreach (var f in QpSystemFieldsDescriptor.SystemDBFields)
            {
                var df = AddDelimiter(f);
                result.Append($" {rootContext.TableALias}.{df} {df},");
            }

            if (rootContext.Classifier != null)
            {
                result.Append($" {rootContext.TableALias}.{AddDelimiter(rootContext.Classifier.Alias)} {AddDelimiter(rootContext.Classifier.QueryAlias)},");
            }

            foreach (var f in rootContext.Fields)
            {
                result.Append($" {rootContext.TableALias}.{AddDelimiter(f.Alias)} {AddDelimiter(f.QueryAlias)},");
            }

            foreach (var item in rootContext.Extensions)
            {
                foreach (var f in item.Fields)
                {
                    result.Append($" {item.TableALias}.{AddDelimiter(f.Alias)} {AddDelimiter(f.QueryAlias)},");
                }
            }

            result.Remove(result.Length - 1, 1);
            return result.ToString();
        }
    }
}
