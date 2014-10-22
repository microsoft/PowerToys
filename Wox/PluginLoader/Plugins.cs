using System;
using System.Collections.Generic;
using System.Linq;
using Wox.Helper;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;

namespace Wox.PluginLoader
{
    public static class Plugins
    {
        public static String DebuggerMode { get; private set; }
        private static List<PluginPair> plugins = new List<PluginPair>();

        public static void Init()
        {
            plugins.Clear();
            List<PluginMetadata> pluginMetadatas = PluginConfigLoader.ParsePluginsConfig();

            plugins.AddRange(new CSharpPluginLoader().LoadPlugin(pluginMetadatas));
            plugins.AddRange(new BasePluginLoader<PythonPlugin>().LoadPlugin(pluginMetadatas));

            Forker forker = new Forker();
            foreach (PluginPair pluginPair in plugins)
            {
                PluginPair pair = pluginPair;
                forker.Fork(() => pair.Plugin.Init(new PluginInitContext()
                {
                    CurrentPluginMetadata = pair.Metadata,
                    Proxy = HttpProxy.Instance,
                    API = App.Window
                }));
            }

            //if plugin init do heavy works, join here will block the UI
            //forker.Join();
        }

        public static List<PluginPair> AllPlugins
        {
            get
            {
                return plugins;
            }
        }

        public static bool HitThirdpartyKeyword(Query query)
        {
            if (string.IsNullOrEmpty(query.ActionName)) return false;

            return plugins.Any(o => o.Metadata.PluginType == PluginType.ThirdParty && o.Metadata.ActionKeyword == query.ActionName);
        }

        public static void ActivatePluginDebugger(string path)
        {
            DebuggerMode = path;
        }
    }
}
