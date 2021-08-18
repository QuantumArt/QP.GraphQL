namespace QP.GraphQL.DAL
{
    public static class QpSystemFieldsDescriptor
    {
        public static string[] SystemFields = new string[]{
            Id.Name,
            StatusTypeId.Name,
            Created.Name,
            Modified.Name,
            LastModifiedBy.Name
        };

        public static class Id
        {
            public const string Name = "Id";
            public const string Description = "Идентификатор";
            public const string DBName = "content_item_id";
        }

        public static class StatusTypeId
        {
            public const string Name = "StatusTypeId";
            public const string Description = "Статус";
            public const string DBName = "status_type_id";
        }

        public static class Created
        {
            public const string Name = "Created";
            public const string Description = "Дата создания";
            public const string DBName = "created";
        }

        public static class Modified
        {
            public const string Name = "Modified";
            public const string Description = "Дата изменения";
            public const string DBName = "modified";
        }

        public static class LastModifiedBy
        {
            public const string Name = "LastModifiedBy";
            public const string Description = "Идентификатор пользователя, обновившего статью";
            public const string DBName = "last_modified_by";
        }
    }
}
