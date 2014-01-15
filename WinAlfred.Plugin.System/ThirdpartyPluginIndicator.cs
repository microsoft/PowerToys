using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinAlfred.Plugin.System
{
    public class ThirdpartyPluginIndicator : BaseSystemPlugin
    {
        private List<PluginPair> allPlugins = new List<PluginPair>();
        private Action<string> changeQuery;

        protected override List<Result> QueryInternal(Query query)
        {
            List<Result> results = new List<Result>();
            if (string.IsNullOrEmpty(query.RawQuery)) return results;

            foreach (PluginMetadata metadata in allPlugins.Select(o=>o.Metadata))
            {
                if (metadata.ActionKeyword.StartsWith(query.RawQuery))
                {
                    PluginMetadata metadataCopy = metadata;
                    Result result = new Result
                    {
                        Title = metadata.ActionKeyword,
                        SubTitle = string.Format("press space to active {0} workflow",metadata.Name),
                        Score = 50,
                        IcoPath = "Images/work.png",
                        Action = () => changeQuery(metadataCopy.ActionKeyword + " "),
                        DontHideWinAlfredAfterSelect = true
                    };
                    results.Add(result); 
                }
            }
            return results;
        }

        protected override void InitInternal(PluginInitContext context)
        {
            allPlugins = context.Plugins;
            changeQuery = context.ChangeQuery;
        }


    }
}
