using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;

namespace Wox.Core.Plugin.QueryDispatcher
{
    public class UserPluginQueryDispatcher : IQueryDispatcher
    {
        public void Dispatch(Query query)
        {
            PluginPair userPlugin = PluginManager.AllPlugins.FirstOrDefault(o => o.Metadata.ActionKeyword == query.ActionName);
            if (userPlugin != null && !string.IsNullOrEmpty(userPlugin.Metadata.ActionKeyword))
            {
                var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == userPlugin.Metadata.ID);
                if (customizedPluginConfig != null && customizedPluginConfig.Disabled)
                {
                    //need to stop the loading animation
                    PluginManager.API.StopLoadingBar();
                    return;
                }

                ThreadPool.QueueUserWorkItem(t =>
                {
                    try
                    {
                        List<Result> results = userPlugin.Plugin.Query(query) ?? new List<Result>();
                        PluginManager.API.PushResults(query,userPlugin.Metadata,results);
                    }
                    catch (Exception queryException)
                    {
                        Log.Error(string.Format("Plugin {0} query failed: {1}", userPlugin.Metadata.Name,
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
