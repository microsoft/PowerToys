using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using WinAlfred.Helper;
using WinAlfred.Plugin;

namespace WinAlfred.PluginLoader
{
    public class CSharpPluginLoader : BasePluginLoader
    {
        public override List<PluginPair> LoadPlugin()
        {
            List<PluginPair> plugins = new List<PluginPair>();

            List<PluginMetadata> metadatas = pluginMetadatas.Where(o => o.Language.ToUpper() == AllowedLanguage.CSharp.ToUpper()).ToList();
            foreach (PluginMetadata metadata in metadatas)
            {
                try
                {
                    Assembly asm = Assembly.LoadFile(metadata.ExecuteFile);
                    List<Type> types = asm.GetTypes().Where(o => o.GetInterfaces().Contains(typeof (IPlugin))).ToList();
                    if (types.Count == 0)
                    {
                        Log.Error(string.Format("Cound't load plugin {0}: didn't find the class who implement IPlugin",
                            metadata.Name));
                        continue;
                    }
                    if (types.Count > 1)
                    {
                        Log.Error(
                            string.Format(
                                "Cound't load plugin {0}: find more than one class who implement IPlugin, there should only one class implement IPlugin",
                                metadata.Name));
                        continue;
                    }

                    PluginPair pair = new PluginPair()
                    {
                        Plugin = Activator.CreateInstance(types[0]) as IPlugin,
                        Metadata = metadata
                    };
                    plugins.Add(pair);
                }
                catch (Exception e)
                {
                    Log.Error(string.Format("Cound't load plugin {0}: {1}", metadata.Name, e.Message));
#if (DEBUG)
                    {
                        throw;
                    }
#endif
                }

            }

            return plugins;
        }
    }
}
