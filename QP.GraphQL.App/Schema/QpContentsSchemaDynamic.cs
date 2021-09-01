using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Resolvers;
using GraphQL.Types;
using GraphQL.Types.Relay;
using GraphQL.Types.Relay.DataObjects;
using GraphQL.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QP.GraphQL.App.Types;
using QP.GraphQL.DAL;
using QP.GraphQL.Interfaces.Articles;
using QP.GraphQL.Interfaces.Articles.Filtering;
using QP.GraphQL.Interfaces.Articles.Paging;
using QP.GraphQL.Interfaces.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using GraphQLTypes = GraphQL.Types;

namespace QP.GraphQL.App.Schema
{
    public class QpContentsSchemaDynamic : GraphQLTypes.Schema
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        private readonly ILogger<QpContentsSchemaDynamic> _logger;        

        public QpContentsSchemaDynamic(IServiceProvider serviceProvider,
            IQpMetadataValidator validator,
            IDataLoaderContextAccessor dataLoaderAccessor,
            IQpMetadataAccessor metadataAccessor,
            IOptions<QpPluginSettings> options,
            ILogger<QpContentsSchemaDynamic> logger
            )
            : base(serviceProvider)
        {
            _logger = logger;
            _logger.LogInformation("Start create schema {schemaId}", Id);
            var settings = options.Value;
            var plugin = metadataAccessor.GetPluginMetadata(settings.InstanceKey);
            string error = null;

            if (plugin == null)
            {
                error = "Plugin is not installed";
            }
            else if (settings.ContractVersion != plugin?.Version)
            {
                error = "Plugin is not compatible";
            }
            
            Metadata["ERROR"] = error;

            if (error == null)
            {
                var apikey = metadataAccessor.GetApiKey(plugin);
                Metadata["APIKEY"] = apikey;

                var metadata = metadataAccessor.GetContentsMetadata(plugin);
                var graphTypes = new Dictionary<int, IComplexGraphType>();
                var graphListTypes = new Dictionary<int, ListGraphType>();
                var graphExtensionsTypes = new Dictionary<int, Dictionary<int, ObjectGraphType<QpArticle>>>();
                var connectionGraphTypes = new Dictionary<int, ObjectGraphType<Connection<QpArticle, Edge<QpArticle>>>>();
                var orderGraphTypes = new Dictionary<int, ListGraphType>();
                var filterGraphTypes = new Dictionary<int, InputObjectGraphType<object>>();
                var filterDefinitionsByContentTypes = new Dictionary<int, Dictionary<string, QpFieldFilterDefinition>>();

                //валидация полей
                metadata = validator.ValidateFields(metadata);

                //базовый интерфейс статьи с системными полями
                var articleInterface = new ArticleInterface();

                //типы и интерфейсы графа для статей и расширений
                foreach (var contentMeta in metadata.Values)
                {
                    ComplexGraphType<QpArticle> complexType = null;

                    if (contentMeta.HasExtensions)
                    {
                        var map = new Dictionary<int, ObjectGraphType<QpArticle>>();

                        var baseType = new ObjectGraphType<QpArticle>()
                        {
                            Name = contentMeta.AliasSingular,
                            Description = contentMeta.FriendlyName
                        };

                        map[contentMeta.Id] = baseType;

                        foreach (var extension in contentMeta.Extensions)
                        {
                            var extensionType = new ObjectGraphType<QpArticle>()
                            {
                                Name = extension.AliasSingular,
                                Description = extension.FriendlyName
                            };

                            map[extension.Id] = extensionType;
                        }

                        var interfaceType = new InterfaceGraphType<QpArticle>()
                        {
                            Name = contentMeta.AliasSingular + "Interface",
                            Description = contentMeta.FriendlyName,
                            ResolveType = obj =>
                            {
                                var article = (QpArticle)obj;

                                if (article.ExtensionContentId.HasValue && map.ContainsKey(article.ExtensionContentId.Value))
                                {
                                    return map[article.ExtensionContentId.Value];
                                }
                                else
                                {
                                    return baseType;
                                }
                            }

                        };

                        // TODO: make interface inplements interface
                        articleInterface.Fields.ToList().ForEach(f => interfaceType.AddField(f));

                        complexType = interfaceType;
                        graphTypes[contentMeta.Id] = interfaceType;
                        graphListTypes[contentMeta.Id] = new ListGraphType(interfaceType);
                        graphExtensionsTypes[contentMeta.Id] = map;
                    }
                    else if (!contentMeta.IsExtension)
                    {
                        var graphType = new ObjectGraphType<QpArticle>()
                        {
                            Name = contentMeta.AliasSingular,
                            Description = contentMeta.FriendlyName
                        };

                        ImplementInterface(graphType, articleInterface);
                        RegisterType(graphType);

                        complexType = graphType;
                        graphTypes[contentMeta.Id] = graphType;
                        graphListTypes[contentMeta.Id] = new ListGraphType(graphType);
                    }

                    if (complexType != null)
                    {

                        //создаём input-тип, который будет использоваться для задания сортировки по статьям этого типа
                        //я решил дать возможность сортировки только по Indexed-полям (если их не будет, то не будет и возможности сортировки)
                        if (contentMeta.Attributes.Any(ca => ca.Indexed))
                        {
                            var orderEnumType = new EnumerationGraphType
                            {
                                Name = $"PossibleOrderFor{complexType.Name}",
                                Description = $"Possible order by literals for content type {complexType.Name}"
                            };
                            foreach (var attribute in contentMeta.Attributes.Where(ca => ca.Indexed))
                            {
                                AddSortValue(orderEnumType, attribute.SchemaAlias, attribute.SchemaAlias);
                            }

                            AddSortValue(orderEnumType, QpSystemFieldsDescriptor.Id.Name, QpSystemFieldsDescriptor.Id.DBName);
                            AddSortValue(orderEnumType, QpSystemFieldsDescriptor.StatusTypeId.Name, QpSystemFieldsDescriptor.StatusTypeId.DBName);
                            AddSortValue(orderEnumType, QpSystemFieldsDescriptor.Created.Name, QpSystemFieldsDescriptor.Created.DBName);
                            AddSortValue(orderEnumType, QpSystemFieldsDescriptor.Modified.Name, QpSystemFieldsDescriptor.Modified.DBName);
                            AddSortValue(orderEnumType, QpSystemFieldsDescriptor.LastModifiedBy.Name, QpSystemFieldsDescriptor.LastModifiedBy.DBName);

                            orderGraphTypes[contentMeta.Id] = new ListGraphType(orderEnumType);
                        }

                        //создаём input-тип, который будет использоваться для задания фильтра по статьям этого типа
                        //также нам понадобится словарь, чтобы потом в резолверах расшифровать все эти фильтры, к каким полям они относятся и какую операцию в себе несут
                        var filterType = new InputObjectGraphType<object>()
                        {
                            Name = $"FilterFor{complexType.Name}",
                            Description = $"Filter object for content type {complexType.Name}"
                        };
                        var filterDefinitions = new Dictionary<string, QpFieldFilterDefinition>();

                        AddFiltersForSystemFields(filterType, filterDefinitions);

                        foreach (var attribute in contentMeta.Attributes.Where(ca => !ca.Alias.Contains(' ')))
                        {
                            switch (attribute.TypeName)
                            {
                                case "String":
                                case "Textbox":
                                case "VisualEdit":
                                    AddFiltersForStringField(filterType, filterDefinitions, attribute);
                                    break;
                                case "Numeric":
                                    AddFiltersForNumericField(filterType, filterDefinitions, attribute, typeof(DecimalGraphType));
                                    break;
                                case "Boolean":
                                    AddSimpleFiltersForField(filterType, filterDefinitions, attribute, typeof(BooleanGraphType));
                                    break;
                                case "Date":
                                    AddFiltersForNumericField(filterType, filterDefinitions, attribute, typeof(DateGraphType));
                                    break;
                                case "Time":
                                    AddFiltersForNumericField(filterType, filterDefinitions, attribute, typeof(TimeGraphType));
                                    break;
                                case "DateTime":
                                    AddFiltersForNumericField(filterType, filterDefinitions, attribute, typeof(DateTimeGraphType));
                                    break;
                                case "Relation":
                                    //TODO: сейчас будет работать только для O2M связей, нет фильтров ни по M2M, ни по M2O. также нет фильтров второго уровня (по какому-то полю из связи o2m)
                                    AddSimpleFiltersForField(filterType, filterDefinitions, attribute, typeof(IntGraphType));
                                    break;
                            }
                        }
                        filterGraphTypes[contentMeta.Id] = filterType;
                        filterDefinitionsByContentTypes[contentMeta.Id] = filterDefinitions;

                        //создаём connection-тип (https://relay.dev/graphql/connections.htm)
                        var connectionType = new ObjectGraphType<Connection<QpArticle, Edge<QpArticle>>>()
                        {
                            Name = $"{complexType.Name}Connection",
                            Description = $"A connection to a list of objects of type `{contentMeta.AliasSingular}`"
                        };

                        //для connection-типа нам понадобится edge-тип
                        var edgeType = new ObjectGraphType<Edge<QpArticle>>()
                        {
                            Name = $"{complexType.Name}Edge",
                            Description = $"Edge of a connection containing a node (a row of `{contentMeta.AliasSingular}`) and cursor"
                        };
                        edgeType.AddField(new FieldType
                        {
                            Name = "node",
                            Description = $"A single row of `{contentMeta.AliasSingular}` within the result data set.",
                            ResolvedType = complexType,
                            Resolver = new FuncFieldResolver<Edge<QpArticle>, QpArticle>(context => context.Source.Node)
                        });
                        edgeType.AddField(new FieldType
                        {
                            Name = "cursor",
                            Description = "The cursor of this edge's node. A cursor is a string representation of a unique identifier of this node.",
                            Type = typeof(StringGraphType),
                            Resolver = new FuncFieldResolver<Edge<QpArticle>, string>(context => context.Source.Node.Id.ToString())//в качестве курсора используем Id, подробнее см в QpDataAccessor
                        });
                        var edgeListType = new ListGraphType(edgeType);

                        //стандартные поля для connection-типа
                        connectionType.AddField(new FieldType
                        {
                            Name = "totalCount",
                            Description = "A count of the total number of objects in this connection, ignoring pagination.",
                            Type = typeof(IntGraphType),
                            Resolver = new FuncFieldResolver<Connection<QpArticle, Edge<QpArticle>>, object>(context => context.Source.TotalCount)
                        });
                        connectionType.AddField(new FieldType
                        {
                            Name = "pageInfo",
                            Description = "Cursor-based pagination details.",
                            Type = typeof(NonNullGraphType<PageInfoType>),
                            Resolver = new FuncFieldResolver<Connection<QpArticle, Edge<QpArticle>>, PageInfo>(context => context.Source.PageInfo)
                        });
                        connectionType.AddField(new FieldType
                        {
                            Name = "edges",
                            Description = "The result data set, stored as a list of edges containing a node (the data) and a cursor (a unique identifier for the data).",
                            ResolvedType = edgeListType,
                            Resolver = new FuncFieldResolver<Connection<QpArticle, Edge<QpArticle>>, List<Edge<QpArticle>>>(context => context.Source.Edges)
                        });
                        connectionType.AddField(new FieldType
                        {
                            Name = "items",
                            Description = "The result data set, stored as a list of edges containing a node (the data) and a cursor (a unique identifier for the data).",
                            ResolvedType = graphListTypes[contentMeta.Id],
                            Resolver = new FuncFieldResolver<Connection<QpArticle, Edge<QpArticle>>, List<QpArticle>>(context => context.Source.Items)
                        });
                        connectionGraphTypes[contentMeta.Id] = connectionType;
                    }
                }

                //создаём поля для каждого типа графа
                foreach (var contentId in metadata.Keys)
                {
                    var contentMeta = metadata[contentId];
                    var graphType = graphTypes[contentId];

                    if (!contentMeta.IsExtension)
                    {
                        foreach (var attribute in contentMeta.Attributes)
                        {
                            if (!attribute.IsClassifier)
                            {
                                var f = GetFieldType(attribute, dataLoaderAccessor, metadata, graphTypes, graphListTypes, orderGraphTypes, filterGraphTypes, filterDefinitionsByContentTypes);

                                if (f != null)
                                    graphType.AddField(f);
                            }
                        }
                    }

                    if (contentMeta.HasExtensions)
                    {
                        var graphExTypes = graphExtensionsTypes[contentMeta.Id];

                        foreach (var m in contentMeta.Extensions)
                        {
                            var exType = graphExTypes[m.Id];

                            foreach (var attribute in m.Attributes)
                            {
                                if (!attribute.ClassifierAttributeId.HasValue)
                                {
                                    var f = GetFieldType(attribute, dataLoaderAccessor, metadata, graphTypes, graphListTypes, orderGraphTypes, filterGraphTypes, filterDefinitionsByContentTypes);

                                    if (f != null)
                                        exType.AddField(f);
                                }
                            }
                        }
                    }
                }

                foreach (var contentMetaBase in metadata.Values.Where(m => m.HasExtensions))
                {
                    var interfaceType = graphTypes[contentMetaBase.Id] as IInterfaceGraphType;
                    var map = graphExtensionsTypes[contentMetaBase.Id];

                    var baseType = map[contentMetaBase.Id] as IObjectGraphType;
                    ImplementInterface(baseType, interfaceType);
                    baseType.AddResolvedInterface(articleInterface);


                    foreach (var contentMeta in contentMetaBase.Extensions)
                    {
                        var extensionType = map[contentMeta.Id] as IObjectGraphType;
                        ImplementInterface(extensionType, interfaceType);
                        extensionType.AddResolvedInterface(articleInterface);
                    }
                }

                //создаём рут
                var rootQuery = new ObjectGraphType<QpArticle>
                {
                    Name = "Query"
                };

                foreach (var contentId in metadata.Keys)
                {
                    var contentMeta = metadata[contentId];
                    rootQuery.AddField(new FieldType
                    {
                        Name = contentMeta.AliasSingular,
                        Description = contentMeta.FriendlyName + " по id",
                        ResolvedType = graphTypes[contentId],
                        Arguments = new QueryArguments(
                            new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "Id", Description = "id of the article" }
                        ),
                        Resolver = new FuncFieldResolver<QpArticle, IDataLoaderResult<QpArticle>>(context =>
                        {
                            var state = GetQpArticleState(context.UserContext);
                            var loader = dataLoaderAccessor.Context.GetOrAddBatchLoader<int, QpArticle>($"Batch_{contentId}",
                                                (ids) => context.RequestServices.GetRequiredService<IQpArticlesAccessor>()
                                                            .GetArticlesByIdList(contentId, contentMeta.Context, ids, state));

                            return loader.LoadAsync(Convert.ToInt32(context.GetArgument<int>("id")));
                        })
                    });
                    var connectionField = new FieldType
                    {
                        Name = contentMeta.AliasPlural,
                        Description = contentMeta.FriendlyName + " - список",
                        ResolvedType = connectionGraphTypes[contentId],
                        Arguments = new QueryArguments(
                            new QueryArgument<IntGraphType> { Name = "skip", Description = "Skips edges before selection" },
                            new QueryArgument<StringGraphType> { Name = "after", Description = "Only return edges after the specified cursor." },
                            new QueryArgument<IntGraphType> { Name = "first", Description = "Specifies the maximum number of edges to return, starting after the cursor specified by 'after', or the first number of edges if 'after' is not specified." },
                            new QueryArgument<StringGraphType> { Name = "before", Description = "Only return edges prior to the specified cursor." },
                            new QueryArgument<IntGraphType> { Name = "last", Description = "Specifies the maximum number of edges to return, starting prior to the cursor specified by 'before', or the last number of edges if 'before' is not specified." },
                            new QueryArgument(filterGraphTypes[contentId]) { Name = "filter", Description = "Filter by" }
                        ),
                        Resolver = new AsyncFieldResolver<Connection<QpArticle, Edge<QpArticle>>>(async context =>
                        {
                            var state = GetQpArticleState(context.UserContext);
                            var needTotalCount = context.SubFields.Any(f => f.Key == "totalCount");
                            var relayResult = await context.RequestServices.GetRequiredService<IQpArticlesAccessor>().GetPagedArticles(contentId,
                                    contentMeta.Context,
                                    GetOrderArguments(context),
                                    GetFilterArguments(context, filterDefinitionsByContentTypes[contentId]),
                                    GetPaginationArguments(context),
                                    needTotalCount,
                                    state);
                            return ToConnection(relayResult);
                        })
                    };
                    if (orderGraphTypes.ContainsKey(contentId))
                        connectionField.Arguments.Add(new QueryArgument(orderGraphTypes[contentId]) { Name = "order", Description = "Order by" });
                    rootQuery.AddField(connectionField);
                }

                graphTypes.Values.ToList().ForEach(RegisterType);
                graphExtensionsTypes.Values.SelectMany(t => t.Values).ToList().ForEach(RegisterType);
                RegisterType(articleInterface);

                Query = rootQuery;

            }
            Description = "Autogenerated QP contents schema";

            _logger.LogInformation("End create schema {schemaId}", Id);
        }

        private static void ImplementInterface(IObjectGraphType objectType, IInterfaceGraphType interfaceType)
        {
            interfaceType.Fields.ToList().ForEach(f => objectType.AddField(f));
            objectType.AddResolvedInterface(interfaceType);
        }

        private static FieldType GetFieldType(
            QpContentAttributeMetadata attribute,
            IDataLoaderContextAccessor dataLoaderAccessor,
            IDictionary<int, QpContentMetadata> metadata,
            Dictionary<int, IComplexGraphType> graphTypes,
            Dictionary<int, ListGraphType> graphListTypes,
            Dictionary<int, ListGraphType> orderGraphTypes,
            Dictionary<int, InputObjectGraphType<object>> filterGraphTypes,
            Dictionary<int, Dictionary<string, QpFieldFilterDefinition>> filterDefinitionsByContentTypes)
        {
            FieldType f = null;
            var attributeAlias = $"cid_{attribute.ContentId}_{attribute.Alias}".ToLowerInvariant();
            switch (attribute.TypeName)
            {
                case "String":
                case "Textbox":
                case "VisualEdit":
                    f = new FieldType
                    {
                        Name = attribute.SchemaAlias,
                        Description = attribute.FriendlyName,
                        Type = typeof(StringGraphType),
                        Arguments = null,
                        Resolver = new FuncFieldResolver<QpArticle, object>(context => attribute.Content.Site.ReplacePlaceholders(
                            context.Source.AllFields[attributeAlias] as string))
                    };
                    break;
                case "Numeric":
                    //TODO: можно быть более точным в выборе GraphType, не всегда DecimalGraphType, еще может быть IntGraphType/LongGraphType
                    f = new FieldType
                    {
                        Name = attribute.SchemaAlias,
                        Description = attribute.FriendlyName,
                        Type = typeof(DecimalGraphType),
                        Arguments = null,
                        Resolver = new FuncFieldResolver<QpArticle, object>(context => context.Source.AllFields[attributeAlias])
                    };
                    break;
                case "Boolean":
                    f = new FieldType
                    {
                        Name = attribute.SchemaAlias,
                        Description = attribute.FriendlyName,
                        Type = typeof(BooleanGraphType),
                        Arguments = null,
                        Resolver = new FuncFieldResolver<QpArticle, bool>(context => Convert.ToInt32(context.Source.AllFields[attributeAlias]) == 1)
                    };
                    break;
                case "Date":
                    f = new FieldType
                    {
                        Name = attribute.SchemaAlias,
                        Description = attribute.FriendlyName,
                        Type = typeof(DateGraphType),
                        Arguments = null,
                        Resolver = new FuncFieldResolver<QpArticle, object>(context => context.Source.AllFields[attributeAlias])
                    };
                    break;
                case "Time":
                    f = new FieldType
                    {
                        Name = attribute.SchemaAlias,
                        Description = attribute.FriendlyName,
                        Type = typeof(TimeGraphType),
                        Arguments = null,
                        Resolver = new FuncFieldResolver<QpArticle, object>(context => context.Source.AllFields[attributeAlias])
                    };
                    break;
                case "DateTime":
                    f = new FieldType
                    {
                        Name = attribute.SchemaAlias,
                        Description = attribute.FriendlyName,
                        Type = typeof(DateTimeGraphType),
                        Arguments = null,
                        Resolver = new FuncFieldResolver<QpArticle, object>(context => context.Source.AllFields[attributeAlias])
                    };
                    break;
                case "File":
                case "Image":
                case "Dynamic Image":
                    f = new FieldType
                    {
                        Name = attribute.SchemaAlias,
                        Description = attribute.FriendlyName,
                        Type = typeof(UriGraphType),
                        Arguments = null,
                        Resolver = new FuncFieldResolver<QpArticle, object>(context =>
                        {
                            var url = context.Source.AllFields[attributeAlias] as string;

                            if (string.IsNullOrEmpty(url))
                            {
                                return null;
                            }
                            else
                            {
                                return $"{attribute.GetBaseUrl(true, false)}/{url}";
                            }
                        })
                    };
                    break;
                case "Relation":
                    bool isM2m = false;
                    bool isO2m = false;
                    int relationContentId = 0;
                    if (attribute.M2mRelationId.HasValue && attribute.RelatedM2mContentId.HasValue && attribute.M2mIsBackward.HasValue)
                    {
                        isM2m = true;
                        relationContentId = attribute.RelatedM2mContentId.Value;
                    }
                    else if (attribute.RelatedO2mContentId.HasValue)
                    {
                        isO2m = true;
                        relationContentId = attribute.RelatedO2mContentId.Value;
                    }
                    else
                    {
                        throw new Exception($"Incorrect relation field metadata. Field id = {attribute.Id}. Cannot decide O2M or M2M.");
                    }

                    if (!metadata.ContainsKey(relationContentId))
                    {
                        //если контента, на который идёт ссылка, нет в графе - делаем просто int-овое поле вместо ссылки
                        f = new FieldType
                        {
                            Name = attribute.SchemaAlias,
                            Description = attribute.FriendlyName,
                            Type = typeof(IntGraphType),
                            Arguments = null,
                            Resolver = new FuncFieldResolver<QpArticle, object>(context => context.Source.AllFields[attributeAlias])
                        };
                    }
                    else if (isM2m)
                    {
                        f = new FieldType
                        {
                            Name = attribute.SchemaAlias,
                            Description = attribute.FriendlyName,
                            ResolvedType = graphListTypes[relationContentId],
                            Arguments = GetRelationArguments(filterGraphTypes, orderGraphTypes, relationContentId),
                            Resolver = new FuncFieldResolver<QpArticle, IDataLoaderResult<IEnumerable<QpArticle>>>(context =>
                            {
                                var state = GetQpArticleState(context.UserContext);
                                var isBackward = attribute.M2mIsBackward.Value;
                                var orderArgs = GetOrderArguments(context);
                                var filterArgs = GetFilterArguments(context, filterDefinitionsByContentTypes[relationContentId]);
                                //нужно составить ключ для даталоадера с учётом сортировки и фильтра
                                var orderArgsKey = GetRelationOrderArgsKey(orderArgs);
                                var filterArgsKey = GetRelationFilterArgsKey(filterArgs);

                                var loader = dataLoaderAccessor.Context.GetOrAddCollectionBatchLoader<int, QpArticle>($"M2M_{attribute.Id}_filter({filterArgsKey})_order({orderArgsKey})",
                                    (ids) => context.RequestServices.GetRequiredService<IQpArticlesAccessor>().GetRelatedM2mArticlesByIdList(
                                        relationContentId,
                                        metadata[relationContentId].Context,
                                        ids,
                                        Convert.ToInt32(context.Source.AllFields[attributeAlias]),
                                        isBackward,
                                        orderArgs,
                                        filterArgs,
                                        state));


                                return loader.LoadAsync(context.Source.Id);
                            })
                        };
                    }
                    else if (isO2m)
                    {
                        f = new FieldType
                        {
                            Name = attribute.SchemaAlias,
                            Description = attribute.FriendlyName,
                            ResolvedType = graphTypes[relationContentId],

                            Arguments = null,
                            Resolver = new FuncFieldResolver<QpArticle, IDataLoaderResult<QpArticle>>(context =>
                            {
                                var state = GetQpArticleState(context.UserContext);
                                var loader = dataLoaderAccessor.Context.GetOrAddBatchLoader<int, QpArticle>($"Batch_{relationContentId}",
                                    (ids) => context.RequestServices.GetRequiredService<IQpArticlesAccessor>()
                                                .GetArticlesByIdList(relationContentId, metadata[relationContentId].Context, ids, state));

                                return loader.LoadAsync(Convert.ToInt32(context.Source.AllFields[attributeAlias]));
                            })
                        };
                    }

                    break;
                case "Relation Many-to-One":
                    relationContentId = 0;
                    string backwardFieldName = null;
                    if (attribute.RelatedM2oContentId.HasValue && !string.IsNullOrEmpty(attribute.RelatedM2oBackwardField))
                    {
                        relationContentId = attribute.RelatedM2oContentId.Value;
                        backwardFieldName = attribute.RelatedM2oBackwardField;
                    }
                    else
                    {
                        throw new Exception($"Incorrect M2O relation field metadata. Field id = {attribute.Id}.");
                    }

                    if (!metadata.ContainsKey(relationContentId))
                    {
                        //если контента, на который идёт ссылка, нет в графе - делаем просто int-овое поле вместо ссылки
                        f = new FieldType
                        {
                            Name = attribute.SchemaAlias,
                            Description = attribute.FriendlyName,
                            Type = typeof(IntGraphType),
                            Arguments = null,
                            Resolver = new FuncFieldResolver<QpArticle, object>(context => context.Source.AllFields[attributeAlias])
                        };
                    }
                    else
                    {
                        f = new FieldType
                        {
                            Name = attribute.SchemaAlias,
                            Description = attribute.FriendlyName,
                            ResolvedType = graphListTypes[relationContentId],
                            Arguments = GetRelationArguments(filterGraphTypes, orderGraphTypes, relationContentId),
                            Resolver = new FuncFieldResolver<QpArticle, IDataLoaderResult<IEnumerable<QpArticle>>>(context =>
                            {
                                var state = GetQpArticleState(context.UserContext);
                                var orderArgs = GetOrderArguments(context);
                                var filterArgs = GetFilterArguments(context, filterDefinitionsByContentTypes[relationContentId]);
                                //нужно составить ключ для даталоадера с учётом сортировки и фильтра
                                var orderArgsKey = GetRelationOrderArgsKey(orderArgs);
                                var filterArgsKey = GetRelationFilterArgsKey(filterArgs);

                                var loader = dataLoaderAccessor.Context.GetOrAddCollectionBatchLoader<int, QpArticle>($"M2O_{attribute.Id}_filter({filterArgsKey})_order({orderArgsKey})",
                                    (ids) => context.RequestServices.GetRequiredService<IQpArticlesAccessor>().GetRelatedM2oArticlesByIdList(
                                        relationContentId,
                                        metadata[relationContentId].Context,
                                        ids,
                                        backwardFieldName,
                                        orderArgs,
                                        filterArgs,
                                        state));

                                return loader.LoadAsync(context.Source.Id);
                            })
                        };
                    }
                    break;
            }

            return f;
        }

        private static void AddSortValue(EnumerationGraphType type, string name, string dbName)
        {
            type.AddValue($"{name}Asc", $"Order by {name} ascending", $"{dbName}");
            type.AddValue($"{name}Desc", $"Order by {name} descending", $"^{dbName}");
        }

        private static QueryArguments GetRelationArguments(Dictionary<int, InputObjectGraphType<object>> filterGraphTypes, Dictionary<int, ListGraphType> orderGraphTypes, int relationContentId)
        {
            var arguments = new QueryArguments(new QueryArgument(filterGraphTypes[relationContentId]) { Name = "filter", Description = "Filter by" });

            if (orderGraphTypes.ContainsKey(relationContentId))
            {
                arguments.Add(new QueryArgument(orderGraphTypes[relationContentId]) { Name = "order", Description = "Order by" });
            }

            return arguments;
        }

        private static string GetRelationOrderArgsKey(IList<string> orderArgs)
        {
            return orderArgs != null ? String.Join(",", orderArgs) : "";
        }

        private static string GetRelationFilterArgsKey(IEnumerable<QpFieldFilterClause> filterArgs)
        {
            if (filterArgs == null)
            {
                return "";
            }
            else
            {
                return String.Join(",", filterArgs
                    .OrderBy(fa => fa.FilterDefinition.QpFieldName)
                    .ThenBy(fa => fa.FilterDefinition.Operator)
                    .Select(fa => $"{fa.FilterDefinition.QpFieldName}_{fa.FilterDefinition.Operator}_{fa.GetHashCode()}"));
            }
        }

        private static void AddFiltersForSystemFields(InputObjectGraphType<object> filterType, Dictionary<string, QpFieldFilterDefinition> filterDefinitions)
        {
            var metadata = new QpContentAttributeMetadata
            {
                Alias = QpSystemFieldsDescriptor.Id.DBName,
                SchemaAlias = QpSystemFieldsDescriptor.Id.Name,
                TypeName = "Numeric"
            };

            AddFiltersForNumericField(filterType, filterDefinitions, metadata, typeof(DecimalGraphType));

            metadata = new QpContentAttributeMetadata
            {
                Alias = QpSystemFieldsDescriptor.StatusTypeId.DBName,
                SchemaAlias = QpSystemFieldsDescriptor.StatusTypeId.Name,
                TypeName = "Numeric"
            };

            AddFiltersForNumericField(filterType, filterDefinitions, metadata, typeof(DecimalGraphType));

            metadata = new QpContentAttributeMetadata
            {
                Alias = QpSystemFieldsDescriptor.Created.DBName,
                SchemaAlias = QpSystemFieldsDescriptor.Created.Name,
                TypeName = "DateTime"
            };

            AddFiltersForNumericField(filterType, filterDefinitions, metadata, typeof(DateTimeGraphType));

            metadata = new QpContentAttributeMetadata
            {
                Alias = QpSystemFieldsDescriptor.Modified.DBName,
                SchemaAlias = QpSystemFieldsDescriptor.Modified.Name,
                TypeName = "DateTime"
            };

            AddFiltersForNumericField(filterType, filterDefinitions, metadata, typeof(DateTimeGraphType));

            metadata = new QpContentAttributeMetadata
            {
                Alias = QpSystemFieldsDescriptor.LastModifiedBy.DBName,
                SchemaAlias = QpSystemFieldsDescriptor.LastModifiedBy.Name,
                TypeName = "Numeric"
            };

            AddFiltersForNumericField(filterType, filterDefinitions, metadata, typeof(DecimalGraphType));
        }

        private static void AddFiltersForNumericField(InputObjectGraphType<object> filterType, Dictionary<string, QpFieldFilterDefinition> filterDefinitions, QpContentAttributeMetadata attribute, Type graphType)
        {
            var clearedAlias = attribute.SchemaAlias;

            AddSimpleFiltersForField(filterType, filterDefinitions, attribute, graphType);

            filterType.AddField(new FieldType
            {
                Name = $"{clearedAlias}Gt",
                Description = $"Filter for articles, where {attribute.Alias} greater than this value",
                Type = graphType
            });
            filterDefinitions[$"{clearedAlias}Gt"] = new QpFieldFilterDefinition { QpFieldName = attribute.Alias, QpFieldType = attribute.TypeName, Operator = FilterOperator.GreaterThan };
            filterType.AddField(new FieldType
            {
                Name = $"{clearedAlias}Ge",
                Description = $"Filter for articles, where {attribute.Alias} greater than or equals to this value",
                Type = graphType
            });
            filterDefinitions[$"{clearedAlias}Ge"] = new QpFieldFilterDefinition { QpFieldName = attribute.Alias, QpFieldType = attribute.TypeName, Operator = FilterOperator.GreaterOrEqual };
            filterType.AddField(new FieldType
            {
                Name = $"{clearedAlias}Lt",
                Description = $"Filter for articles, where {attribute.Alias} lesser than this value",
                Type = graphType
            });
            filterDefinitions[$"{clearedAlias}Lt"] = new QpFieldFilterDefinition { QpFieldName = attribute.Alias, QpFieldType = attribute.TypeName, Operator = FilterOperator.LessThan };
            filterType.AddField(new FieldType
            {
                Name = $"{clearedAlias}Le",
                Description = $"Filter for articles, where {attribute.Alias} lesser than or equals to this value",
                Type = graphType
            });
            filterDefinitions[$"{clearedAlias}Le"] = new QpFieldFilterDefinition { QpFieldName = attribute.Alias, QpFieldType = attribute.TypeName, Operator = FilterOperator.LessOrEqual };
        }

        private static void AddFiltersForStringField(InputObjectGraphType<object> filterType, Dictionary<string, QpFieldFilterDefinition> filterDefinitions, QpContentAttributeMetadata attribute)
        {
            var clearedAlias = attribute.SchemaAlias;
            filterType.AddField(new FieldType
            {
                Name = $"{clearedAlias}Like",
                Description = $"Filter for articles, where {attribute.Alias} like this value",
                Type = typeof(StringGraphType)
            });
            filterDefinitions[$"{clearedAlias}Like"] = new QpFieldFilterDefinition { QpFieldName = attribute.Alias, QpFieldType = attribute.TypeName, Operator = FilterOperator.Like };
            filterType.AddField(new FieldType
            {
                Name = $"{clearedAlias}NotLike",
                Description = $"Filter for articles, where {attribute.Alias} NOT like this value",
                Type = typeof(StringGraphType)
            });
            filterDefinitions[$"{clearedAlias}NotLike"] = new QpFieldFilterDefinition { QpFieldName = attribute.Alias, QpFieldType = attribute.TypeName, Operator = FilterOperator.NotLike };
        }

        private static void AddSimpleFiltersForField(InputObjectGraphType<object> filterType, Dictionary<string, QpFieldFilterDefinition> filterDefinitions, QpContentAttributeMetadata attribute, Type graphType)
        {
            var clearedAlias = attribute.SchemaAlias;
            filterType.AddField(new FieldType
            {
                Name = $"{clearedAlias}Eq",
                Description = $"Filter for articles, where {attribute.Alias} equals to this value",
                Type = graphType
            });
            filterDefinitions[$"{clearedAlias}Eq"] = new QpFieldFilterDefinition { QpFieldName = attribute.Alias, QpFieldType = attribute.TypeName, Operator = FilterOperator.Equal };
            filterType.AddField(new FieldType
            {
                Name = $"{clearedAlias}Not",
                Description = $"Filter for articles, where {attribute.Alias} NOT equals to this value",
                Type = graphType
            });
            filterDefinitions[$"{clearedAlias}Not"] = new QpFieldFilterDefinition { QpFieldName = attribute.Alias, QpFieldType = attribute.TypeName, Operator = FilterOperator.NotEqual };
        }

        private static IList<string> GetOrderArguments(IResolveFieldContext context)
        {
            return context.GetArgument<List<string>>("order");
        }

        private static RelayPaginationArgs GetPaginationArguments(IResolveFieldContext context)
        {
            return new RelayPaginationArgs
            {
                Skip = context.GetArgument<int?>("skip"),
                After = context.GetArgument<string>("after"),
                First = context.GetArgument<int?>("first"),
                Before = context.GetArgument<string>("before"),
                Last = context.GetArgument<int?>("last")
            };
        }

        private static IEnumerable<QpFieldFilterClause> GetFilterArguments(IResolveFieldContext context, Dictionary<string, QpFieldFilterDefinition> possibleFilterArgs)
        {
            var dict = context.GetArgument<Dictionary<string, object>>("filter");

            return dict?.Select(kvp => new QpFieldFilterClause 
            { 
                FilterDefinition = possibleFilterArgs.First(kvp2 => kvp2.Key.Equals(kvp.Key, StringComparison.InvariantCultureIgnoreCase)).Value, //кривовато вышло, т.к. ключи равны, но регистр может отличаться
                Value = kvp.Value 
            });
        }

        private static Connection<QpArticle, Edge<QpArticle>> ToConnection(RelayPaginationResult paginationResult)
        {
            var edges = paginationResult.Articles.Select(a => new Edge<QpArticle> 
            { 
                Node = a, 
                Cursor = a.Id.ToString() //возможно, не совсем верно, что знание о том, как образуется курсор, есть в этом классе (т.к. от курсора зависит построение запроса в бд)
            }).ToList();
            return new Connection<QpArticle, Edge<QpArticle>>
            {
                TotalCount = paginationResult.TotalCount,
                PageInfo = new PageInfo 
                {
                    HasNextPage = paginationResult.HasNextPage,
                    HasPreviousPage = paginationResult.HasPreviousPage,
                    StartCursor = edges.FirstOrDefault()?.Cursor,
                    EndCursor = edges.LastOrDefault()?.Cursor
                },
                Edges = edges
            };
        }

        private static QpArticleState GetQpArticleState(IDictionary<string, object> context)
        {
            if (context.TryGetValue(ServicesExstension.QpArticleStateField, out object value))
            {
                var state = value as QpArticleState?;

                if (state.HasValue)
                {
                    return state.Value;
                }
            }

            return QpArticleState.Live;
        }

        protected override void Dispose(bool disposing)
        {                        
            base.Dispose(disposing);

            _logger.LogInformation("Schema {schemaId} is disposed", Id);

        }
    }
}
