using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Wox.Core.Exception;
using Wox.Core.UserSettings;
using Wox.Infrastructure.Logger;
using Wox.Plugin;

namespace Wox.Core.Plugin.QueryDispatcher
{
    public class WildcardPluginQueryDispatcher : IQueryDispatcher
    {
        private IEnumerable<PluginPair> allSytemPlugins = PluginManager.AllPlugins.Where(o => PluginManager.IsWildcardPlugin(o.Metadata));

        public void Dispatch(Query query)
        {
            var queryPlugins = allSytemPlugins;
            foreach (PluginPair pair in queryPlugins)
            {
                PluginPair pair1 = pair;
                ThreadPool.QueueUserWorkItem(state =>
                {
                    try
                    {
                        List<Result> results = pair1.Plugin.Query(query);
                        results.ForEach(o =>
                        {
                            o.PluginID = pair1.Metadata.ID;
                        });

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