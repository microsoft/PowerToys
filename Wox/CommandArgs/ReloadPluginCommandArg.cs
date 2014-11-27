using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.PluginLoader;

namespace Wox.CommandArgs
{
    public class ReloadPluginCommandArg : ICommandArg
    {
        public string Command
        {
            get { return "reloadplugin"; }
        }

        public void Execute(IList<string> args)
        {
            Plugins.Init();
        }
    }
}
