﻿using System;
using System.Collections.Generic;

namespace QP.GraphQL.Interfaces.Articles
{
    public class QpArticle
    {
        public QpArticle(int contentId)
        {
            ContentId = contentId;
            AllFields = new Dictionary<string, object>();
        }
        public int ContentId { get; private set; }
        public int? ExtensionContentId { get; set; }
        public int Id { get; set; }
        public int StatusTypeId { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public int LastModifiedBy { get; set; }

        public Dictionary<string, object> AllFields { get; set; }
    }
}
