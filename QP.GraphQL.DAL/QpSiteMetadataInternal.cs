using QP.GraphQL.Interfaces.Metadata;

namespace QP.GraphQL.DAL
{
    internal class QpSiteMetadataInternal
    {
        public int Id { get; set; }
        public string UploadUrlPrefix { get; set; }
        public string UploadUrl { get; set; }
        public bool UseAbsoluteUploadUrl { get; set; }
        public string Dns { get; set; }
        public string StageDns { get; set; }

        public QpSiteMetadata ToSiteMetadata()
        {
            return new QpSiteMetadata
            {
                Id = this.Id,
                UploadUrlPrefix = this.UploadUrlPrefix,
                UploadUrl = this.UploadUrl,
                UseAbsoluteUploadUrl = this.UseAbsoluteUploadUrl,
                Dns = this.Dns,
                StageDns = this.StageDns
            };
        }
    }
}
