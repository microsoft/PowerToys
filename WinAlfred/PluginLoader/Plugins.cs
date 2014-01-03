using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WinAlfred.Plugin;

namespace WinAlfred.PluginLoader
{
    public static class Plugins
    {
        private static List<PluginPair> plugins = new List<PluginPair>();

        public static void Init()
        {
            plugins.Clear();
            plugins.AddRange(new PythonPluginLoader().LoadPlugin());
            plugins.AddRange(new CSharpPluginLoader().LoadPlugin());
        }

        public static List<PluginPair> AllPlugins
        {
            get { return plugins; }
        }
    }
}
