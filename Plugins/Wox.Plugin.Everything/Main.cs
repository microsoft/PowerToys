using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.Everything
{
    public class Main : IPlugin
    {
        EverythingAPI api = new EverythingAPI();

        public List<Result> Query(Query query)
        {
            var results = new List<Result>();
            if (query.ActionParameters.Count > 0 && query.ActionParameters[0].Length > 0)
            {
                IEnumerable<string> enumerable = api.Search(query.ActionParameters[0]);
                foreach (string s in enumerable)
                {
                    Result r  = new Result();
                    r.Title = s;
                    results.Add(r);
                }
            }

            return results;
        }

        public void Init()
        {
            //init everything
        }
    }
}
