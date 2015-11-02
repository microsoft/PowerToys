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

        public static void ApplyThemeResource()
        {
            RemoveResource(Theme.Theme.DirectoryName);
            ApplyUIResources();
        }

        public static void ApplyLanguageResources()
        {
            RemoveResource(Internationalization.DirectoryName);
            ApplyUIResources();
        }

        private static void ApplyUIResources()
        {
            var UIResources = AssemblyHelper.LoadInterfacesFromAppDomain<IUIResource>();
            foreach (var uiResource in UIResources)
            {
                Application.Current.Resources.MergedDictionaries.Add(uiResource.GetResourceDictionary());
            }
        }

        internal static void ApplyPluginLanguages()
        {
            RemoveResource(PluginManager.DirectoryName);
            var pluginI18ns = AssemblyHelper.LoadInterfacesFromAppDomain<IPluginI18n>();
            foreach (var pluginI18n in pluginI18ns)
            {
                string languageFile = InternationalizationManager.Instance.GetLanguageFile(pluginI18n.GetLanguagesFolder());
                if (!string.IsNullOrEmpty(languageFile))
                {
                    Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri(languageFile, UriKind.Absolute)
                    });
                }
            }
        }

      
    }
}