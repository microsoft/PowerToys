using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.Plugin;

namespace Wox.PluginLoader
{
    public class ExecutablePluginLoader : BasePluginLoader
    {
        public override List<PluginPair> LoadPlugin()
        {
            List<PluginPair> plugins = new List<PluginPair>();
            List<PluginMetadata> metadatas = pluginMetadatas.Where(o => o.Language.ToUpper() == AllowedLanguage.ExecutableFile.ToUpper()).ToList();
            foreach (PluginMetadata metadata in metadatas)
            {
                ExecutablePluginWrapper executer = new ExecutablePluginWrapper();
                PluginPair pair = new PluginPair()
                {
                    Plugin = executer,
                    Metadata = metadata
                };
                plugins.Add(pair);
            }

            return plugins;
        }
    }
}
