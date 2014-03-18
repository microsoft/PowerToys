using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.Helper;
using Wox.Plugin;

namespace Wox.Commands
{
    internal static class CommandFactory
    {
        private static PluginCommand pluginCmd;
        private static SystemCommand systemCmd;

        public static void DispatchCommand(Query query)
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

            systemCmd.Dispatch(query);
            pluginCmd.Dispatch(query);
        }
    }
}
