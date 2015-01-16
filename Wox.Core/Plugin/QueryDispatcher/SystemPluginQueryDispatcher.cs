using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Wox.Core.Exception;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Logger;
using Wox.Plugin;
//using Wox.Plugin.SystemPlugins;

namespace Wox.Core.Plugin.QueryDispatcher
{
    public class SystemPluginQueryDispatcher : IQueryDispatcher
    {
        private IEnumerable<PluginPair> allSytemPlugins = PluginManager.AllPlugins.Where(o => PluginManager.IsSystemPlugin(o.Metadata));

        public void Dispatch(Query query)
        {
            var queryPlugins = allSytemPlugins;
            if (UserSettingStorage.Instance.WebSearches.Exists(o => o.ActionWord == query.ActionName && o.Enabled))
            {
                //websearch mode
                queryPlugins = new List<PluginPair>()
                {
                    allSytemPlugins.First(o => o.Metadata.ID == "565B73353DBF4806919830B9202EE3BF")
                };
            }

            foreach (PluginPair pair in queryPlugins)
            {
                PluginPair pair1 = pair;
                ThreadPool.QueueUserWorkItem(state =>
                {
                    try
                    {
                        List<Result> results = pair1.Plugin.Query(query);
                        results.ForEach(o => { o.AutoAjustScore = true; });

                        PluginManager.API.PushResults(query, pair1.Metadata, results);
                    }
                    catch (System.Exception e)
                    {
                        throw new WoxPluginException(pair1.Metadata.Name,e);
                    }
                });
            }
        }
    }
}