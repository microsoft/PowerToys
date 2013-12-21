using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinAlfred.Plugin.System
{
    public class Main : IPlugin
    {
        List<Result> results = new List<Result>();

        public List<Result> Query(Query query)
        {
            results.Clear();

            if (query.ActionParameters.Count == 0)
            {
                results.Add(new Result
                {
                    Title = "Shutdown",
                    SubTitle = "shutdown your computer",
                    Score = 100,
                    Action = () =>
                    {
                        
                    }
                });
            }
            return results;
        }

        public void Init()
        {
        }
    }
}
