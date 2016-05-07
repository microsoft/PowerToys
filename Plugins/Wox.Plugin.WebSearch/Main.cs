using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using JetBrains.Annotations;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Plugin.WebSearch.SuggestionSources;

namespace Wox.Plugin.WebSearch
{
    public class Main : IPlugin, ISettingProvider, IPluginI18n, IMultipleActionKeywords, ISavable, IResultUpdated
    {
        public PluginInitContext Context { get; private set; }

        private PluginJsonStorage<Settings> _storage;
        private Settings _settings;
        private CancellationTokenSource _updateSource;
        private CancellationToken _updateToken;

        public const string Images = "Images";
        public static string ImagesDirectory;

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
                const int waittime = 300;
                var task = Task.Run(() =>
                {
                    results.AddRange(ResultsFromSuggestions(keyword, subtitle, webSearch));

                }, _updateToken);

                if (!task.Wait(waittime))
                {
                    task.ContinueWith(_ => ResultsUpdated?.Invoke(this, new ResultUpdatedEventArgs
                    {
                        Results = results,
                        Query = query
                    }), _updateToken);
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

        static Main()
        {
            var plugins = Infrastructure.Wox.Plugins;
            var assemblyName = typeof(Main).Assembly.GetName().Name;
            var pluginDirectory = Path.Combine(Infrastructure.Wox.SettingsPath, plugins, assemblyName);
            ImagesDirectory = Path.Combine(pluginDirectory, Images);
        }

        public void Init(PluginInitContext context)
        {
            Context = context;

            _storage = new PluginJsonStorage<Settings>();
            _settings = _storage.Load();

            var pluginDirectory = context.CurrentPluginMetadata.PluginDirectory;
            var bundledImagesDirectory = Path.Combine(pluginDirectory, Images);
            Helper.ValidateDataDirectory(bundledImagesDirectory, ImagesDirectory);
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
