using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WinAlfred.Helper;
using WinAlfred.Plugin;
using WinAlfred.PluginLoader;

namespace WinAlfred.Commands
{
    public class PluginCommand : BaseCommand
    {
        public PluginCommand(MainWindow mainWindow)
            : base(mainWindow)
        {

        }

        public override void Dispatch(Query q)
        {

            foreach (PluginPair pair in Plugins.AllPlugins)
            {
                if (pair.Metadata.ActionKeyword == q.ActionName)
                {
                    PluginPair pair1 = pair;
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        try
                        {
                            List<Result> r = pair1.Plugin.Query(q);
                            r.ForEach(o =>
                            {
                                o.PluginDirectory = pair1.Metadata.PluginDirecotry;
                                o.OriginQuery = q;
                            });
                            UpdateResultView(r);
                        }
                        catch (Exception queryException)
                        {
                            Log.Error(string.Format("Plugin {0} query failed: {1}", pair1.Metadata.Name,
                                queryException.Message));
#if (DEBUG)
                            {
                                throw;
                            }
#endif
                        }
                    });
                }
            }
        }
    }
}
