using System.Collections.Generic;
using System.Threading.Tasks;

namespace QP.GraphQL.Interfaces.Metadata
{
    public interface IQpMetadataAccessor
    {
        IDictionary<int, QpContentMetadata> GetContentsMetadata();
    }
}
