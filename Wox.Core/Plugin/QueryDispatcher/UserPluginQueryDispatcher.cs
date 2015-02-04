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
    public class UserPluginQueryDispatcher : BaseQueryDispatcher
    {
        protected override List<PluginPair> GetPlugins(Query query)
        {
            List<PluginPair> plugins = new List<PluginPair>();
            //only first plugin that matches action keyword will get executed
            PluginPair userPlugin = PluginManager.AllPlugins.FirstOrDefault(o => o.Metadata.ActionKeyword == query.GetActionKeyword());
            if (userPlugin != null)
            {
                var customizedPluginConfig = UserSettingStorage.Instance.
                    CustomizedPluginConfigs.FirstOrDefault(o => o.ID == userPlugin.Metadata.ID);
                if (customizedPluginConfig != null && customizedPluginConfig.Disabled)
                {
                    //need to stop the loading animation
                    PluginManager.API.StopLoadingBar();
                }
                else
                {
                    plugins.Add(userPlugin);
                }
            }

            return plugins;
        }
    }
}
