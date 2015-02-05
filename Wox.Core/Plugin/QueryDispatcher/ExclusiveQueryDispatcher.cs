using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Wox.Core.Exception;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Core.Plugin.QueryDispatcher
{
    public class ExclusiveQueryDispatcher : BaseQueryDispatcher
    {
        protected override List<PluginPair> GetPlugins(Query query)
        {
            List<PluginPair> pluginPairs = new List<PluginPair>();
            var exclusivePluginPair = PluginManager.GetExclusivePlugin(query) ??
                                      PluginManager.GetActionKeywordPlugin(query);
            if (exclusivePluginPair != null)
            {
                pluginPairs.Add(exclusivePluginPair);
            }

            return pluginPairs;
        }


    
    }
}
