using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wox.Infrastructure;

namespace Wox.Plugin.System
{
    public class ThirdpartyPluginIndicator : BaseSystemPlugin
    {
        private List<PluginPair> allPlugins = new List<PluginPair>();
        private Action<string> changeQuery;

        protected override List<Result> QueryInternal(Query query)
        {
            List<Result> results = new List<Result>();
            if (string.IsNullOrEmpty(query.RawQuery)) return results;

            foreach (PluginMetadata metadata in allPlugins.Select(o => o.Metadata))
            {
                if (metadata.ActionKeyword.StartsWith(query.RawQuery))
                {
                    PluginMetadata metadataCopy = metadata;
                    Result result = new Result
                    {
                        Title = metadata.ActionKeyword,
                        SubTitle = string.Format("Activate {0} plugin", metadata.Name),
                        Score = 50,
                        IcoPath = "Images/work.png",
                        Action = () => changeQuery(metadataCopy.ActionKeyword + " "),
                        DontHideWoxAfterSelect = true
                    };
                    results.Add(result);
                }
            }

            results.AddRange(CommonStorage.Instance.UserSetting.WebSearches.Where(o => o.ActionWord.StartsWith(query.RawQuery)).Select(n => new Result()
            {

                Title = n.ActionWord,
                SubTitle = string.Format("Activate {0} plugin", n.ActionWord),
                Score = 50,
                IcoPath = "Images/work.png",
                Action = () => changeQuery(n.ActionWord + " "),
                DontHideWoxAfterSelect = true
            }));

            return results;
        }

        protected override void InitInternal(PluginInitContext context)
        {
            allPlugins = context.Plugins;
            changeQuery = context.ChangeQuery;
        }


    }
}
