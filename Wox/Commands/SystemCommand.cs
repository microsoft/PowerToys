using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Wox.Infrastructure.MFTSearch;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;
using Wox.Plugin.SystemPlugins;
using Wox.PluginLoader;

namespace Wox.Commands
{
    public class SystemCommand : BaseCommand
    {
        private IEnumerable<PluginPair> allSytemPlugins = Plugins.AllPlugins.Where(o => o.Metadata.PluginType == PluginType.System);

        public override void Dispatch(Query query)
        {
            if (UserSettingStorage.Instance.WebSearches.Exists(o => o.ActionWord == query.ActionName && o.Enabled))
            {
                //websearch mode
                allSytemPlugins = new List<PluginPair>()
                {
                    allSytemPlugins.First(o => ((ISystemPlugin)o.Plugin).ID == "565B73353DBF4806919830B9202EE3BF")
                };
            }

            foreach (PluginPair pair in allSytemPlugins)
            {
                PluginPair pair1 = pair;
                ThreadPool.QueueUserWorkItem(state =>
                {
                    List<Result> results = pair1.Plugin.Query(query);
                    results.ForEach(o => { o.AutoAjustScore = true; });

                    App.Window.PushResults(query, pair1.Metadata, results);
                });
            }
        }
    }
}
