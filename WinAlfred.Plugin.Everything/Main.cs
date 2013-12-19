using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinAlfred.Plugin.Everything
{
    public class Main : IPlugin
    {
        EverythingAPI api = new EverythingAPI();

        public string GetActionName()
        {
            return "ev";
        }

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            if (query.ActionParameters.Count > 0)
            {
                api.Search(query.ActionParameters[0]);
            }

            return results;
        }

        public void Init()
        {
            //init everything
        }
    }
}
