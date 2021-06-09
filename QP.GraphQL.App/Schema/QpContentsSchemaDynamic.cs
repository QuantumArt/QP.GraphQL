using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Resolvers;
using GraphQL.Types;
using GraphQL.Types.Relay;
using GraphQL.Types.Relay.DataObjects;
using GraphQL.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        public QpContentsSchemaDynamic(IServiceProvider serviceProvider, 
            IDataLoaderContextAccessor dataLoaderAccessor,
            IQpMetadataAccessor metadataAccessor,
            ILogger<QpContentsSchemaDynamic> logger
            )
            : base(serviceProvider)
        {
            var metadata = metadataAccessor.GetContentsMetadata();
            var graphTypes = new Dictionary<int, IObjectGraphType>();
            var graphListTypes = new Dictionary<int, ListGraphType>();
            var connectionGraphTypes = new Dictionary<int, ObjectGraphType<Connection<QpArticle, Edge<QpArticle>>>>();
            var orderGraphTypes = new Dictionary<int, ListGraphType>();
            var filterGraphTypes = new Dictionary<int, InputObjectGraphType<object>>();
            var filterDefinitionsByContentTypes = new Dictionary<int, Dictionary<string, QpFieldFilterDefinition>>();

            //валидация полей
            foreach(var item in metadata)
            {
                var attributes = new List<QpContentAttributeMetadata>();

                foreach(var a in item.Value.Attributes)
                {
                    try
                    {
                        NameValidator.ValidateName(a.Alias, NamedElement.Field);
                        attributes.Add(a);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning("field {field} not pass: {validationerror}", new { a.Id, a.Alias, a.ContentId}, ex.Message);
                    }
                }

                item.Value.Attributes = attributes;
            }

            //создаём типы графов по каждому типу контента
            foreach (var contentId in metadata.Keys)
            {
                var contentMeta = metadata[contentId];
                var graphType = new ObjectGraphType<QpArticle>()
                {
                    Name = ClearifyGraphName(contentMeta.AliasSingular),
                    Description = contentMeta.FriendlyName
                };
                graphTypes[contentId] = graphType;
                graphListTypes[contentId] = new ListGraphType(graphType);

                //создаём input-тип, который будет использоваться для задания сортировки по статьям этого типа
                //я решил дать возможность сортировки только по Indexed-полям (если их не будет, то не будет и возможности сортировки)
                if (contentMeta.Attributes.Any(ca => ca.Indexed))
                {
                    var orderEnumType = new EnumerationGraphType 
                    { 
                        Name = $"PossibleOrderFor{graphType.Name}", 
                        Description = $"Possible order by literals for content type {graphType.Name}"
                    };
                    foreach (var attribute in contentMeta.Attributes.Where(ca => ca.Indexed))
                    {
                        var attributeAlias = attribute.Alias.ToLowerInvariant();
                        orderEnumType.AddValue($"{attribute.Alias}Asc", $"Order by {attribute.Alias} ascending", $"{attribute.Alias}");
                        orderEnumType.AddValue($"{attribute.Alias}Desc", $"Order by {attribute.Alias} descending", $"^{attribute.Alias}");
                    }
                    orderGraphTypes[contentId] = new ListGraphType(orderEnumType);
                }

                //создаём input-тип, который будет использоваться для задания фильтра по статьям этого типа
                //также нам понадобится словарь, чтобы потом в резолверах расшифровать все эти фильтры, к каким полям они относятся и какую операцию в себе несут
                var filterType = new InputObjectGraphType<object>()
                {
                    Name = $"FilterFor{graphType.Name}",
                    Description = $"Filter object for content type {graphType.Name}"
                };
                var filterDefinitions = new Dictionary<string, QpFieldFilterDefinition>();
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
                        case "DateTime":
                            AddFiltersForNumericField(filterType, filterDefinitions, attribute, typeof(DateTimeGraphType));
                            break;
                        case "Relation":
                            //TODO: сейчас будет работать только для O2M связей, нет фильтров ни по M2M, ни по M2O. также нет фильтров второго уровня (по какому-то полю из связи o2m)
                            AddSimpleFiltersForField(filterType, filterDefinitions, attribute, typeof(IntGraphType));
                            break;
                    }
                }
                filterGraphTypes[contentId] = filterType;
                filterDefinitionsByContentTypes[contentId] = filterDefinitions;

                //создаём connection-тип (https://relay.dev/graphql/connections.htm)
                var connectionType = new ObjectGraphType<Connection<QpArticle, Edge<QpArticle>>>()
                {
                    Name = $"{graphType.Name}Connection",
                    Description = $"A connection to a list of objects of type `{contentMeta.AliasSingular}`"
                };

                //для connection-типа нам понадобится edge-тип
                var edgeType = new ObjectGraphType<Edge<QpArticle>>()
                {
                    Name = $"{graphType.Name}Edge",
                    Description = $"Edge of a connection containing a node (a row of `{contentMeta.AliasSingular}`) and cursor"
                };
                edgeType.AddField(new FieldType
                {
                    Name = "node",
                    Description = $"A single row of `{contentMeta.AliasSingular}` within the result data set.",
                    ResolvedType = graphType,
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
                    ResolvedType = graphListTypes[contentId],
                    Resolver = new FuncFieldResolver<Connection<QpArticle, Edge<QpArticle>>, List<QpArticle>>(context => context.Source.Items)
                });
                connectionGraphTypes[contentId] = connectionType;
            }

            //создаём поля для каждого типа графа
            foreach (var contentId in metadata.Keys)
            {
                var contentMeta = metadata[contentId];
                var graphType = graphTypes[contentId];

                //всегда добавляем id
                graphType.AddField(new FieldType
                {
                    Name = "Id", 
                    Description = "Id", 
                    Type = typeof(IntGraphType),
                    Arguments = null,
                    Resolver = new FuncFieldResolver<QpArticle, object>(context => context.Source.Id)
                });

                foreach (var attribute in contentMeta.Attributes)
                {
                    FieldType f = null;
                    var attributeAlias = attribute.Alias.ToLowerInvariant();
                    switch (attribute.TypeName)
                    {
                        case "String":
                        case "Textbox":
                        case "VisualEdit":
                            f = new FieldType
                            {
                                Name = ClearifyGraphName(attribute.Alias),
                                Description = attribute.FriendlyName,
                                Type = typeof(StringGraphType),
                                Arguments = null,
                                Resolver = new FuncFieldResolver<QpArticle, object>(context => context.Source.AllFields[attributeAlias])
                            };
                            break;
                        case "Numeric":
                            //TODO: можно быть более точным в выборе GraphType, не всегда DecimalGraphType, еще может быть IntGraphType/LongGraphType
                            f = new FieldType
                            {
                                Name = ClearifyGraphName(attribute.Alias),
                                Description = attribute.FriendlyName,
                                Type = typeof(DecimalGraphType),
                                Arguments = null,
                                Resolver = new FuncFieldResolver<QpArticle, object>(context => context.Source.AllFields[attributeAlias])
                            };
                            break;
                        case "Boolean":
                            f = new FieldType
                            {
                                Name = ClearifyGraphName(attribute.Alias),
                                Description = attribute.FriendlyName,
                                Type = typeof(BooleanGraphType),
                                Arguments = null,
                                Resolver = new FuncFieldResolver<QpArticle, bool>(context => Convert.ToInt32(context.Source.AllFields[attributeAlias]) == 1)
                            };
                            break;
                        case "Date":
                            f = new FieldType
                            {
                                Name = ClearifyGraphName(attribute.Alias),
                                Description = attribute.FriendlyName,
                                Type = typeof(DateGraphType),
                                Arguments = null,
                                Resolver = new FuncFieldResolver<QpArticle, object>(context => context.Source.AllFields[attributeAlias])
                            };
                            break;
                        case "Time":
                        case "DateTime":
                            f = new FieldType
                            {
                                Name = ClearifyGraphName(attribute.Alias),
                                Description = attribute.FriendlyName,
                                Type = typeof(DateTimeGraphType),
                                Arguments = null,
                                Resolver = new FuncFieldResolver<QpArticle, object>(context => context.Source.AllFields[attributeAlias])
                            };
                            break;
                        case "File":
                        case "Image":
                        case "Dynamic Image":
                            //TODO: нужно генерировать полную ссылку на файл здесь, с учётом настроек QP
                            f = new FieldType
                            {
                                Name = ClearifyGraphName(attribute.Alias),
                                Description = attribute.FriendlyName,
                                Type = typeof(UriGraphType),
                                Arguments = null,
                                Resolver = new FuncFieldResolver<QpArticle, object>(context => context.Source.AllFields[attributeAlias])
                            };
                            break;
                        case "Relation":
                            bool isM2m = false;
                            bool isO2m = false;
                            int relationContentId = 0;
                            if (attribute.M2mRelationId.HasValue && attribute.RelatedM2mContentId.HasValue)
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
                                    Name = ClearifyGraphName(attribute.Alias),
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
                                    Name = ClearifyGraphName(attribute.Alias),
                                    Description = attribute.FriendlyName,
                                    ResolvedType = graphListTypes[relationContentId],
                                    Arguments = new QueryArguments(
                                        new QueryArgument(filterGraphTypes[relationContentId]) { Name = "filter", Description = "Filter by" }
                                    ),
                                    Resolver = new FuncFieldResolver<QpArticle, IDataLoaderResult<IEnumerable<QpArticle>>>(context =>
                                    {
                                        var orderArgs = GetOrderArguments(context);
                                        var filterArgs = GetFilterArguments(context, filterDefinitionsByContentTypes[relationContentId]);
                                        //нужно составить ключ для даталоадера с учётом сортировки и фильтра
                                        var orderArgsKey = orderArgs != null ? String.Join(",", orderArgs) : "";
                                        var filterArgsKey = filterArgs != null ? String.Join(",", filterArgs
                                            .OrderBy(fa => fa.FilterDefinition.QpFieldName)
                                            .ThenBy(fa => fa.FilterDefinition.Operator)
                                            .Select(fa => $"{fa.FilterDefinition.QpFieldName}_{fa.FilterDefinition.Operator}_{fa.GetHashCode()}")) : "";

                                        var loader = dataLoaderAccessor.Context.GetOrAddCollectionBatchLoader<int, QpArticle>($"M2M_{attribute.Id}_filter({filterArgsKey})_order({orderArgsKey})",
                                            (ids) => context.RequestServices.GetRequiredService<IQpArticlesAccessor>().GetRelatedM2mArticlesByIdList(
                                                relationContentId, 
                                                ids, 
                                                Convert.ToInt32(context.Source.AllFields[attributeAlias]),
                                                orderArgs,
                                                filterArgs));

                                        return loader.LoadAsync(context.Source.Id);
                                    })
                                };
                                if (orderGraphTypes.ContainsKey(relationContentId))
                                    f.Arguments.Add(new QueryArgument(orderGraphTypes[relationContentId]) { Name = "order", Description = "Order by" });
                            }
                            else if (isO2m)
                            {
                                f = new FieldType
                                {
                                    Name = ClearifyGraphName(attribute.Alias),
                                    Description = attribute.FriendlyName,
                                    ResolvedType = graphTypes[relationContentId],
                                    Arguments = null,
                                    Resolver = new FuncFieldResolver<QpArticle, IDataLoaderResult<QpArticle>>(context =>
                                    {
                                        var loader = dataLoaderAccessor.Context.GetOrAddBatchLoader<int, QpArticle>($"Batch_{relationContentId}",
                                            (ids) => context.RequestServices.GetRequiredService<IQpArticlesAccessor>()
                                                        .GetArticlesByIdList(relationContentId, ids));

                                        return loader.LoadAsync(Convert.ToInt32(context.Source.AllFields[attributeAlias]));
                                    })
                                };
                            }

                            break;
                        case "Relation Many-to-One":
                            //TODO: реализовать
                            break;
                    }

                    if (f != null)
                        graphType.AddField(f);
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
                    Name = ClearifyGraphName(contentMeta.AliasSingular),
                    Description = contentMeta.FriendlyName + " по id", 
                    ResolvedType = graphTypes[contentId],
                    Arguments = new QueryArguments(
                        new QueryArgument<NonNullGraphType<IntGraphType>> { Name = "Id", Description = "id of the article" }
                    ),
                    Resolver = new FuncFieldResolver<QpArticle, IDataLoaderResult<QpArticle>>(context => 
                    {
                        var loader = dataLoaderAccessor.Context.GetOrAddBatchLoader<int, QpArticle>($"Batch_{contentId}",
                                            (ids) => context.RequestServices.GetRequiredService<IQpArticlesAccessor>()
                                                        .GetArticlesByIdList(contentId, ids));

                        return loader.LoadAsync(Convert.ToInt32(context.GetArgument<int>("id")));
                    })
                });
                var connectionField = new FieldType
                {
                    Name = ClearifyGraphName(contentMeta.AliasPlural),
                    Description = contentMeta.FriendlyName + " - список",
                    ResolvedType = connectionGraphTypes[contentId],
                    Arguments = new QueryArguments(
                        new QueryArgument<StringGraphType> { Name = "after", Description = "Only return edges after the specified cursor." },
                        new QueryArgument<IntGraphType> { Name = "first", Description = "Specifies the maximum number of edges to return, starting after the cursor specified by 'after', or the first number of edges if 'after' is not specified." },
                        new QueryArgument<StringGraphType> { Name = "before", Description = "Only return edges prior to the specified cursor." },
                        new QueryArgument<IntGraphType> { Name = "last", Description = "Specifies the maximum number of edges to return, starting prior to the cursor specified by 'before', or the last number of edges if 'before' is not specified." },
                        new QueryArgument(filterGraphTypes[contentId]) { Name = "filter", Description = "Filter by" }
                    ),
                    Resolver = new AsyncFieldResolver<Connection<QpArticle, Edge<QpArticle>>>(async context =>
                    {
                        var needTotalCount = context.SubFields.Any(f => f.Key == "totalCount");
                        var relayResult = await context.RequestServices.GetRequiredService<IQpArticlesAccessor>().GetPagedArticles(contentId, 
                                GetOrderArguments(context), 
                                GetFilterArguments(context, filterDefinitionsByContentTypes[contentId]),
                                GetPaginationArguments(context),
                                needTotalCount);
                        return ToConnection(relayResult);
                    })
                };
                if (orderGraphTypes.ContainsKey(contentId))
                    connectionField.Arguments.Add(new QueryArgument(orderGraphTypes[contentId]) { Name = "order", Description = "Order by" });
                rootQuery.AddField(connectionField);
            }

            Query = rootQuery;
            Description = "Autogenerated QP contents schema";
        }

        private static void AddFiltersForNumericField(InputObjectGraphType<object> filterType, Dictionary<string, QpFieldFilterDefinition> filterDefinitions, QpContentAttributeMetadata attribute, Type graphType)
        {
            var clearedAlias = ClearifyGraphName(attribute.Alias);

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
            var clearedAlias = ClearifyGraphName(attribute.Alias);
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
            var clearedAlias = ClearifyGraphName(attribute.Alias);
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

        private static string ClearifyGraphName(string alias)
        {
            //TODO: по идее надо сюда добавить отсечение всяких запрещенных в GraphQL, но разрешенных в QP, символов
            return alias;
        }

        private static IList<string> GetOrderArguments(IResolveFieldContext context)
        {
            return context.GetArgument<List<string>>("order");
        }

        private static RelayPaginationArgs GetPaginationArguments(IResolveFieldContext context)
        {
            return new RelayPaginationArgs
            {
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
    }
}
