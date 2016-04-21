using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Controls;
using Wox.Infrastructure.Storage;
using Wox.Plugin.WebSearch.Annotations;
using Wox.Plugin.WebSearch.SuggestionSources;

namespace Wox.Plugin.WebSearch
{
    public class WebSearchPlugin : IPlugin, ISettingProvider, IPluginI18n, IInstantQuery, IMultipleActionKeywords
    {
        public PluginInitContext Context { get; private set; }

        private readonly PluginSettingsStorage<Settings> _storage;
        private readonly Settings _settings;

        public WebSearchPlugin()
        {
            _storage = new PluginSettingsStorage<Settings>();
            _settings = _storage.Load();
        }

        ~WebSearchPlugin()
        {
            _storage.Save();
        }

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            WebSearch webSearch =
                _settings.WebSearches.FirstOrDefault(o => o.ActionKeyword == query.ActionKeyword && o.Enabled);

            if (webSearch != null)
            {
                string keyword = query.Search;
                string title = keyword;
                string subtitle = Context.API.GetTranslation("wox_plugin_websearch_search") + " " + webSearch.Title;
                if (string.IsNullOrEmpty(keyword))
                {
                    title = subtitle;
                    subtitle = string.Empty;
                }
                var result = new Result
                {
                    Title = title,
                    SubTitle = subtitle,
                    Score = 6,
                    IcoPath = webSearch.IconPath,
                    Action = c =>
                    {
                        Process.Start(webSearch.Url.Replace("{q}", Uri.EscapeDataString(keyword ?? string.Empty)));
                        return true;
                    }
                };
                results.Add(result);

                if (_settings.EnableWebSearchSuggestion && !string.IsNullOrEmpty(keyword))
                {
                    // todo use Task.Wait when .net upgraded
                    results.AddRange(ResultsFromSuggestions(keyword, subtitle, webSearch));
                }
            }
            return results;
        }

        private IEnumerable<Result> ResultsFromSuggestions(string keyword, string subtitle, WebSearch webSearch)
        {
            ISuggestionSource sugg = SuggestionSourceFactory.GetSuggestionSource(_settings.WebSearchSuggestionSource, Context);
            var suggestions = sugg?.GetSuggestions(keyword);
            if (suggestions != null)
            {
                var resultsFromSuggestion = suggestions.Select(o => new Result
                {
                    Title = o,
                    SubTitle = subtitle,
                    Score = 5,
                    IcoPath = webSearch.IconPath,
                    Action = c =>
                    {
                        Process.Start(webSearch.Url.Replace("{q}", Uri.EscapeDataString(o)));
                        return true;
                    }
                });
                return resultsFromSuggestion;
            }
            return new List<Result>();
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
        }

        #region ISettingProvider Members

        public Control CreateSettingPanel()
        {
            return new WebSearchesSetting(this, _settings);
        }

        #endregion

        public string GetTranslatedPluginTitle()
        {
            return Context.API.GetTranslation("wox_plugin_websearch_plugin_name");
        }

        public string GetTranslatedPluginDescription()
        {
            return Context.API.GetTranslation("wox_plugin_websearch_plugin_description");
        }

        public bool IsInstantQuery(string query) => false;

        [NotifyPropertyChangedInvocator]
        public void NotifyActionKeywordsUpdated(string oldActionKeywords, string newActionKeywords)
        {
            ActionKeywordsChanged?.Invoke(this, new ActionKeywordsChangedEventArgs
            {
                OldActionKeyword = oldActionKeywords,
                NewActionKeyword = newActionKeywords
            });
        }

        [NotifyPropertyChangedInvocator]
        public void NotifyActionKeywordsAdded(string newActionKeywords)
        {
            ActionKeywordsChanged?.Invoke(this, new ActionKeywordsChangedEventArgs
            {
                NewActionKeyword = newActionKeywords
            });
        }

        public event ActionKeywordsChangedEventHandler ActionKeywordsChanged;
    }
}
