using System.Collections.Generic;
using System.Threading.Tasks;

namespace QP.GraphQL.Interfaces.Metadata
{
    public interface IQpMetadataAccessor
    {
        Task<IDictionary<int, QpContentMetadata>> GetContentsMetadata(IEnumerable<int> contentIds);
    }
}
