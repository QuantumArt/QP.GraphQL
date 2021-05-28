using System;
using System.Collections.Generic;
using System.Text;

namespace QP.GraphQL.Interfaces.Articles.Paging
{
    public class RelayPaginationResult
    {
        public int? TotalCount { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public IList<QpArticle> Articles { get; set; }
    }
}
