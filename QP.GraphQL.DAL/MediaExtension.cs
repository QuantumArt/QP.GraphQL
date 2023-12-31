﻿using QP.GraphQL.Interfaces.Metadata;
using System;
using System.Linq;
using System.Text;

namespace QP.GraphQL.DAL
{
    public static class MediaExtension
    {
        private const string UploadPlaceholder = "<%=upload_url%>";
        private const string SitePlaceholder = "<%=site_url%>";

        public static string ReplacePlaceholders(this QpSiteMetadata site, string input)
        {  
            string result = input;

            if (!string.IsNullOrEmpty(result) && site.ReplaceUrls)
            {
                result = result.Replace(UploadPlaceholder, site.UploadUrlPlaceholderValue);
                result = result.Replace(SitePlaceholder, site.SiteUrlPlaceholderValue);
            }

            return result;
        }

        public static string GetBaseUrl(this QpContentAttributeMetadata attribute, bool asShortAsPossible, bool removeSchema)
        {
            if (!new[] { "File", "Image", "Dynamic Image" }.Contains(attribute.TypeName))
            {
                throw new ArgumentException();
            }

            var baseUrl = attribute.UseSiteLibrary ? GetImagesUploadUrl(attribute.Content.Site, asShortAsPossible, removeSchema) : GetContentUploadUrlByID(attribute, asShortAsPossible, removeSchema);
            return CombineWithoutDoubleSlashes(baseUrl, GetFieldSubUrl(attribute));
        }

        private static string GetContentUploadUrlByID(QpContentAttributeMetadata attribute, bool asShortAsPossible, bool removeSchema)
        {
            var sb = new StringBuilder();
            sb.Append(GetUploadUrl(attribute.Content.Site, asShortAsPossible, removeSchema));
            if (sb[sb.Length - 1] != '/')
            {
                sb.Append("/");
            }

            sb.Append("contents/");
            sb.Append(attribute.ContentId);
            return sb.ToString();
        }

        private static string CombineWithoutDoubleSlashes(string first, string second)
        {
            if (string.IsNullOrEmpty(second))
            {
                return first;
            }

            var sb = new StringBuilder();
            sb.Append(first.Replace(@":/", @"://").Replace(@":///", @"://").TrimEnd('/'));
            sb.Append("/");
            sb.Append(second.Replace("//", "/").TrimStart('/'));

            return sb.ToString();
        }

        private static string GetFieldSubUrl(QpContentAttributeMetadata attribute) => GetFieldSubFolder(attribute, true);

        private static string GetFieldSubFolder(QpContentAttributeMetadata attribute, bool revertSlashes)
        {
            var result = attribute.SubFolder;
            if (!string.IsNullOrEmpty(result))
            {
                result = @"\" + result;
                if (revertSlashes)
                {
                    result = result.Replace(@"\", @"/");
                }
            }

            return result;
        }

        public static string GetImagesUploadUrl(this QpSiteMetadata site, bool asShortAsPossible, bool removeSchema) => GetUploadUrl(site, asShortAsPossible, removeSchema) + "images";

        private static string GetUploadUrl(QpSiteMetadata site, bool asShortAsPossible, bool removeSchema)
        {
            var sb = new StringBuilder();
            var prefix = GetUploadUrlPrefix(site);
            if (!string.IsNullOrEmpty(prefix))
            {
                if (removeSchema)
                {
                    prefix = ConvertUrlToSchemaInvariant(prefix);
                }

                sb.Append(prefix);
            }
            else
            {
                if (!asShortAsPossible)
                {
                    sb.Append(!removeSchema ? "http://" : "//");

                    sb.Append(GetDns(site, true));
                }
            }

            sb.Append(site.UploadUrl);

            return sb.ToString();
        }

        private static string ConvertUrlToSchemaInvariant(string prefix)
        {
            if (prefix.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase))
            {
                return "//" + prefix.Substring(7);
            }

            return prefix;
        }

        private static string GetUploadUrlPrefix(QpSiteMetadata site)
        {
            return site.UseAbsoluteUploadUrl ? site.UploadUrlPrefix : string.Empty;
        }

        private static string GetDns(QpSiteMetadata site, bool isLive)
        {
            return isLive || string.IsNullOrEmpty(site.StageDns) ? site.Dns : site.StageDns;
        }

        public static string GetSiteUrl(this QpSiteMetadata site)
        {
            var sb = new StringBuilder();
            sb.Append("http://");
            sb.Append(GetDns(site, site.IsLive));
            sb.Append(GetSiteUrlRel(site));
            return sb.ToString();
        }

        public static string GetSiteUrlRel(this QpSiteMetadata site)
        {
            return site.IsLive ? site.LiveVirtualRoot : site.StageVirtualRoot;
        }
    }
}
