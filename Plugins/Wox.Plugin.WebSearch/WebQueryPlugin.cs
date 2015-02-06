using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Wox.Core.UserSettings;
using Wox.Plugin.Features;
using Wox.Plugin.WebSearch.SuggestionSources;

namespace Wox.Plugin.WebSearch
{
    public class WebSearchPlugin : IPlugin, ISettingProvider, IPluginI18n, IInstantQuery, IExclusiveQuery
    {
        private PluginInitContext context;

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            if (!query.Search.Contains(' '))
            {
                return results;
            }

            WebSearch webSearch =
                WebSearchStorage.Instance.WebSearches.FirstOrDefault(o => o.ActionWord == query.FirstSearch.Trim() && o.Enabled);

            if (webSearch != null)
            {
                string keyword = query.SecondToEndSearch;
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

                if (WebSearchStorage.Instance.EnableWebSearchSuggestion && !string.IsNullOrEmpty(keyword))
                {
                    ISuggestionSource sugg = SuggestionSourceFactory.GetSuggestionSource(
                            WebSearchStorage.Instance.WebSearchSuggestionSource);
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

            if (WebSearchStorage.Instance.WebSearches == null)
                WebSearchStorage.Instance.WebSearches = WebSearchStorage.Instance.LoadDefaultWebSearches();
        }

        #region ISettingProvider Members

        public System.Windows.Controls.Control CreateSettingPanel()
        {
            return new WebSearchesSetting(context);
        }

        #endregion

        public string GetLanguagesFolder()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Languages");
        }

        public bool IsInstantQuery(string query)
        {
            var strings = query.Split(' ');
            if (strings.Length > 1)
            {
                return WebSearchStorage.Instance.WebSearches.Exists(o => o.ActionWord == strings[0] && o.Enabled);
            }
            return false;
        }

        public bool IsExclusiveQuery(Query query)
        {
            return IsInstantQuery(query.RawQuery);
        }
    }
}
