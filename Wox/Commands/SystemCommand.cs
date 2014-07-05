using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;
using Wox.PluginLoader;

namespace Wox.Commands
{
    public class SystemCommand : BaseCommand
    {
        public override void Dispatch(Query query)
        {
            foreach (PluginPair pair in Plugins.AllPlugins.Where(o => o.Metadata.PluginType == PluginType.System))
            {
                PluginPair pair1 = pair;

                ThreadPool.QueueUserWorkItem(state =>
                {
                    List<Result> results = pair1.Plugin.Query(query);
                    results.ForEach(o=> { o.AutoAjustScore = true; });

                    App.Window.PushResults(query,pair1.Metadata,results);
                });
            }
        }
    }
}
