using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.Core.Plugin;
using Wox.Helper;
using Wox.Plugin;

namespace Wox.Commands
{
    internal static class CommandFactory
    {
        private static PluginCommand pluginCmd = new PluginCommand();
        private static SystemCommand systemCmd = new SystemCommand();

        public static void DispatchCommand(Query query)
        {
            if (PluginManager.HitThirdpartyKeyword(query))
            {
                pluginCmd.Dispatch(query);
            }
            else
            {
                systemCmd.Dispatch(query);                
            }
        }
    }
}
