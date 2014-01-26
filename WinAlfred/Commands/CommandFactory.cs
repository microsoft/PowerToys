using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WinAlfred.Helper;
using WinAlfred.Plugin;

namespace WinAlfred.Commands
{
    internal static class CommandFactory
    {
        private static PluginCommand pluginCmd;
        private static SystemCommand systemCmd;

        public static void DispatchCommand(Query query, bool updateView = true)
        {
            //lazy init command instance.
            if (pluginCmd == null)
            {
                pluginCmd = new PluginCommand();
            }
            if (systemCmd == null)
            {
                systemCmd = new SystemCommand();
            }

            systemCmd.Dispatch(query,updateView);
            pluginCmd.Dispatch(query,updateView);
        }
    }
}
