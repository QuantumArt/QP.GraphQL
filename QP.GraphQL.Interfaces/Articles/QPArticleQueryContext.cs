using System;
using System.Collections.Generic;
using System.Text;

namespace QP.GraphQL.Interfaces.Articles
{
    public class ContentContext
    {
        public int ContetnId { get; set; }
        public string TableALias => $"cid_{ContetnId}";
        public FieldContext[] Fields { get; set; }
    }

    public class RootContext : ContentContext
    {
        public ExtensionContext[] Extensions { get; set; }
        public FieldContext Classifier { get; set; }

    }

    public class ExtensionContext : ContentContext
    {
        public string ReferenceToBase { get; set; }
    }


    public class FieldContext
    {
        public int ContetnId { get; set; }
        public string QueryAlias => $"cid_{ContetnId}_{Alias}";
        public string Alias { get; set; }
    }
}
