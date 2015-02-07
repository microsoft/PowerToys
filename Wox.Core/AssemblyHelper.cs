using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Wox.Core.Plugin;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Core
{
    internal class AssemblyHelper
    {
        public static List<KeyValuePair<PluginPair, T>> LoadPluginInterfaces<T>() where T : class
        {
            List<PluginMetadata> CSharpPluginMetadatas = PluginManager.AllPlugins.Select(o => o.Metadata).Where(o => o.Language.ToUpper() == AllowedLanguage.CSharp.ToUpper()).ToList();
            List<KeyValuePair<PluginPair, T>> plugins = new List<KeyValuePair<PluginPair, T>>();
            foreach (PluginMetadata metadata in CSharpPluginMetadatas)
            {
                try
                {
                    Assembly asm = Assembly.Load(AssemblyName.GetAssemblyName(metadata.ExecuteFilePath));
                    List<Type> types = asm.GetTypes().Where(o => o.IsClass && !o.IsAbstract && o.GetInterfaces().Contains(typeof(T))).ToList();
                    if (types.Count == 0)
                    {
                        continue;
                    }

                    foreach (Type type in types)
                    {
                        plugins.Add(new KeyValuePair<PluginPair, T>(PluginManager.AllPlugins.First(o => o.Metadata.ID == metadata.ID),
                            Activator.CreateInstance(type) as T));
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

        public static List<T> LoadInterfacesFromAppDomain<T>() where T : class
        {
            var interfaceObjects = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => p.IsClass && !p.IsAbstract && p.GetInterfaces().Contains(typeof(T)));

            return interfaceObjects.Select(interfaceObject => (T) Activator.CreateInstance(interfaceObject)).ToList();
        }
    }
}
