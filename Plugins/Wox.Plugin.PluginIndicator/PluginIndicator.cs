using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Wox.Core.Plugin;
using Wox.Core.UserSettings;

namespace Wox.Plugin.PluginIndicator
{
    public class PluginIndicator : IPlugin, IPluginI18n
    {
        private PluginInitContext context;

        public List<Result> Query(Query query)
        {
            var results = from keyword in PluginManager.NonGlobalPlugins.Keys
                          where keyword.StartsWith(query.Terms[0])
                          let metadata = PluginManager.NonGlobalPlugins[keyword].Metadata
                          let customizedPluginConfig =
                              UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == metadata.ID)
                          where customizedPluginConfig == null || !customizedPluginConfig.Disabled
                          select new Result
                          {
                              Title = keyword,
                              SubTitle = $"Activate {metadata.Name} plugin",
                              Score = 100,
                              IcoPath = metadata.FullIcoPath,
                              Action = (c) =>
                              {
                                  context.API.ChangeQuery($"{keyword}{Plugin.Query.TermSeperater}");
                                  return false;
                              },
                          };
            return results.ToList();
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }

        public string GetLanguagesFolder()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Languages");
        }

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("wox_plugin_pluginindicator_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("wox_plugin_pluginindicator_plugin_description");
        }
    }
}
