using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CSharp;
using Wox.Plugin;

namespace Wox.PluginLoader
{
    public static class Plugins
    {
        private static List<PluginPair> plugins = new List<PluginPair>();

        public static void Init()
        {
            plugins.Clear();
            BasePluginLoader.ParsePluginsConfig();

            plugins.AddRange(new PythonPluginLoader().LoadPlugin());
            plugins.AddRange(new CSharpPluginLoader().LoadPlugin());
            foreach (IPlugin plugin in plugins.Select(pluginPair => pluginPair.Plugin))
            {
                IPlugin plugin1 = plugin;
                PluginPair pluginPair = plugins.FirstOrDefault(o => o.Plugin == plugin1);
                if (pluginPair != null)
                {
                    PluginMetadata metadata = pluginPair.Metadata;
                    ThreadPool.QueueUserWorkItem(o => plugin1.Init(new PluginInitContext()
                    {
                        Plugins = plugins,
                        CurrentPluginMetadata = metadata,
                        ChangeQuery = s => App.Window.ChangeQuery(s),
                        CloseApp = App.Window.CloseApp,
                        HideApp = App.Window.HideApp,
                        ShowApp = () => App.Window.ShowApp(),
                        ShowMsg = (title, subTitle, iconPath) => App.Window.ShowMsg(title, subTitle, iconPath),
                        OpenSettingDialog = () => App.Window.OpenSettingDialog(),
                        ShowCurrentResultItemTooltip = (msg) => App.Window.ShowCurrentResultItemTooltip(msg)
                    }));
                }
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
