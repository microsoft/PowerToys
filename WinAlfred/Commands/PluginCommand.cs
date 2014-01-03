using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WinAlfred.Helper;
using WinAlfred.Plugin;
using WinAlfred.PluginLoader;

namespace WinAlfred.Commands
{
    public class PluginCommand
    {
        public void Dispatch(Query q)
        {
            //ThreadPool.QueueUserWorkItem(state =>
            //{
            foreach (PluginPair pair in Plugins.AllPlugins)
            {
                if (pair.Metadata.ActionKeyword == q.ActionName)
                {
                    try
                    {
                        pair.Plugin.Query(q).ForEach(o => o.PluginDirectory = pair.Metadata.PluginDirecotry);
                    }
                    catch (Exception queryException)
                    {
                        Log.Error(string.Format("Plugin {0} query failed: {1}", pair.Metadata.Name,
                            queryException.Message));
#if (DEBUG)
                        {
                            throw;
                        }
#endif
                    }
                }
            }
            resultCtrl.Dispatcher.Invoke(new Action(() =>
            {
                resultCtrl.AddResults(results.OrderByDescending(o => o.Score).ToList());
                resultCtrl.SelectFirst();
            }));
            //}); 
        }
    }
}
