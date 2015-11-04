using System.Collections.Generic;
using Wox.Core.Plugin;

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
            PluginManager.Init(App.Window);
        }
    }
}
