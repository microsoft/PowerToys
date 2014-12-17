using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Infrastructure.Storage.UserSettings;

namespace Wox.Plugin.SystemPlugins
{
    public class ThirdpartyPluginIndicator : BaseSystemPlugin
    {
        private List<PluginPair> allPlugins = new List<PluginPair>();
        private PluginInitContext context;

        protected override List<Result> QueryInternal(Query query)
        {
            List<Result> results = new List<Result>();
            if(allPlugins.Count == 0) allPlugins = context.API.GetAllPlugins();


            foreach (PluginMetadata metadata in allPlugins.Select(o => o.Metadata))
            {
                if (metadata.ActionKeyword.StartsWith(query.RawQuery))
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

            results.AddRange(UserSettingStorage.Instance.WebSearches.Where(o => o.ActionWord.StartsWith(query.RawQuery) && o.Enabled).Select(n => new Result()
            {
                Title = n.ActionWord,
                SubTitle = string.Format("Activate {0} web search", n.ActionWord),
                Score = 100,
                IcoPath = "Images/work.png",
                Action = (c) =>
                {
                    context.API.ChangeQuery(n.ActionWord + " ");
                    return false;
                }
            }));

            return results;
        }

        protected override void InitInternal(PluginInitContext context)
        {
            this.context = context;
        }


        public override string ID
        {
            get { return "6A122269676E40EB86EB543B945932B9"; }
        }

        public override string Name
        {
            get { return "Third-party Plugin Indicator"; }
        }

        public override string IcoPath
        {
            get { return @"Images\work.png"; }
        }

        public override string Description
        {
            get { return "Provide Third-party plugin actionword suggestion."; }
        }
    }
}
