using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Wox.Infrastructure.Logger;
using Wox.Plugin;
//using Wox.Plugin.SystemPlugins;

namespace Wox.Core.Plugin
{
    internal class CSharpPluginLoader : IPluginLoader
    {
        public IEnumerable<PluginPair> LoadPlugin(List<PluginMetadata> pluginMetadatas)
        {
            var plugins = new List<PluginPair>();
            List<PluginMetadata> CSharpPluginMetadatas = pluginMetadatas.Where(o => o.Language.ToUpper() == AllowedLanguage.CSharp.ToUpper()).ToList();

            foreach (PluginMetadata metadata in CSharpPluginMetadatas)
            {
                try
                {
                    Assembly asm = Assembly.Load(AssemblyName.GetAssemblyName(metadata.ExecuteFilePath));
                    List<Type> types = asm.GetTypes().Where(o => o.IsClass && !o.IsAbstract &&  o.GetInterfaces().Contains(typeof(IPlugin))).ToList();
                    if (types.Count == 0)
                    {
                        Log.Warn(string.Format("Couldn't load plugin {0}: didn't find the class that implement IPlugin", metadata.Name));
                        continue;
                    }

                    foreach (Type type in types)
                    {
                        PluginPair pair = new PluginPair()
                        {
                            Plugin = Activator.CreateInstance(type) as IPlugin,
                            Metadata = metadata
                        };

                        //var sys = pair.Plugin as BaseSystemPlugin;
                        //if (sys != null)
                        //{
                        //    sys.PluginDirectory = metadata.PluginDirectory;
                        //}

                        plugins.Add(pair);
                    }
                }
                catch (System.Exception e)
                {
                    Log.Error(string.Format("Couldn't load plugin {0}: {1}", metadata.Name, e.Message));
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