using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using QP8.Plugins.Contract;
using System.Collections.Generic;

namespace QP.GraphQL.App.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class QpContractController : ControllerBase
    {
        private readonly QpPluginSettings pluginSettings;

        public QpContractController(IOptions<QpPluginSettings> pluginOpts)
        {
            pluginSettings = pluginOpts.Value;
        }

        [HttpGet]
        public QpPluginContract Index()
        {
            return new QpPluginContract
            { 
                Code = "graphql",
                Description = "GraphQL Headless API",
                Version = pluginSettings.ContractVersion,
                InstanceKey = pluginSettings.InstanceKey,
                AllowMultipleInstances = true,
                Fields = new List<QpPluginField>
                { 
                    new QpPluginField
                    {
                        Name = "ApiKey",
                        Description = "Ключ доступа для GraphQL API",
                        ValueType = QpPluginValueType.String,
                        RelationType = QpPluginRelationType.Site,
                        SortOrder = 0,
                        //Required = true
                    },
                    new QpPluginField
                    {
                        Name = "MaxDepth",
                        Description = "Максимальная глубина запроса",
                        ValueType = QpPluginValueType.Numeric,
                        RelationType = QpPluginRelationType.Site,
                        SortOrder = 5
                    },
                    new QpPluginField
                    {
                        Name = "MaxComplexity",
                        Description = "Максимальный коэффициент сложности документа",
                        ValueType = QpPluginValueType.Numeric,
                        RelationType = QpPluginRelationType.Site,
                        SortOrder = 10
                    },
                    new QpPluginField
                    {
                        Name = "FieldImpact",
                        Description = "Максимальное число объектов, возвращаемых каждым полем",
                        ValueType = QpPluginValueType.Numeric,
                        RelationType = QpPluginRelationType.Site,
                        SortOrder = 15
                    },
                    new QpPluginField
                    {
                        Name = "MaxRecursionCount",
                        Description = "Максимальное количество итераций для обхода узлов дерева",
                        ValueType = QpPluginValueType.Numeric,
                        RelationType = QpPluginRelationType.Site,
                        SortOrder = 20
                    },
                    new QpPluginField
                    {
                        Name = "IsExposed",
                        Description = "Доступность контента в GraphQL API",
                        ValueType = QpPluginValueType.Bool,
                        RelationType = QpPluginRelationType.Content,
                        SortOrder = 0
                    },
                    new QpPluginField
                    {
                        Name = "AliasSingular",
                        Description = "Алиас в ед. числе в схеме GraphQL",
                        ValueType = QpPluginValueType.String,
                        RelationType = QpPluginRelationType.Content,
                        SortOrder = 5
                    },
                    new QpPluginField
                    {
                        Name = "AliasPlural",
                        Description = "Алиас в множ. числе в схеме GraphQL",
                        ValueType = QpPluginValueType.String,
                        RelationType = QpPluginRelationType.Content,
                        SortOrder = 10
                    },
                    new QpPluginField
                    {
                        Name = "IsHidden",
                        Description = "Недоступность поля в GraphQL API",
                        ValueType = QpPluginValueType.Bool,
                        RelationType = QpPluginRelationType.ContentAttribute,
                        SortOrder = 0
                    }
                }
            };
        }
    }
}
