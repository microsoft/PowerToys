using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace Wox.Helper
{
    public class ResourceMerger
    {
        public static void ApplyResources()
        {
            var languageResource = LanguageManager.GetResourceDictionary();
            var themeResource = ThemeManager.GetResourceDictionary();

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(languageResource);
            Application.Current.Resources.MergedDictionaries.Add(themeResource);
        }
    }
}
