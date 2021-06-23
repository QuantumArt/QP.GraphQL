using QP.GraphQL.Interfaces.Metadata;
using System;
using System.Collections.Generic;

namespace QP.GraphQL.DAL
{
    internal class QpContentAttributeMetadataInternal
    {
        public int Id { get; set; }
        public int ContentId { get; set; }
        public int SiteId { get; set; }
        public string FriendlyName { get; set; }
        public string Alias { get; set; }
        public string TypeName { get; set; }
        public int Indexed { get; set; }

        public int? RelatedO2mContentId { get; set; }
        public int? RelatedM2mContentId { get; set; }
        public bool? M2mIsBackward { get; set; }
        public int? M2mRelationId { get; set; }
        public int? RelatedM2oContentId { get; set; }
        public string RelatedM2oBackwardField { get; set; }

        public string ContentFriendlyName { get; set; }
        public string ContentAliasSingular { get; set; }
        public string ContentAliasPlural { get; set; }
        public string ContentDescription { get; set; }
        public string SubFolder { get; set; }
        public bool UseSiteLibrary { get; set; }
        public int? SourceAttributeId { get; set; }
        
        public QpContentMetadata ToContentMetadata()
        {
            return new QpContentMetadata
            {
                Id = ContentId,
                FriendlyName = String.IsNullOrWhiteSpace(ContentFriendlyName) ? $"Контент {ContentId}" : ContentFriendlyName,
                AliasSingular = String.IsNullOrWhiteSpace(ContentAliasSingular) ? $"Content{ContentId}" : ContentAliasSingular,
                AliasPlural = String.IsNullOrWhiteSpace(ContentAliasPlural) ? $"Contents{ContentId}" : ContentAliasPlural,
                Description = ContentDescription,
                Attributes = new List<QpContentAttributeMetadata>()
            };
        }

        public QpContentAttributeMetadata ToContentAttributeMetadata()
        {
            return new QpContentAttributeMetadata
            {
                Id = this.Id,
                ContentId = this.ContentId,
                FriendlyName = String.IsNullOrWhiteSpace(this.FriendlyName) ? $"Поле {Alias}" : this.FriendlyName,
                Alias = this.Alias,
                TypeName = this.TypeName,
                Indexed = this.Indexed == 1,
                RelatedO2mContentId = this.RelatedO2mContentId,
                RelatedM2mContentId = this.RelatedM2mContentId,
                M2mIsBackward = this.M2mIsBackward,
                M2mRelationId = this.M2mRelationId,
                RelatedM2oContentId = this.RelatedM2oContentId,
                RelatedM2oBackwardField = this.RelatedM2oBackwardField,
                SubFolder = this.SubFolder,
                UseSiteLibrary = this.UseSiteLibrary,
                SourceAttributeId = this.SourceAttributeId
            };
        }
    }
}
