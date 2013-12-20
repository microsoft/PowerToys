using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WinAlfred.Helper;
using WinAlfred.Plugin;

namespace WinAlfred.PluginLoader
{
    public class CSharpPluginLoader:BasePluginLoader
    {
        private List<IPlugin> plugins = new List<IPlugin>();

        public override List<IPlugin> LoadPlugin()
        {
            return plugins;
        }
    }
}
