using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace WinAlfred.Plugin.System
{
    public class DirectoryIndicator : BaseSystemPlugin
    {
        protected override List<Result> QueryInternal(Query query)
        {
            List<Result> results = new List<Result>();
            if (string.IsNullOrEmpty(query.RawQuery)) return results;

            if (Directory.Exists(query.RawQuery))
            {
                Result result = new Result
                {
                    Title = "Open this directory",
                    SubTitle = string.Format("path: {0}", query.RawQuery),
                    Score = 50,
                    IcoPath = "Images/folder.png",
                    Action = () =>  Process.Start(query.RawQuery)
                };
                results.Add(result); 
            }

            return results;
        }

        protected override void InitInternal(PluginInitContext context)
        {
        }

    }
}
