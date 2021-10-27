using Dapper;
using Microsoft.Extensions.Logging;
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
        public QpMetadataAccessor(DbConnection connection, IQueryService queryService, ILogger<QpMetadataAccessor> logger)
        {
            Connection = connection;
            QueryService = queryService;
            Logger = logger;
        }
        
        public DbConnection Connection { get; }
        protected IQueryService QueryService { get; private set; }
        protected ILogger<QpMetadataAccessor> Logger { get; }

        public IDictionary<int, QpContentMetadata> GetContentsMetadata(QpPluginMetadata plugin)
        {
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
				    pc.aliassingular as ContentAliasSingular,
				    pc.aliasplural as ContentAliasPlural,
	                c.description as ContentDescription,
                    ca.subfolder as SubFolder,
                    ca.use_site_library as UseSiteLibrary,
                    ca.persistent_attr_id as SourceAttributeId
                from content_attribute ca 
                join content c on c.content_id = ca.content_id			
			    join plugin_content_{plugin.Id} pc on pc.id = c.content_id						
                join site s on c.site_id = s.site_id
                join attribute_type at on at.attribute_type_id = ca.attribute_type_id
			    left join plugin_content_attribute_{plugin.Id} as pca on pca.id = ca.attribute_id
                left join content_to_content ctc on ctc.link_id = ca.link_id
                left join content_attribute rca on rca.attribute_id = ca.related_attribute_id
                left join content_attribute bca on bca.attribute_id = ca.back_related_attribute_id
			    where pc.isexposed = {QueryService.AsBool(true)} and (pca.ishidden is null or pca.ishidden = {QueryService.AsBool(false)})";

            try
            {
                if (Connection.State != ConnectionState.Open)
                    Connection.Open();

                var command = Connection.CreateCommand();
                command.CommandText = query;
                command.CommandType = CommandType.Text;

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

                foreach (var id in contentMap.Keys)
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
                                var duplicates = content.Attributes.Where(a => baseContent.Attributes.Any(ba => ba.SchemaAlias.Equals(a.SchemaAlias, StringComparison.InvariantCultureIgnoreCase)));

                                foreach (var d in duplicates)
                                {
                                    d.SchemaAlias = $"{content.AliasSingular}_{d.SchemaAlias}";
                                }


                                baseContent.Extensions.Add(content);
                            }

                            contentMap.Remove(id);
                        }
                    }
                }

                return contentMap;
            }
            finally
            {
                Connection.Close();
            }

            
        }

        public QpPluginMetadata GetPluginMetadata(string instanceKey)
        {
            try
            {
                if (Connection.State != ConnectionState.Open)
                    Connection.Open();

                var instanceKeyParam = QueryService.GetParameter("instance_key", SqlDbType.NVarChar, instanceKey);
                var command = Connection.CreateCommand();
                command.CommandType = CommandType.Text;
                command.Parameters.Add(instanceKeyParam);
                command.CommandText = $@"
                select id, version
                from plugin
                where instance_key = {instanceKeyParam.ParameterName}";

                var metadata = command.ExecuteReader().Parse<QpPluginMetadata>().FirstOrDefault();
                return metadata;
            }
            finally
            {
                Connection.Close();
            }
        }

        public QpPluginSiteMetadata GetPluginSiteMetadata(QpPluginMetadata plugin)
        {
            try
            {
                if (Connection.State != ConnectionState.Open)
                    Connection.Open();

                var query = $"select apikey, maxdepth, maxcomplexity, fieldimpact, maxrecursioncount from plugin_site_{plugin.Id}";

                var command = Connection.CreateCommand();
                command.CommandText = query;
                command.CommandType = CommandType.Text;

                var metadata = command.ExecuteReader().Parse<QpPluginSiteMetadata>().FirstOrDefault();
                return metadata;
            }
            finally
            {
                Connection.Close();
            }
        }
    }
}
