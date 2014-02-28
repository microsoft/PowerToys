using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Wox.Infrastructure;
using Wox.Infrastructure.UserSettings;

namespace Wox.Plugin.System
{
    public class WebSearchPlugin : BaseSystemPlugin
    {
        protected override List<Result> QueryInternal(Query query)
        {
            List<Result> results = new List<Result>();
            if (string.IsNullOrEmpty(query.ActionName)) return results;

            WebSearch webSearch =
                CommonStorage.Instance.UserSetting.WebSearches.FirstOrDefault(o => o.ActionWord == query.ActionName && o.Enabled);

            if (webSearch != null)
            {
                string keyword = query.ActionParameters.Count > 0 ? query.RawQuery.Substring(query.RawQuery.IndexOf(' ') + 1) : "";
                results.Add(new Result()
                {
                    Title = string.Format("Search {0} for \"{1}\"", webSearch.Title, keyword),
                    IcoPath = webSearch.IconPath,
                    Action = (c) =>
                    {
                        Process.Start(webSearch.Url.Replace("{q}", keyword));
                        return true;
                    }
                });
            }

            return results;
        }

        protected override void InitInternal(PluginInitContext context)
        {
        }
    }
}
