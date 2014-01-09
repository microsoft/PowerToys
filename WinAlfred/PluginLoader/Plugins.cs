using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WinAlfred.Plugin;

namespace WinAlfred.PluginLoader
{
    public static class Plugins
    {
        private static List<PluginPair> plugins = new List<PluginPair>();

        public static void Init(MainWindow window)
        {
            plugins.Clear();
            plugins.AddRange(new PythonPluginLoader().LoadPlugin());
            plugins.AddRange(new CSharpPluginLoader().LoadPlugin());
            foreach (IPlugin plugin in plugins.Select(pluginPair => pluginPair.Plugin))
            {
                IPlugin plugin1 = plugin;
                //ThreadPool.QueueUserWorkItem(o =>);

                plugin1.Init(new PluginInitContext()
                {
                    Plugins = plugins,
                    ChangeQuery = s => window.ChangeQuery(s),
                    CloseApp = window.CloseApp,
                    HideApp = window.HideApp,
                    ShowApp = window.ShowApp,
                    ShowMsg = (title, subTitle, iconPath) => window.ShowMsg(title, subTitle, iconPath)
                });
            }
        }

        public static List<PluginPair> AllPlugins
        {
            get { return plugins; }
        }

        public static bool HitThirdpartyKeyword(Query query)
        {
            if (string.IsNullOrEmpty(query.ActionName)) return false;

            return plugins.Any(o => o.Metadata.PluginType == PluginType.ThirdParty && o.Metadata.ActionKeyword == query.ActionName);
        }
    }
}
