using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Wox.Core.Plugin;
using Wox.Helper;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage.UserSettings;
using Wox.Plugin;

namespace Wox.Commands
{
    public class PluginCommand : BaseCommand
    {
        public override void Dispatch(Query query)
        {
            PluginPair thirdPlugin = PluginManager.AllPlugins.FirstOrDefault(o => o.Metadata.ActionKeyword == query.ActionName);
            if (thirdPlugin != null && !string.IsNullOrEmpty(thirdPlugin.Metadata.ActionKeyword))
            {
                var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == thirdPlugin.Metadata.ID);
                if (customizedPluginConfig != null && customizedPluginConfig.Disabled)
                {
                    //need to stop the loading animation
                    UpdateResultView(null);
                    return;
                }

                ThreadPool.QueueUserWorkItem(t =>
                {
                    try
                    {
                        List<Result> results = thirdPlugin.Plugin.Query(query) ?? new List<Result>();
                        App.Window.PushResults(query,thirdPlugin.Metadata,results);
                    }
                    catch (Exception queryException)
                    {
                        Log.Error(string.Format("Plugin {0} query failed: {1}", thirdPlugin.Metadata.Name,
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
