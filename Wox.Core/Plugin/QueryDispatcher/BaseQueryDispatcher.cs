using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Wox.Core.UserSettings;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Wox.Core.Plugin.QueryDispatcher
{
    public abstract class BaseQueryDispatcher : IQueryDispatcher
    {
        protected abstract List<PluginPair> GetPlugins(Query query);

        public void Dispatch(Query query)
        {
            foreach (PluginPair pair in GetPlugins(query))
            {
                var customizedPluginConfig = UserSettingStorage.Instance.
                    CustomizedPluginConfigs.FirstOrDefault(o => o.ID == pair.Metadata.ID);
                if (customizedPluginConfig != null && customizedPluginConfig.Disabled)
                {
                    return;
                }
                PluginPair localPair = pair;
                if (query.IsIntantQuery && PluginManager.IsInstantSearchPlugin(pair.Metadata))
                {
                    Debug.WriteLine(string.Format("Plugin {0} is executing instant search.", pair.Metadata.Name));
                    using (new Timeit("  => instant search took: "))
                    {
                        PluginManager.ExecutePluginQuery(localPair, query);
                    }
                }
                else
                {
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        PluginManager.ExecutePluginQuery(localPair, query);
                    });
                }
            }
        }
    }
}
