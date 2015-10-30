using System;
using System.Collections.Generic;
using System.Linq;
using Wox.Core.Plugin;
using Wox.Plugin;

namespace Wox.Core
{
    internal class AssemblyHelper
    {
        public static List<KeyValuePair<PluginPair, T>> LoadPluginInterfaces<T>() where T : class
        {
            List<KeyValuePair<PluginPair, T>> results = new List<KeyValuePair<PluginPair, T>>();
            foreach (PluginPair pluginPair in PluginManager.AllPlugins)
            {
                //need to load types from AllPlugins
                //PluginInitContext is only available in this instance
                T type = pluginPair.Plugin as T;
                if (type != null)
                {
                    results.Add(new KeyValuePair<PluginPair, T>(pluginPair,type));
                }
            }
            return results;
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
