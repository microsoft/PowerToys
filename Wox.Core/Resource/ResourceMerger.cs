using System;
using System.IO;
using System.Linq;
using System.Windows;
using Wox.Core.i18n;
using Wox.Core.Plugin;
using Wox.Plugin;

namespace Wox.Core.UI
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

        public static void UpdateResource<T>(T t) where T : Resource
        {
            RemoveResource(t.DirectoryName);
            Application.Current.Resources.MergedDictionaries.Add(t.GetResourceDictionary());
        }

        internal static void UpdatePluginLanguages()
        {
            RemoveResource(PluginManager.DirectoryName);
            foreach (var languageFile in PluginManager.GetPluginsForInterface<IPluginI18n>().
                Select(plugin => InternationalizationManager.Instance.GetLanguageFile(((IPluginI18n)plugin.Plugin).GetLanguagesFolder())).
                Where(file => !string.IsNullOrEmpty(file)))
            {
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(languageFile, UriKind.Absolute)
                });
            }
        }
    }
}