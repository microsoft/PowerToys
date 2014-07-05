using System;
using System.Collections.Generic;
using System.Linq;
using Wox.Helper;
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
            BasePluginLoader.ParsePluginsConfig();

            plugins.AddRange(new PythonPluginLoader().LoadPlugin());
            plugins.AddRange(new CSharpPluginLoader().LoadPlugin());
            plugins.AddRange(new ExecutablePluginLoader().LoadPlugin());

            Forker forker = new Forker();
            foreach (IPlugin plugin in plugins.Select(pluginPair => pluginPair.Plugin))
            {
                IPlugin plugin1 = plugin;
                PluginPair pluginPair = plugins.FirstOrDefault(o => o.Plugin == plugin1);
                if (pluginPair != null)
                {
                    PluginMetadata metadata = pluginPair.Metadata;
                    pluginPair.InitContext = new PluginInitContext()
                    {
                        CurrentPluginMetadata = metadata,
                        API = App.Window
                    };

                    forker.Fork(() => plugin1.Init(pluginPair.InitContext));
                }
            }

            forker.Join();
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
