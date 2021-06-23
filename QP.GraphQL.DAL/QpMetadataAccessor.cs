﻿using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QP.GraphQL.Interfaces.Metadata;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace QP.GraphQL.DAL
{
    public class QpMetadataAccessor : IQpMetadataAccessor
    {
        public QpMetadataAccessor(DbConnection connection, IOptions<QpMetadataSettings> options, ILogger<QpMetadataAccessor> logger)
        {
            Connection = connection;
            Settings = options.Value;
            Logger = logger;
        }
        
        public DbConnection Connection { get; }
        protected QpMetadataSettings Settings { get; }
        protected ILogger<QpMetadataAccessor> Logger { get; }

        public IDictionary<int, QpContentMetadata> GetContentsMetadata()
        {
            if (Connection.State != ConnectionState.Open)
                Connection.Open();

            var query = $@"
                select ca.attribute_id as Id,
	                ca.content_id as ContentId,
	                ca.friendly_name as FriendlyName,
	                ca.attribute_name as Alias,
	                at.type_name as TypeName,
                    ca.index_flag as Indexed,
	                ca.link_id as M2mRelationId,
					case   
                        when ctc.l_content_id = c.CONTENT_ID then ctc.r_content_id
                        when ctc.r_content_id = c.CONTENT_ID then ctc.l_content_id
                    end as RelatedM2mContentId,
                    case   
                        when ctc.l_content_id = c.CONTENT_ID then 0
                        when ctc.r_content_id = c.CONTENT_ID then 1
                    end as M2mIsBackward,
	                rca.content_id as RelatedO2mContentId,
                    bca.content_id as RelatedM2oContentId,
                    bca.attribute_name as RelatedM2oBackwardField,
                    c.site_id as SiteId,
	                c.content_name as ContentFriendlyName,
	                c.net_content_name as ContentAliasSingular,
	                c.net_plural_content_name as ContentAliasPlural,
	                c.description as ContentDescription,
                    ca.subfolder as SubFolder,
                    ca.use_site_library as UseSiteLibrary,
                    ca.persistent_attr_id as SourceAttributeId
                from content_attribute ca
                join content c on c.content_id = ca.content_id
                join attribute_type at on at.attribute_type_id = ca.attribute_type_id
                left join content_to_content ctc on ctc.link_id = ca.link_id
                left join content_attribute rca on rca.attribute_id = ca.related_attribute_id
                left join content_attribute bca on bca.attribute_id = ca.back_related_attribute_id
                where c.content_id in ({(Settings.ContentIds == null || !Settings.ContentIds.Any() ? "select content_id from content" : String.Join(",", Settings.ContentIds))})
                ";

            var contentAttributesRaw = Connection.Query<QpContentAttributeMetadataInternal>(query).ToList();

            var result = new Dictionary<int, QpContentMetadata>();
            var siteMap = GetSitesMetadata();


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
                    content.Site = siteMap[contentAttributeRaw.SiteId];
                    result[contentAttributeRaw.ContentId] = content;
                }

                var attribute = contentAttributeRaw.ToContentAttributeMetadata();
                attribute.Content = content;
                content.Attributes.Add(attribute);
            }
            return result;
        }

        private IDictionary<int, QpSiteMetadata> GetSitesMetadata()
        {
            if (Connection.State != ConnectionState.Open)
                Connection.Open();

            var query = $@"
                select
                    site_id as Id,
                    upload_url_prefix as UploadUrlPrefix,
                    upload_url as UploadUrl,
                    use_absolute_upload_url as UseAbsoluteUploadUrl,
                    dns as Dns,
                    stage_dns as StageDns
                from site";

            return Connection.Query<QpSiteMetadataInternal>(query).ToDictionary(s => s.Id, s => s.ToSiteMetadata());
        }
    }
}
