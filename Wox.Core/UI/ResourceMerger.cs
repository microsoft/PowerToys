using System;
using System.Linq;
using System.Windows;
using Wox.Core.i18n;
using Wox.Core.Plugin;
using Wox.Plugin;

namespace Wox.Core.UI
{
    public static class ResourceMerger
    {
        private static void RemoveResource(string resourceDirectoryName)
        {
            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
            foreach (var resource in mergedDictionaries)
            {
                int directoryPosition = resource.Source.Segments.Length - 2;
                string currentDirectoryName = resource.Source.Segments[directoryPosition];
                if (currentDirectoryName == resourceDirectoryName)
                {
                    mergedDictionaries.Remove(resource);
                    break;
                }
            }
        }

        public static void ApplyThemeResource(Theme.Theme t)
        {
            RemoveResource(Theme.Theme.DirectoryName);
            Application.Current.Resources.MergedDictionaries.Add(t.GetResourceDictionary());
        }

        public static void ApplyLanguageResources(Internationalization i)
        {
            RemoveResource(Internationalization.DirectoryName);
            Application.Current.Resources.MergedDictionaries.Add(i.GetResourceDictionary());
        }

        internal static void ApplyPluginLanguages()
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