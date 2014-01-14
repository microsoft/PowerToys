using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WinAlfred.Plugin;
using WinAlfred.PluginLoader;

namespace WinAlfred.Commands
{
    public class SystemCommand : BaseCommand
    {
        private List<PluginPair> systemPlugins;

        public SystemCommand(MainWindow window)
            : base(window)
        {
            systemPlugins = Plugins.AllPlugins.Where(o => o.Metadata.PluginType == PluginType.System).ToList();
        }

        public override void Dispatch(Query query,bool updateView)
        {
            foreach (PluginPair pair in systemPlugins)
            {
                PluginPair pair1 = pair;
                ThreadPool.QueueUserWorkItem(state =>
                {
                    List<Result> results = pair1.Plugin.Query(query);
                    foreach (Result result in results)
                    {
                        result.PluginDirectory = pair1.Metadata.PluginDirecotry;
                        result.OriginQuery = query;
                    }
                    if(results.Count > 0 && updateView) UpdateResultView(results);
                });
            }
        }
    }
}
