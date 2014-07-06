using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.Plugin;
using Wox.RPC;

namespace Wox.PluginLoader
{
    public class BasePluginLoader<T> where T :BasePluginWrapper,new()
    {
        public List<PluginPair> LoadPlugin(List<PluginMetadata> pluginMetadatas)
        {
            List<PluginPair> plugins = new List<PluginPair>();

            T pluginWrapper = new T();
            List<string> allowedLanguages = pluginWrapper.GetAllowedLanguages();
            List<PluginMetadata> metadatas = pluginMetadatas.Where(o => allowedLanguages.Contains(o.Language.ToUpper())).ToList();
            foreach (PluginMetadata metadata in metadatas)
            {
                PluginPair pair = new PluginPair()
                {
                    Plugin = pluginWrapper,
                    Metadata = metadata
                };
                plugins.Add(pair);
            }

            return plugins;
        }
    }
}
