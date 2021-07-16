using QP.GraphQL.Interfaces.Metadata;
using System.Collections.Generic;

namespace QP.GraphQL.App.Schema
{
    public interface IQpMetadataValidator
    {
        IDictionary<int, QpContentMetadata> ValidateFields(IDictionary<int, QpContentMetadata> metadata);
    }
}
