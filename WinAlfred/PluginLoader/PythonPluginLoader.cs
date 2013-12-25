using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WinAlfred.Plugin;

namespace WinAlfred.PluginLoader
{
    public class PythonPluginLoader : BasePluginLoader
    {
        public override List<PluginPair> LoadPlugin()
        {
            List<PluginPair> plugins = new List<PluginPair>();
            List<PluginMetadata> metadatas = pluginMetadatas.Where(o => o.Language.ToUpper() == AllowedLanguage.Python.ToUpper()).ToList();
            foreach (PluginMetadata metadata in metadatas)
            {
                PythonPluginWrapper python = new PythonPluginWrapper(metadata);
                PluginPair pair = new PluginPair()
                {
                    Plugin = python,
                    Metadata = metadata
                };
                plugins.Add(pair);
            }

            foreach (IPlugin plugin in plugins.Select(pluginPair => pluginPair.Plugin))
            {
                new Thread(plugin.Init).Start();
            }
            return plugins;
        }
    }
}
