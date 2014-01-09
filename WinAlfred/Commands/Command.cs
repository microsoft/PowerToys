using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WinAlfred.Helper;
using WinAlfred.Plugin;

namespace WinAlfred.Commands
{
    public class Command
    {
        private PluginCommand pluginCmd;
        private SystemCommand systemCmd;

        public Command(MainWindow window)
        {
           pluginCmd = new PluginCommand(window);
           systemCmd = new SystemCommand(window);
        }

        public void DispatchCommand(Query query)
        {
            systemCmd.Dispatch(query);
            pluginCmd.Dispatch(query);
        }
    }
}
