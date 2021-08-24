﻿using QP.GraphQL.DAL;
using QP.GraphQL.Interfaces.Articles;
using System.Collections.Generic;
using System.Linq;

namespace QP.GraphQL.Interfaces.Metadata
{
    public class QpContentMetadata
    {
        public int Id { get; set; }
        public QpSiteMetadata Site { get; set; }
        public string FriendlyName { get; set; }
        public string AliasSingular { get; set; }
        public string AliasPlural { get; set; }
        public string Description { get; set; }
        public IList<QpContentAttributeMetadata> Attributes { get; set; }
        public bool HasExtensions => Attributes.Any(a => a.IsClassifier);
        public bool IsExtension => Attributes.Any(a => a.ClassifierAttributeId.HasValue);
        public IList<QpContentMetadata> Extensions { get; set; }

        public RootContext Context =>
            new RootContext
            {
                ContetnId = Id,                
                Fields = Attributes.Where(a => !a.IsClassifier).Select(a => new FieldContext
                {
                    ContetnId = Id,
                    Alias = a.Alias,
                }).ToArray(),
                Classifier = Attributes.Where(a => a.IsClassifier).Select(a => new FieldContext
                {
                    ContetnId = Id,
                    Alias = a.Alias,
                }).FirstOrDefault(),
                Extensions = Extensions.Select(e => new ExtensionContext {
                    ContetnId = e.Id,
                    ReferenceToBase = e.Attributes.First(a => a.ClassifierAttributeId.HasValue).Alias,
                    Fields = e.Attributes.Where(a => !a.ClassifierAttributeId.HasValue).Select(a => new FieldContext
                    {
                        ContetnId = e.Id,
                        Alias = a.Alias,
                    }).ToArray()
                }).ToArray(),
            };
    }
}
