﻿using QP.GraphQL.Interfaces.Metadata;
using System;
using System.Linq;
using System.Text;

namespace QP.GraphQL.DAL
{
    public static class MediaExtension
    {
        public static string GetBaseUrl(this QpContentAttributeMetadata attribute, bool asShortAsPossible, bool removeSchema)
        {
            if (!new[] { "File", "Image", "Dynamic Image" }.Contains(attribute.TypeName))
            {
                throw new ArgumentException();
            }

            var baseUrl = attribute.UseSiteLibrary ? GetImagesUploadUrl(attribute, asShortAsPossible, removeSchema) : GetContentUploadUrlByID(attribute, asShortAsPossible, removeSchema);
            return CombineWithoutDoubleSlashes(baseUrl, GetFieldSubUrl(attribute));
        }

        private static string GetContentUploadUrlByID(QpContentAttributeMetadata attribute, bool asShortAsPossible, bool removeSchema)
        {
            var sb = new StringBuilder();
            sb.Append(GetUploadUrl(attribute, asShortAsPossible, removeSchema));
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

        private static string GetImagesUploadUrl(QpContentAttributeMetadata attribute, bool asShortAsPossible, bool removeSchema) => GetUploadUrl(attribute, asShortAsPossible, removeSchema) + "images";

        private static string GetUploadUrl(QpContentAttributeMetadata attribute, bool asShortAsPossible, bool removeSchema)
        {
            var sb = new StringBuilder();
            var prefix = GetUploadUrlPrefix(attribute);
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

                    sb.Append(GetDns(attribute, true));
                }
            }

            sb.Append(attribute.Content.Site.UploadUrl);

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

        private static string GetUploadUrlPrefix(QpContentAttributeMetadata attribute)
        {
            return attribute.Content.Site.UseAbsoluteUploadUrl ? attribute.Content.Site.UploadUrlPrefix : string.Empty;
        }

        private static string GetDns(QpContentAttributeMetadata attribute, bool isLive)
        {
            return isLive || string.IsNullOrEmpty(attribute.Content.Site.StageDns) ? attribute.Content.Site.Dns : attribute.Content.Site.StageDns;
        }
    }
}
