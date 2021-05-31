using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using QP.GraphQL.Interfaces.Metadata;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace QP.GraphQL.DAL.Postgresql
{
    public class QpMetadataAccessor : IQpMetadataAccessor
    {
        public QpMetadataAccessor(NpgsqlConnection connection, ILogger<QpMetadataAccessor> logger)
        {
            Connection = connection;
            Logger = logger;
        }
        
        public NpgsqlConnection Connection { get; }
        protected ILogger<QpMetadataAccessor> Logger { get; private set; }

        public async Task<IDictionary<int, QpContentMetadata>> GetContentsMetadata(IEnumerable<int> contentIds)
        {
            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync();

            var query = $@"
                select ca.attribute_id as Id,
	                ca.content_id as ContentId,
	                ca.friendly_name as FriendlyName,
	                ca.attribute_name as Alias,
	                at.type_name as TypeName,
                    ca.index_flag as Indexed,
	                ca.link_id as M2mRelationId,
	                ctc.r_content_id as RelatedM2mContentId,
	                rca.content_id as RelatedO2mContentId,
	                c.content_name as ContentFriendlyName,
	                c.net_content_name as ContentAliasSingular,
	                c.net_plural_content_name as ContentAliasPlural,
	                c.description as ContentDescription
                from content_attribute ca
                join content c on c.content_id = ca.content_id
                join attribute_type at on at.attribute_type_id = ca.attribute_type_id
                left join content_to_content ctc on ctc.link_id = ca.link_id
                left join content_attribute rca on rca.attribute_id = ca.related_attribute_id
                where c.content_id in ({(contentIds == null || !contentIds.Any() ? "select content_id from content" : String.Join(",", contentIds))})
                ";

            var contentAttributesRaw = Connection.Query<QpContentAttributeMetadataInternal>(query).ToList();
            Logger.LogInformation("Make query {db_query}", query);


            var result = new Dictionary<int, QpContentMetadata>();
            foreach (var contentAttributeRaw in contentAttributesRaw)
            {
                QpContentMetadata content;
                if (result.ContainsKey(contentAttributeRaw.ContentId))
                {
                    content = result[contentAttributeRaw.ContentId];
                }
                else
                {
                    content = contentAttributeRaw.ToContentMetadata();
                    result[contentAttributeRaw.ContentId] = content;
                }

                content.Attributes.Add(contentAttributeRaw.ToContentAttributeMetadata());
            }
            return result;
        }
    }
}
