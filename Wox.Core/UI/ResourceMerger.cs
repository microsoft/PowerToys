using System;
using System.Linq;
using System.Windows;
using Wox.Core.i18n;
using Wox.Core.Theme;
using Wox.Plugin;

namespace Wox.Core.UI
{
    public class ResourceMerger
    {
        public static void ApplyResources()
        {
            Application.Current.Resources.MergedDictionaries.Clear();
            ApplyUIResources();
            ApplyPluginLanguages();
        }

        private static void ApplyUIResources()
        {
            var UIResourceType = typeof(IUIResource);
            var UIResources = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => p.IsClass && !p.IsAbstract && UIResourceType.IsAssignableFrom(p));

            foreach (var uiResource in UIResources)
            {
                Application.Current.Resources.MergedDictionaries.Add(
                    ((IUIResource)Activator.CreateInstance(uiResource)).GetResourceDictionary());
            }
        }

        private static void ApplyPluginLanguages()
        {
            var pluginI18nType = typeof(IPluginI18n);
            var pluginI18ns = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => p.IsClass && !p.IsAbstract && pluginI18nType.IsAssignableFrom(p));

            foreach (IPluginI18n pluginI18n in pluginI18ns)
            {
                string languageFile = InternationalizationManager.Internationalization.GetLanguageFile(pluginI18n.GetLanguagesFolder());
                Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(languageFile, UriKind.Absolute)
                });
            }
        }
    }
}