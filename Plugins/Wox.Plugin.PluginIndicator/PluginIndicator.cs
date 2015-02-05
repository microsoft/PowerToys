using System.Collections.Generic;
using System.Linq;
using Wox.Core.Plugin;
using Wox.Core.UserSettings;

namespace Wox.Plugin.PluginIndicator
{
    public class PluginIndicator : IPlugin
    {
        private List<PluginPair> allPlugins = new List<PluginPair>();
        private PluginInitContext context;

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            if (allPlugins.Count == 0)
            {
                allPlugins = context.API.GetAllPlugins().Where(o => !PluginManager.IsGenericPlugin(o.Metadata)).ToList();
            }

            foreach (PluginMetadata metadata in allPlugins.Select(o => o.Metadata))
            {
                if (metadata.ActionKeyword.StartsWith(query.Search))
                {
                    PluginMetadata metadataCopy = metadata;
                    var customizedPluginConfig = UserSettingStorage.Instance.CustomizedPluginConfigs.FirstOrDefault(o => o.ID == metadataCopy.ID);
                    if (customizedPluginConfig != null && customizedPluginConfig.Disabled)
                    {
                        continue;
                    }

                    Result result = new Result
                    {
                        Title = metadata.ActionKeyword,
                        SubTitle = string.Format("Activate {0} plugin", metadata.Name),
                        Score = 100,
                        IcoPath = metadata.FullIcoPath,
                        Action = (c) =>
                        {
                            context.API.ChangeQuery(metadataCopy.ActionKeyword + " ");
                            return false;
                        },
                    };
                    results.Add(result);
                }
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
        }
    }
}
