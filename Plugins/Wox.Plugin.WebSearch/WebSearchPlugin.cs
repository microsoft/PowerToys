using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Wox.Core.UserSettings;
using Wox.Plugin.WebSearch.SuggestionSources;

namespace Wox.Plugin.WebSearch
{
    public class WebSearchPlugin : IPlugin, ISettingProvider
    {
        private PluginInitContext context;

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();

            Core.UserSettings.WebSearch webSearch =
                UserSettingStorage.Instance.WebSearches.FirstOrDefault(o => o.ActionWord == query.ActionName && o.Enabled);

            if (webSearch != null)
            {
                string keyword = query.ActionParameters.Count > 0 ? query.GetAllRemainingParameter() : "";
                string title = keyword;
                string subtitle = "Search " + webSearch.Title;
                if (string.IsNullOrEmpty(keyword))
                {
                    title = subtitle;
                    subtitle = null;
                }
                context.API.PushResults(query, context.CurrentPluginMetadata, new List<Result>()
                {
                    new Result()
                    {
                        Title = title,
                        SubTitle = subtitle,
                        Score = 6,
                        IcoPath = webSearch.IconPath,
                        Action = (c) =>
                        {
                            Process.Start(webSearch.Url.Replace("{q}", Uri.EscapeDataString(keyword)));
                            return true;
                        }
                    }
                });

                if (UserSettingStorage.Instance.EnableWebSearchSuggestion && !string.IsNullOrEmpty(keyword))
                {
                    ISuggestionSource sugg = SuggestionSourceFactory.GetSuggestionSource(
                            UserSettingStorage.Instance.WebSearchSuggestionSource);
                    if (sugg != null)
                    {
                        var result = sugg.GetSuggestions(keyword);
                        if (result != null)
                        {
                            context.API.PushResults(query, context.CurrentPluginMetadata,
                                result.Select(o => new Result()
                                {
                                    Title = o,
                                    SubTitle = subtitle,
                                    Score = 5,
                                    IcoPath = webSearch.IconPath,
                                    Action = (c) =>
                                    {
                                        Process.Start(webSearch.Url.Replace("{q}", Uri.EscapeDataString(o)));
                                        return true;
                                    }
                                }).ToList());
                        }
                    }
                }
            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;

            if (UserSettingStorage.Instance.WebSearches == null)
                UserSettingStorage.Instance.WebSearches = UserSettingStorage.Instance.LoadDefaultWebSearches();
        }

        #region ISettingProvider Members

        public System.Windows.Controls.Control CreateSettingPanel()
        {
            return new WebSearchesSetting();
        }

        #endregion
    }
}
