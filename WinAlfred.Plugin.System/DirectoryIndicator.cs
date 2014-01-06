using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace WinAlfred.Plugin.System
{
    public class DirectoryIndicator : ISystemPlugin
    {
        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            if (string.IsNullOrEmpty(query.RawQuery)) return results;

            if (CheckIfDirectory(query.RawQuery))
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

        private bool CheckIfDirectory(string path)
        {
            return Directory.Exists(path);
        }

        public void Init(PluginInitContext context)
        {
        }

        public string Name
        {
            get
            {
                return "DirectoryIndicator";
            }
        }

        public string Description
        {
            get
            {
                return "DirectoryIndicator";
            }
        }
    }
}
