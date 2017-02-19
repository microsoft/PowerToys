using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Wox.Core.Plugin;
using Wox.Infrastructure;
using Wox.Plugin;
using Wox.Infrastructure.Exception;
using Wox.Infrastructure.Logger;

namespace Wox.Core.Resource
{
    public static class ResourceMerger
    {
        static ResourceMerger()
        {
            // remove all dictionaries defined in xaml e.g.g App.xaml
            Application.Current.Resources.MergedDictionaries.Clear();
        }
        private static void RemoveResource(string directoryName)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var invalids = dictionaries.Where(dict =>
            {
                var dir = Path.GetDirectoryName(dict.Source.AbsolutePath).NonNull();
                var invalid = dir.Contains(directoryName);
                return invalid;
            }).ToList();
            foreach (var i in invalids)
            {
                dictionaries.Remove(i);
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
                        var resource = new ResourceDictionary
                        {
                            Source = new Uri(file, UriKind.Absolute)
                        };
                        Application.Current.Resources.MergedDictionaries.Add(resource);
                    }
                }
                else
                {
                    Log.Error($"|ResourceMerger.UpdatePluginLanguages|Can't find plugin path <{location}> for <{plugin.Metadata.Name}>");
                }
                
            }
        }
    }
}