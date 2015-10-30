using System;
using System.Windows;
using Wox.Core.i18n;
using Wox.Plugin;

namespace Wox.Core.UI
{
    public class ResourceMerger
    {
        internal static void ApplyResources()
        {
            Application.Current.Resources.MergedDictionaries.Clear();
            ApplyPluginLanguages();
            ApplyThemeAndLanguageResources();
        }

        internal static void ApplyThemeAndLanguageResources()
        {
            var UIResources = AssemblyHelper.LoadInterfacesFromAppDomain<IUIResource>();
            foreach (var uiResource in UIResources)
            {
                Application.Current.Resources.MergedDictionaries.Add(uiResource.GetResourceDictionary());
            }
        }

        internal static void ApplyPluginLanguages()
        {
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