using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Wox.Core.Exception;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Core.Plugin.QueryDispatcher
{
    public class SystemPluginQueryDispatcher : BaseQueryDispatcher
    {
        private readonly List<PluginPair> allSytemPlugins =
            PluginManager.AllPlugins.Where(o => PluginManager.IsSystemPlugin(o.Metadata)).ToList();

        protected override List<PluginPair> GetPlugins(Query query)
        {
            return allSytemPlugins;
        }
    }
}