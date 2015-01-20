using System.Collections.Generic;
using Wox.Plugin;

namespace Wox.Core.Plugin
{
    internal interface IPluginLoader
    {
        IEnumerable<PluginPair> LoadPlugin(List<PluginMetadata> pluginMetadatas);
    }
}
