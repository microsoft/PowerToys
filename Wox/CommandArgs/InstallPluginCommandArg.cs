using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Wox.Core.Plugin;
using Wox.Helper;

namespace Wox.CommandArgs
{
    public class InstallPluginCommandArg : ICommandArg
    {
        public string Command
        {
            get { return "installplugin"; }
        }

        public void Execute(IList<string> args)
        {
            if (args.Count > 0)
            {
                var path = args[0];
                if (!File.Exists(path))
                {
                    MessageBox.Show("Plugin " + path + " didn't exist");
                    return;
                }
                PluginManager.InstallPlugin(path);
            }
        }
    }
}