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
    public class RegularPluginQueryDispatcher : IQueryDispatcher
    {
        public void Dispatch(Query query)
        {
            PluginPair regularPlugin = PluginManager.AllPlugins.FirstOrDefault(o => o.Metadata.ActionKeyword == query.ActionName);
            if (regularPlugin != null && !string.IsNullOrEmpty(regularPlugin.Metadata.ActionKeyword))
            {
                var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == regularPlugin.Metadata.ID);
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
                        List<Result> results = regularPlugin.Plugin.Query(query) ?? new List<Result>();
                        PluginManager.API.PushResults(query,regularPlugin.Metadata,results);
                    }
                    catch (System.Exception e)
                    {
                        throw new WoxPluginException(regularPlugin.Metadata.Name, e);
                    }
                });
            }
        }
    }
}
