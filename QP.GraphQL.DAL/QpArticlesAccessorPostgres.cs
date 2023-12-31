﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QP.GraphQL.Interfaces.Articles;
using QP.GraphQL.Interfaces.DAL;
using System.Collections.Generic;
using System.Data.Common;

namespace QP.GraphQL.DAL
{
    public class QpArticlesAccessorPostgres : QpArticlesAccessorBase
    {
        public QpArticlesAccessorPostgres(DbConnection connection, IQueryService queryService, IOptions<QpArticlesAccessorSettings> options, ILogger<QpArticlesAccessorPostgres> logger)
            : base(connection, queryService, options, logger)
        {

        }

        protected override string AddDelimiter(string identifier) => $"\"{identifier.ToLowerInvariant()}\"";

        protected override string BuildIdsFieldClause(int linkId, QpArticleState state, bool isBackward)
        {
            return "array_to_string(array_agg(id), ',')";
        }

        protected override string BuildLimitClause(RootContext context, string fields, string whereClause, string pagingWhereClause, IList<string> orderBy, int count, bool reverse, QpArticleState state)
        {
            return $"select {fields} from {GetContentTable(state, context)} where {whereClause} and {pagingWhereClause} order by {BuildOrderbyClause(orderBy, reverse, context)} limit {count}";
        }

        protected override string BuildTakeSkipClause(RootContext context, string fields, string whereClause, IList<string> orderBy, int take, int skip, QpArticleState state)
        {
            var query = base.BuildTakeSkipClause(context, fields, whereClause, orderBy, take, skip, state);

            query = $"{query} limit {take} offset {skip}";

            return query;
        }
    }
}
