using System.Collections.Generic;
using System.Threading.Tasks;

namespace QP.GraphQL.Interfaces.Metadata
{
    public interface IQpMetadataAccessor
    {
        IDictionary<int, QpContentMetadata> GetContentsMetadata(QpPluginMetadata plugin);
        QpPluginMetadata GetPluginMetadata(string instanceKey);
        QpPluginSiteMetadata GetPluginSiteMetadata(QpPluginMetadata plugin);
    }
}
