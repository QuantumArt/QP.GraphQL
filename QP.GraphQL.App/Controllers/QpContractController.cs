using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using QP.GraphQL.App.QpContract;

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
                Version = pluginSettings.Version,
                InstanceKey = pluginSettings.InstanceKey,
                AllowMultipleInstances = true,
                Fields = new QpPluginField[5]
                { 
                    new QpPluginField
                    {
                        Name = "ApiKey",
                        Description = "Ключ доступа для GraphQL API",
                        ValueType = QpPluginFieldValueType.String,
                        RelationType = QpPluginFieldRelationType.Site,
                        SortOrder = 0,
                        Required = true
                    },
                    new QpPluginField
                    {
                        Name = "IsExposed",
                        Description = "Доступность контента в GraphQL API",
                        ValueType = QpPluginFieldValueType.Bool,
                        RelationType = QpPluginFieldRelationType.Content,
                        SortOrder = 0
                    },
                    new QpPluginField
                    {
                        Name = "AliasSingular",
                        Description = "Алиас в ед. числе в схеме GraphQL",
                        ValueType = QpPluginFieldValueType.String,
                        RelationType = QpPluginFieldRelationType.Content,
                        SortOrder = 5
                    },
                    new QpPluginField
                    {
                        Name = "AliasPlural",
                        Description = "Алиас в множ. числе в схеме GraphQL",
                        ValueType = QpPluginFieldValueType.String,
                        RelationType = QpPluginFieldRelationType.Content,
                        SortOrder = 10
                    },
                    new QpPluginField
                    {
                        Name = "IsHidden",
                        Description = "Недоступность поля в GraphQL API",
                        ValueType = QpPluginFieldValueType.Bool,
                        RelationType = QpPluginFieldRelationType.ContentAttribute,
                        SortOrder = 0
                    }
                }
            };
        }
    }
}
