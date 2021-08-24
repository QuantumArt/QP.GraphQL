namespace QP.GraphQL.Interfaces.Articles.Paging
{
    public class RelayPaginationArgs
    {
        public int? Skip { get; set; }
        public int? First { get; set; }
        public string After { get; set; }
        public int? Last { get; set; }
        public string Before { get; set; }

    }
}
