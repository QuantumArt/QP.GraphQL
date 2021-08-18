using GraphQL;
using GraphQL.Resolvers;
using GraphQL.Types;
using QP.GraphQL.DAL;
using QP.GraphQL.Interfaces.Articles;
using System;

namespace QP.GraphQL.App.Types
{
    public class ArticleInterface : InterfaceGraphType<QpArticle>
    {
        public ArticleInterface()
        {
            Name = "Article";
            Description = "Article system fields";

            AddField<IntGraphType>(QpSystemFieldsDescriptor.Id.Name, QpSystemFieldsDescriptor.Id.Description, context => context.Source.Id);
            AddField<IntGraphType>(QpSystemFieldsDescriptor.StatusTypeId.Name, QpSystemFieldsDescriptor.StatusTypeId.Description, context => context.Source.StatusTypeId);
            AddField<DateTimeGraphType>(QpSystemFieldsDescriptor.Created.Name, QpSystemFieldsDescriptor.Created.Description, context => context.Source.Created);
            AddField<DateTimeGraphType>(QpSystemFieldsDescriptor.Modified.Name, QpSystemFieldsDescriptor.Modified.Description, context => context.Source.Modified);
            AddField<IntGraphType>(QpSystemFieldsDescriptor.LastModifiedBy.Name, QpSystemFieldsDescriptor.LastModifiedBy.Description, context => context.Source.LastModifiedBy);
        }

        private FieldType AddField<T>(string name, string description, Func<IResolveFieldContext<QpArticle>, object> resolver) where T: GraphType
        {
            return AddField(new FieldType
            {
                Name = name,
                Description = description,
                Type = typeof(T),
                Arguments = null,
                Resolver = new FuncFieldResolver<QpArticle, object>(resolver)
            });
        }
    }
}
