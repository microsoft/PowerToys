using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

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
                    List<Type> types = asm.GetTypes().Where(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Contains(typeof(IPlugin))).ToList();
                    if (types.Count == 0)
                    {
                        Log.Warn($"Couldn't load plugin {metadata.Name}: didn't find the class that implement IPlugin");
                        continue;
                    }

                    foreach (Type type in types)
                    {
                        PluginPair pair = new PluginPair
                        {
                            Plugin = Activator.CreateInstance(type) as IPlugin,
                            Metadata = metadata
                        };

                        plugins.Add(pair);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(new WoxPluginException(metadata.Name, $"Couldn't load plugin", e));
                }
            }

            return plugins;
        }
    }
}