using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using JetBrains.Annotations;
using Wox.Infrastructure.Storage;
using Wox.Plugin.WebSearch.SuggestionSources;

namespace Wox.Plugin.WebSearch
{
    public class WebSearchPlugin : IPlugin, ISettingProvider, IPluginI18n, IMultipleActionKeywords, ISavable, IResultUpdated
    {
        public PluginInitContext Context { get; private set; }

        private PluginJsonStorage<Settings> _storage;
        private Settings _settings;
        private CancellationTokenSource _updateSource;
        private CancellationToken _updateToken;

        public const string ImageDirectory = "Images";
        public static string PluginDirectory;

        public void Save()
        {
            _storage.Save();
        }

        public List<Result> Query(Query query)
        {
            _updateSource?.Cancel();
            _updateSource = new CancellationTokenSource();
            _updateToken = _updateSource.Token;

            WebSearch webSearch =
                _settings.WebSearches.FirstOrDefault(o => o.ActionKeyword == query.ActionKeyword && o.Enabled);

            if (webSearch != null)
            {
                string keyword = query.Search;
                string title = keyword;
                string subtitle = Context.API.GetTranslation("wox_plugin_websearch_search") + " " + webSearch.Title;
                if (string.IsNullOrEmpty(keyword))
                {
                    var result = new Result
                    {
                        Title = subtitle,
                        SubTitle = string.Empty,
                        IcoPath = webSearch.IconPath
                    };
                    return new List<Result> { result };
                }
                else
                {
                    var results = new List<Result>();
                    var result = new Result
                    {
                        Title = title,
                        SubTitle = subtitle,
                        Score = 6,
                        IcoPath = webSearch.IconPath,
                        Action = c =>
                        {
                            Process.Start(webSearch.Url.Replace("{q}", Uri.EscapeDataString(keyword)));
                            return true;
                        }
                    };
                    results.Add(result);
                    UpdateResultsFromSuggestion(results, keyword, subtitle, webSearch, query);
                    return results;
                }
            }
            else
            {
                return new List<Result>();
            }
        }

        private void UpdateResultsFromSuggestion(List<Result> results, string keyword, string subtitle, WebSearch webSearch, Query query)
        {
            if (_settings.EnableWebSearchSuggestion)
            {
                var waittime = 300;
                var fastDomain = Task.Factory.StartNew(() =>
                {
                    var ping = new Ping();
                    var source = SuggestionSource.GetSuggestionSource(_settings.WebSearchSuggestionSource, Context);
                    ping.Send(source.Domain);
                }, _updateToken).Wait(waittime);
                if (fastDomain)
                {
                    results.AddRange(ResultsFromSuggestions(keyword, subtitle, webSearch));
                }
                else
                {
                    Task.Factory.StartNew(() =>
                    {
                        results.AddRange(ResultsFromSuggestions(keyword, subtitle, webSearch));
                        ResultsUpdated?.Invoke(this, new ResultUpdatedEventHandlerArgs
                        {
                            Results = results,
                            Query = query
                        });
                    }, _updateToken);
                }
            }
        }

        private IEnumerable<Result> ResultsFromSuggestions(string keyword, string subtitle, WebSearch webSearch)
        {
            var source = SuggestionSource.GetSuggestionSource(_settings.WebSearchSuggestionSource, Context);
            var suggestions = source?.GetSuggestions(keyword);
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
            PluginDirectory = Context.CurrentPluginMetadata.PluginDirectory;
            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();
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
        public event ResultUpdatedEventHandler ResultsUpdated;
    }
}
