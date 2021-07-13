using GraphQL.Utilities;
using Microsoft.Extensions.Logging;
using QP.GraphQL.DAL;
using QP.GraphQL.Interfaces.Metadata;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace QP.GraphQL.App.Schema
{
    public class QpMetadataValidator : IQpMetadataValidator
    {
        private readonly Regex SymbolsToReplace = new Regex("[^_a-zA-Z0-9]+", RegexOptions.Compiled);

        private readonly ILogger<QpMetadataValidator> _logger;

        public QpMetadataValidator(ILogger<QpMetadataValidator> logger)
        {
            _logger = logger;
        }


        public IDictionary<int, QpContentMetadata> ValidateFields(IDictionary<int, QpContentMetadata> metadata)
        {
            var validatedMethadata = new Dictionary<int, QpContentMetadata>();
            var contentAliasCounter = new ConcurrentDictionary<string, int>();
            var contentPluralAliasCounter = new ConcurrentDictionary<string, int>();

            foreach (var content in metadata.Values)
            {
                var attributes = new List<QpContentAttributeMetadata>();
                var fieldAliasCounter = new ConcurrentDictionary<string, int>();

                content.AliasSingular = SymbolsToReplace.Replace(content.AliasSingular, "_");
                content.AliasPlural = SymbolsToReplace.Replace(content.AliasPlural, "_");

                var singularPosition = contentAliasCounter.AddOrUpdate(content.AliasSingular, 1, (_, current) => current + 1);
                var pluralPosition = contentPluralAliasCounter.AddOrUpdate(content.AliasPlural, 1, (_, current) => current + 1);

                if (singularPosition > 1)
                {
                    content.AliasSingular = $"{content.AliasSingular}_{singularPosition}";
                    contentAliasCounter.AddOrUpdate(content.AliasSingular, 1, (_, current) => current + 1);
                }

                if (pluralPosition > 1)
                {
                    content.AliasPlural = $"{content.AliasPlural}_{pluralPosition}";
                    contentPluralAliasCounter.AddOrUpdate(content.AliasPlural, 1, (_, current) => current + 1);
                }

                try
                {
                    NameValidator.ValidateName(content.AliasSingular, NamedElement.Type);
                    NameValidator.ValidateName(content.AliasPlural, NamedElement.Type);

                    foreach (var a in content.Attributes)
                    {
                        try
                        {
                            a.SchemaAlias = a.Alias;

                            if (QpSystemFieldsDescripotor.Id.Equals(a.Alias, StringComparison.InvariantCultureIgnoreCase))
                            {
                                a.SchemaAlias = $"{a.SchemaAlias}Field";
                            }
                            else
                            {
                                a.SchemaAlias = SymbolsToReplace.Replace(a.SchemaAlias, "_");
                            }

                            var position = fieldAliasCounter.AddOrUpdate(a.SchemaAlias, 1, (_, current) => current + 1);

                            if (position > 1)
                            {
                                a.SchemaAlias = $"{a.SchemaAlias}_{position}";
                                fieldAliasCounter.AddOrUpdate(a.SchemaAlias, 1, (_, current) => current + 1);
                            }

                            NameValidator.ValidateName(a.SchemaAlias, NamedElement.Field);
                            attributes.Add(a);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("field {field} not pass: {validationerror}", new { a.Id, a.SchemaAlias, a.ContentId }, ex.Message);
                        }
                    }

                    content.Attributes = attributes;
                    validatedMethadata[content.Id] = content;

                }
                catch(Exception ex)
                {
                    _logger.LogWarning("content {content} not pass: {validationerror}", content.Id, ex.Message);
                }
            }

            return validatedMethadata;
        }
    }
}
