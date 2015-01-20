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
            ApplyPluginLanguages();
            ApplyThemeAndLanguageResources();
        }

        private static void ApplyThemeAndLanguageResources()
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

        public static void ApplyPluginLanguages()
        {
            var pluginI18nType = typeof(IPluginI18n);
            var pluginI18ns = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => p.IsClass && !p.IsAbstract && pluginI18nType.IsAssignableFrom(p));

            foreach (var pluginI18n in pluginI18ns)
            {
                string languageFile = InternationalizationManager.Internationalization.GetLanguageFile(
                    ((IPluginI18n)Activator.CreateInstance(pluginI18n)).GetLanguagesFolder());
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