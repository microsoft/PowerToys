using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinAlfred.Plugin.System
{
    public class ThirdpartyPluginIndicator : ISystemPlugin
    {
        private List<PluginPair> allPlugins = new List<PluginPair>();
        private Action<string> changeQuery;

        public List<Result> Query(Query query)
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
                        DontHideWinAlfredAfterAction = true
                    };
                    results.Add(result); 
                }
            }
            return results;
        }

        public void Init(PluginInitContext context)
        {
            allPlugins = context.Plugins;
            changeQuery = context.ChangeQuery;
        }

        public string Name {
            get
            {
                return "ThirdpartyPluginIndicator";
            }
        }

        public string Description
        {
            get
            {
                return "ThirdpartyPluginIndicator";
            }
        }


    }
}
