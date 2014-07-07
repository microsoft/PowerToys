using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.Plugin;

namespace Wox.PluginLoader
{
    public interface IPluginLoader
    {
        List<PluginPair> LoadPlugin(List<PluginMetadata> pluginMetadatas);
    }
}
