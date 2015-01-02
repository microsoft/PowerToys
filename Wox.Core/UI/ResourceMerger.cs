using System;
using System.Linq;
using System.Windows;
using Wox.Core.i18n;
using Wox.Core.Theme;

namespace Wox.Core.UI
{
    public class ResourceMerger
    {
        public static void ApplyResources()
        {
            var UIResourceType = typeof(IUIResource);
            var UIResources = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => p.IsClass && !p.IsAbstract && UIResourceType.IsAssignableFrom(p));

            Application.Current.Resources.MergedDictionaries.Clear();

            foreach (var uiResource in UIResources)
            {
                Application.Current.Resources.MergedDictionaries.Add(
                    ((IUIResource)Activator.CreateInstance(uiResource)).GetResourceDictionary());
            }
        }
    }
}
