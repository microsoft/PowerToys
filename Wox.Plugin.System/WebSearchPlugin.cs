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
                string keyword = query.ActionParameters.Count > 0 ? query.GetAllRemainingParameter() : "";
                string title = string.Format("Search {0} for \"{1}\"", webSearch.Title, keyword);
                if (string.IsNullOrEmpty(keyword))
                {
                    title = "Search " + webSearch.Title;
                }
                results.Add(new Result()
                {
                    Title = title,
                    Score = 6,
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
