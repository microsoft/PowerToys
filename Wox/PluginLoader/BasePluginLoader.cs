using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.Plugin;

namespace Wox.PluginLoader
{
    public class BasePluginLoader<T> : IPluginLoader where T : BasePlugin, new()
    {
        public virtual List<PluginPair> LoadPlugin(List<PluginMetadata> pluginMetadatas)
        {
            T pluginWrapper = new T();
            List<PluginMetadata> metadatas = pluginMetadatas.Where(o => pluginWrapper.SupportedLanguage.ToUpper() == o.Language.ToUpper()).ToList();

            return metadatas.Select(metadata => new PluginPair()
            {
                Plugin = pluginWrapper, 
                Metadata = metadata
            }).ToList();
        }
    }
}
