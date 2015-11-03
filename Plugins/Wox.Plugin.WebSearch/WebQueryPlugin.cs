using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Wox.Plugin.WebSearch.SuggestionSources;

namespace Wox.Plugin.WebSearch
{
    public class WebSearchPlugin : IPlugin, ISettingProvider, IPluginI18n, IInstantQuery, IExclusiveQuery
    {
        private PluginInitContext context;
        private IDisposable suggestionTimer;

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
                string subtitle = context.API.GetTranslation("wox_plugin_websearch_search") + " " + webSearch.Title;
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
                    if (suggestionTimer != null)
                    {
                        suggestionTimer.Dispose();
                    }
                    suggestionTimer = EasyTimer.SetTimeout(() => { QuerySuggestions(keyword, query, subtitle, webSearch); }, 350);
                }
            }

            return results;
        }

        private void QuerySuggestions(string keyword, Query query, string subtitle, WebSearch webSearch)
        {
            ISuggestionSource sugg = SuggestionSourceFactory.GetSuggestionSource(WebSearchStorage.Instance.WebSearchSuggestionSource, context);
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

        public string GetTranslatedPluginTitle()
        {
            return context.API.GetTranslation("wox_plugin_websearch_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return context.API.GetTranslation("wox_plugin_websearch_plugin_description");
        }

        public bool IsInstantQuery(string query) => false;

        public bool IsExclusiveQuery(Query query)
        {
            var strings = query.RawQuery.Split(' ');
            if (strings.Length > 1)
            {
                return WebSearchStorage.Instance.WebSearches.Exists(o => o.ActionWord == strings[0] && o.Enabled);
            }
            return false;
        }
    }
}
