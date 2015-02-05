using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Wox.Core.Exception;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Core.Plugin.QueryDispatcher
{
    public class GenericQueryDispatcher : BaseQueryDispatcher
    {
        protected override List<PluginPair> GetPlugins(Query query)
        {
            return PluginManager.AllPlugins.Where(o => PluginManager.IsGenericPlugin(o.Metadata)).ToList();
        }
    }
}