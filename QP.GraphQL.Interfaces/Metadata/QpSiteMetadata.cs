namespace QP.GraphQL.Interfaces.Metadata
{
    public class QpSiteMetadata
    {
        public int Id { get; set; }
        public string UploadUrlPrefix { get; set; }
        public string UploadUrl { get; set; }
        public bool UseAbsoluteUploadUrl { get; set; }
        public string Dns { get; set; }
        public string StageDns { get; set; }
        public bool ReplaceUrls { get; set; }
    }
}
