using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Wox.Core.Plugin;
using Wox.Plugin;
using Wox.Infrastructure.Exception;

namespace Wox.Core.Resource
{
    public static class ResourceMerger
    {
        private static void RemoveResource(string directoryName)
        {
            directoryName = $"{Path.DirectorySeparatorChar}{directoryName}";
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            foreach (var resource in dictionaries)
            {
                string currentDirectoryName = Path.GetDirectoryName(resource.Source.AbsolutePath);
                if (currentDirectoryName == directoryName)
                {
                    dictionaries.Remove(resource);
                    break;
                }
            }
        }

        public static void UpdateResource<T>(T t) where T : Core.Resource.Resource
        {
            RemoveResource(t.DirectoryName);
            Application.Current.Resources.MergedDictionaries.Add(t.GetResourceDictionary());
        }

        internal static void UpdatePluginLanguages()
        {
            RemoveResource(Infrastructure.Constant.Plugins);
            foreach (var plugin in PluginManager.GetPluginsForInterface<IPluginI18n>())
            {
                var location = Assembly.GetAssembly(plugin.Plugin.GetType()).Location;
                var directoryName = Path.GetDirectoryName(location);
                if (directoryName != null)
                {
                    var internationalization = InternationalizationManager.Instance;
                    var folder = Path.Combine(directoryName, internationalization.DirectoryName);
                    var file = internationalization.GetLanguageFile(folder);
                    if (!string.IsNullOrEmpty(file))
                    {
                        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                        {
                            Source = new Uri(file, UriKind.Absolute)
                        });
                    }
                }
                else
                {
                    throw new WoxPluginException(plugin.Metadata.Name, "Can't find plugin location.");
                }
                
            }
        }
    }
}