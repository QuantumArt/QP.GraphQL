using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QP.GraphQL.Interfaces.DAL;
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
        public QpMetadataAccessor(DbConnection connection, IOptions<QpMetadataSettings> options, IQueryService queryService, ILogger<QpMetadataAccessor> logger)
        {
            Connection = connection;
            Settings = options.Value;
            QueryService = queryService;
            Logger = logger;
        }
        
        public DbConnection Connection { get; }
        protected QpMetadataSettings Settings { get; }
        protected IQueryService QueryService { get; private set; }
        protected ILogger<QpMetadataAccessor> Logger { get; }

        public IDictionary<int, QpContentMetadata> GetContentsMetadata()
        {
            if (Connection.State != ConnectionState.Open)
                Connection.Open();

            var filterContents = Settings.ContentIds != null && Settings.ContentIds.Any();

            var query = $@"
                select ca.attribute_id as Id,
                    s.site_id as SiteId,
                    s.upload_url_prefix as UploadUrlPrefix,
                    s.upload_url as UploadUrl,
                    s.use_absolute_upload_url as UseAbsoluteUploadUrl,
                    s.dns as Dns,
                    s.stage_dns as StageDns,
                    s.replace_urls as ReplaceUrls,
                    s.live_virtual_root as LiveVirtualRoot,
                    s.stage_virtual_root as StageVirtualRoot,
                    s.is_live as IsLive,
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
					ca.CLASSIFIER_ATTRIBUTE_ID  as ClassifierAttributeId,
					ca.IS_CLASSIFIER as IsClassifier,
	                c.content_name as ContentFriendlyName,
	                c.net_content_name as ContentAliasSingular,
	                c.net_plural_content_name as ContentAliasPlural,
	                c.description as ContentDescription,
                    ca.subfolder as SubFolder,
                    ca.use_site_library as UseSiteLibrary,
                    ca.persistent_attr_id as SourceAttributeId
                from content_attribute ca 
                join content c on c.content_id = ca.content_id
                join site s on c.site_id = s.site_id
                join attribute_type at on at.attribute_type_id = ca.attribute_type_id
                left join content_to_content ctc on ctc.link_id = ca.link_id
                left join content_attribute rca on rca.attribute_id = ca.related_attribute_id
                left join content_attribute bca on bca.attribute_id = ca.back_related_attribute_id
                where c.content_id in ({(filterContents ? "select id from " + QueryService.GetIdTable("@contentIds") : "select content_id from content")})
                ";

            var command = Connection.CreateCommand();
            command.CommandText = query;
            command.CommandType = CommandType.Text;
            
            if (filterContents)
            {
                command.Parameters.Add(QueryService.GetIdParam("@contentIds", Settings.ContentIds));
            }

            var metadataItems = command.ExecuteReader().Parse<QpMetadataItemInternal>();
            var siteMap = new Dictionary<int, QpSiteMetadata>();
            var contentMap = new Dictionary<int, QpContentMetadata>();

            foreach (var metadataItem in metadataItems)
            {
                QpSiteMetadata site;
                if (siteMap.ContainsKey(metadataItem.SiteId))
                {
                    site = siteMap[metadataItem.SiteId];
                }
                else
                {
                    site = metadataItem.ToSiteMetadata(true, false, false);
                    siteMap[metadataItem.SiteId] = site;
                }

                QpContentMetadata content;
                if (contentMap.ContainsKey(metadataItem.ContentId))
                {
                    content = contentMap[metadataItem.ContentId];
                }
                else
                {
                    content = metadataItem.ToContentMetadata();
                    content.Site = site;
                    contentMap[metadataItem.ContentId] = content;
                }

                var attribute = metadataItem.ToContentAttributeMetadata();
                attribute.Content = content;
                content.Attributes.Add(attribute);
            }

            foreach(var id in contentMap.Keys)
            {
                var content = contentMap[id];

                if (!content.HasExtensions)
                {
                    var baseRef = content.Attributes.FirstOrDefault(a => a.ClassifierAttributeId.HasValue);

                    if (baseRef != null)
                    {
                        var baseContentId = baseRef.RelatedO2mContentId.Value;
                        var baseContent = contentMap[baseContentId];
                        var baseClassifier = baseContent.Attributes.First(a => a.IsClassifier);

                        if (baseRef.ClassifierAttributeId.Value == baseClassifier.Id)
                        {
                            baseContent.Extensions.Add(content);
                        }

                        contentMap.Remove(id);
                    }
                }
            }

            return contentMap;
        }
    }
}
