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
    public class UserPluginQueryDispatcher : IQueryDispatcher
    {
        public void Dispatch(Query query)
        {
            PluginPair userPlugin = PluginManager.AllPlugins.FirstOrDefault(o => o.Metadata.ActionKeyword == query.GetActionKeyword());
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
                        results.ForEach(o =>
                        {
                            o.PluginID = userPlugin.Metadata.ID;
                        });
                        PluginManager.API.PushResults(query, userPlugin.Metadata, results);
                    }
                    catch (System.Exception e)
                    {
                        throw new WoxPluginException(userPlugin.Metadata.Name, e);
                    }
                });
            }
        }
    }
}
