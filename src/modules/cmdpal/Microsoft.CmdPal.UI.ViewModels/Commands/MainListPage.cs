// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.State;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.MainPage;

/// <summary>
/// This class encapsulates the data we load from built-in providers and extensions to use within the same extension-UI system for a <see cref="ListPage"/>.
/// TODO: Need to think about how we structure/interop for the page -> section -> item between the main setup, the extensions, and our viewmodels.
/// </summary>
public partial class MainListPage : DynamicListPage,
    IRecipient<ClearSearchMessage>,
    IRecipient<UpdateFallbackItemsMessage>, IDisposable
{
    private readonly TopLevelCommandManager _tlcManager;
    private readonly AliasManager _aliasManager;
    private readonly SettingsModel _settings;
    private readonly AppStateModel _appStateModel;
    private List<Scored<IListItem>>? _filteredItems;
    private List<Scored<IListItem>>? _filteredApps;

    // Keep as IEnumerable for deferred execution. Fallback item titles are updated
    // asynchronously, so scoring must happen lazily when GetItems is called.
    private IEnumerable<Scored<IListItem>>? _scoredFallbackItems;
    private IEnumerable<Scored<IListItem>>? _fallbackItems;
    private bool _includeApps;
    private bool _filteredItemsIncludesApps;
    private int _appResultLimit = 10;

    private InterlockedBoolean _refreshRunning;
    private InterlockedBoolean _refreshRequested;

    private CancellationTokenSource? _cancellationTokenSource;

    public MainListPage(TopLevelCommandManager topLevelCommandManager, SettingsModel settings, AliasManager aliasManager, AppStateModel appStateModel)
    {
        Title = Resources.builtin_home_name;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.scale-200.png");
        PlaceholderText = Properties.Resources.builtin_main_list_page_searchbar_placeholder;

        _settings = settings;
        _aliasManager = aliasManager;
        _appStateModel = appStateModel;
        _tlcManager = topLevelCommandManager;
        _tlcManager.PropertyChanged += TlcManager_PropertyChanged;
        _tlcManager.TopLevelCommands.CollectionChanged += Commands_CollectionChanged;

        // The all apps page will kick off a BG thread to start loading apps.
        // We just want to know when it is done.
        var allApps = AllAppsCommandProvider.Page;
        allApps.PropChanged += (s, p) =>
            {
                if (p.PropertyName == nameof(allApps.IsLoading))
                {
                    IsLoading = ActuallyLoading();
                }
            };

        WeakReferenceMessenger.Default.Register<ClearSearchMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateFallbackItemsMessage>(this);

        settings.SettingsChanged += SettingsChangedHandler;
        HotReloadSettings(settings);
        _includeApps = _tlcManager.IsProviderActive(AllAppsCommandProvider.WellKnownId);

        IsLoading = true;
    }

    private void TlcManager_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsLoading))
        {
            IsLoading = ActuallyLoading();
        }
    }

    private void Commands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _includeApps = _tlcManager.IsProviderActive(AllAppsCommandProvider.WellKnownId);
        if (_includeApps != _filteredItemsIncludesApps)
        {
            ReapplySearchInBackground();
        }
        else
        {
            RaiseItemsChanged();
        }
    }

    private void ReapplySearchInBackground()
    {
        _refreshRequested.Set();
        if (!_refreshRunning.Set())
        {
            return;
        }

        _ = Task.Run(RunRefreshLoop);
    }

    private void RunRefreshLoop()
    {
        try
        {
            do
            {
                _refreshRequested.Clear();
                lock (_tlcManager.TopLevelCommands)
                {
                    if (_filteredItemsIncludesApps == _includeApps)
                    {
                        break;
                    }
                }

                var currentSearchText = SearchText;
                UpdateSearchText(currentSearchText, currentSearchText);
            }
            while (_refreshRequested.Value);
        }
        catch (Exception e)
        {
            Logger.LogError("Failed to reload search", e);
        }
        finally
        {
            _refreshRunning.Clear();
            if (_refreshRequested.Value && _refreshRunning.Set())
            {
                _ = Task.Run(RunRefreshLoop);
            }
        }
    }

    public override IListItem[] GetItems()
    {
        lock (_tlcManager.TopLevelCommands)
        {
            // Either return the top-level commands (no search text), or the merged and
            // filtered results.
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return _tlcManager.TopLevelCommands
                    .Where(tlc => !tlc.IsFallback && !string.IsNullOrEmpty(tlc.Title))
                    .ToArray();
            }
            else
            {
                var validScoredFallbacks = _scoredFallbackItems?
                    .Where(s => !string.IsNullOrWhiteSpace(s.Item.Title))
                    .ToList();

                var validFallbacks = _fallbackItems?
                    .Where(s => !string.IsNullOrWhiteSpace(s.Item.Title))
                    .ToList();

                return MainListPageResultFactory.Create(
                    _filteredItems,
                    validScoredFallbacks,
                    _filteredApps,
                    validFallbacks,
                    _appResultLimit);
            }
        }
    }

    private void ClearResults()
    {
        _filteredItems = null;
        _filteredApps = null;
        _fallbackItems = null;
        _scoredFallbackItems = null;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        var timer = new Stopwatch();
        timer.Start();

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();

        var token = _cancellationTokenSource.Token;
        if (token.IsCancellationRequested)
        {
            return;
        }

        // Handle changes to the filter text here
        if (!string.IsNullOrEmpty(SearchText))
        {
            var aliases = _aliasManager;

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (aliases.CheckAlias(newSearch))
            {
                if (_filteredItemsIncludesApps != _includeApps)
                {
                    lock (_tlcManager.TopLevelCommands)
                    {
                        _filteredItemsIncludesApps = _includeApps;
                        ClearResults();
                    }
                }

                return;
            }
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        var commands = _tlcManager.TopLevelCommands;
        lock (commands)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            // prefilter fallbacks
            var globalFallbacks = _settings.GetGlobalFallbacks();
            var specialFallbacks = new List<TopLevelViewModel>(globalFallbacks.Length);
            var commonFallbacks = new List<TopLevelViewModel>();

            foreach (var s in commands)
            {
                if (!s.IsFallback)
                {
                    continue;
                }

                if (globalFallbacks.Contains(s.Id))
                {
                    specialFallbacks.Add(s);
                }
                else
                {
                    commonFallbacks.Add(s);
                }
            }

            // start update of fallbacks; update special fallbacks separately,
            // so they can finish faster
            UpdateFallbacks(SearchText, specialFallbacks, token);
            UpdateFallbacks(SearchText, commonFallbacks, token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            // Cleared out the filter text? easy. Reset _filteredItems, and bail out.
            if (string.IsNullOrEmpty(newSearch))
            {
                _filteredItemsIncludesApps = _includeApps;
                ClearResults();
                RaiseItemsChanged(commands.Count);
                return;
            }

            // If the new string doesn't start with the old string, then we can't
            // re-use previous results. Reset _filteredItems, and keep er moving.
            if (!newSearch.StartsWith(oldSearch, StringComparison.CurrentCultureIgnoreCase))
            {
                ClearResults();
            }

            // If the internal state has changed, reset _filteredItems to reset the list.
            if (_filteredItemsIncludesApps != _includeApps)
            {
                ClearResults();
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            var newFilteredItems = Enumerable.Empty<IListItem>();
            var newFallbacks = Enumerable.Empty<IListItem>();
            var newApps = Enumerable.Empty<IListItem>();

            if (_filteredItems is not null)
            {
                newFilteredItems = _filteredItems.Select(s => s.Item);
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (_filteredApps is not null)
            {
                newApps = _filteredApps.Select(s => s.Item);
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (_fallbackItems is not null)
            {
                newFallbacks = _fallbackItems.Select(s => s.Item);
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            // If we don't have any previous filter results to work with, start
            // with a list of all our commands & apps.
            if (!newFilteredItems.Any() && !newApps.Any())
            {
                newFilteredItems = commands.Where(s => !s.IsFallback);

                // Fallbacks are always included in the list, even if they
                // don't match the search text. But we don't want to
                // consider them when filtering the list.
                newFallbacks = commonFallbacks;

                if (token.IsCancellationRequested)
                {
                    return;
                }

                _filteredItemsIncludesApps = _includeApps;

                if (_includeApps)
                {
                    var allNewApps = AllAppsCommandProvider.Page.GetItems().ToList();

                    // We need to remove pinned apps from allNewApps so they don't show twice.
                    var pinnedApps = PinnedAppsManager.Instance.GetPinnedAppIdentifiers();

                    if (pinnedApps.Length > 0)
                    {
                        newApps = allNewApps.Where(w =>
                            pinnedApps.IndexOf(((AppListItem)w).AppIdentifier) < 0);
                    }
                    else
                    {
                        newApps = allNewApps;
                    }
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }
            }

            var history = _appStateModel.RecentCommands!;
            Func<string, IListItem, int> scoreItem = (a, b) => { return ScoreTopLevelItem(a, b, history); };

            // Produce a list of everything that matches the current filter.
            _filteredItems = [.. ListHelpers.FilterListWithScores<IListItem>(newFilteredItems ?? [], SearchText, scoreItem)];

            if (token.IsCancellationRequested)
            {
                return;
            }

            IEnumerable<IListItem> newFallbacksForScoring = commands.Where(s => s.IsFallback && globalFallbacks.Contains(s.Id));

            if (token.IsCancellationRequested)
            {
                return;
            }

            _scoredFallbackItems = ListHelpers.FilterListWithScores<IListItem>(newFallbacksForScoring ?? [], SearchText, scoreItem);

            if (token.IsCancellationRequested)
            {
                return;
            }

            Func<string, IListItem, int> scoreFallbackItem = (a, b) => { return ScoreFallbackItem(a, b, _settings.FallbackRanks); };
            _fallbackItems = [.. ListHelpers.FilterListWithScores<IListItem>(newFallbacks ?? [], SearchText, scoreFallbackItem)];

            if (token.IsCancellationRequested)
            {
                return;
            }

            // Produce a list of filtered apps with the appropriate limit
            if (newApps.Any())
            {
                var scoredApps = ListHelpers.FilterListWithScores<IListItem>(newApps, SearchText, scoreItem);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                // We'll apply this limit in the GetItems method after merging with commands
                // but we need to know the limit now to avoid re-scoring apps
                var appLimit = AllAppsCommandProvider.TopLevelResultLimit;

                _filteredApps = [.. scoredApps];

                if (token.IsCancellationRequested)
                {
                    return;
                }
            }

            RaiseItemsChanged();

            timer.Stop();
            Logger.LogDebug($"Filter with '{newSearch}' in {timer.ElapsedMilliseconds}ms");
        }
    }

    private void UpdateFallbacks(string newSearch, IReadOnlyList<TopLevelViewModel> commands, CancellationToken token)
    {
        _ = Task.Run(
            () =>
        {
            var needsToUpdate = false;

            foreach (var command in commands)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var changedVisibility = command.SafeUpdateFallbackTextSynchronous(newSearch);
                needsToUpdate = needsToUpdate || changedVisibility;
            }

            if (needsToUpdate)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                RaiseItemsChanged();
            }
        },
            token);
    }

    private bool ActuallyLoading()
    {
        var allApps = AllAppsCommandProvider.Page;
        return allApps.IsLoading || _tlcManager.IsLoading;
    }

    // Almost verbatim ListHelpers.ScoreListItem, but also accounting for the
    // fact that we want fallback handlers down-weighted, so that they don't
    // _always_ show up first.
    internal static int ScoreTopLevelItem(string query, IListItem topLevelOrAppItem, IRecentCommandsManager history)
    {
        var title = topLevelOrAppItem.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            return 0;
        }

        var isWhiteSpace = string.IsNullOrWhiteSpace(query);

        var isFallback = false;
        var isAliasSubstringMatch = false;
        var isAliasMatch = false;
        var id = IdForTopLevelOrAppItem(topLevelOrAppItem);

        var extensionDisplayName = string.Empty;
        if (topLevelOrAppItem is TopLevelViewModel topLevel)
        {
            isFallback = topLevel.IsFallback;
            if (topLevel.HasAlias)
            {
                var alias = topLevel.AliasText;
                isAliasMatch = alias == query;
                isAliasSubstringMatch = isAliasMatch || alias.StartsWith(query, StringComparison.CurrentCultureIgnoreCase);
            }

            extensionDisplayName = topLevel.ExtensionHost?.Extension?.PackageDisplayName ?? string.Empty;
        }

        // StringMatcher.FuzzySearch will absolutely BEEF IT if you give it a
        // whitespace-only query.
        //
        // in that scenario, we'll just use a simple string contains for the
        // query. Maybe someone is really looking for things with a space in
        // them, I don't know.

        // Title:
        // * whitespace query: 1 point
        // * otherwise full weight match
        var nameMatch = isWhiteSpace ?
            (title.Contains(query) ? 1 : 0) :
               FuzzyStringMatcher.ScoreFuzzy(query, title);

        // Subtitle:
        // * whitespace query: 1/2 point
        // * otherwise ~half weight match. Minus a bit, because subtitles tend to be longer
        var descriptionMatch = isWhiteSpace ?
            (topLevelOrAppItem.Subtitle.Contains(query) ? .5 : 0) :
            (FuzzyStringMatcher.ScoreFuzzy(query, topLevelOrAppItem.Subtitle) - 4) / 2.0;

        // Extension title: despite not being visible, give the extension name itself some weight
        // * whitespace query: 0 points
        // * otherwise more weight than a subtitle, but not much
        var extensionTitleMatch = isWhiteSpace ? 0 : FuzzyStringMatcher.ScoreFuzzy(query, extensionDisplayName) / 1.5;

        var scores = new[]
        {
             nameMatch,
             descriptionMatch,
             isFallback ? 1 : 0, // Always give fallbacks a chance
        };
        var max = scores.Max();

        // _Add_ the extension name. This will bubble items that match both
        // title and extension name up above ones that just match title.
        // e.g. "git" will up-weight "GitHub searches" from the GitHub extension
        // above "git" from "whatever"
        max = max + extensionTitleMatch;

        var matchSomething = max
            + (isAliasMatch ? 9001 : (isAliasSubstringMatch ? 1 : 0));

        // If we matched title, subtitle, or alias (something real), then
        // here we add the recent command weight boost
        //
        // Otherwise something like `x` will still match everything you've run before
        var finalScore = matchSomething * 10;
        if (matchSomething > 0)
        {
            var recentWeightBoost = history.GetCommandHistoryWeight(id);
            finalScore += recentWeightBoost;
        }

        return (int)finalScore;
    }

    internal static int ScoreFallbackItem(string query, IListItem topLevelOrAppItem, string[] fallbackRanks)
    {
        // Default to 1 so it always shows in list.
        var finalScore = 1;

        if (topLevelOrAppItem is TopLevelViewModel topLevelViewModel)
        {
            var index = Array.IndexOf(fallbackRanks, topLevelViewModel.Id);

            if (index >= 0)
            {
                finalScore = fallbackRanks.Length - index + 1;
            }
        }

        return finalScore;
    }

    public void UpdateHistory(IListItem topLevelOrAppItem)
    {
        var id = IdForTopLevelOrAppItem(topLevelOrAppItem);
        var history = _appStateModel.RecentCommands;
        history.AddHistoryItem(id);
        AppStateModel.SaveState(_appStateModel);
    }

    private static string IdForTopLevelOrAppItem(IListItem topLevelOrAppItem)
    {
        if (topLevelOrAppItem is TopLevelViewModel topLevel)
        {
            return topLevel.Id;
        }
        else
        {
            // we've got an app here
            return topLevelOrAppItem.Command?.Id ?? string.Empty;
        }
    }

    public void Receive(ClearSearchMessage message) => SearchText = string.Empty;

    public void Receive(UpdateFallbackItemsMessage message) => RaiseItemsChanged(_tlcManager.TopLevelCommands.Count);

    private void SettingsChangedHandler(SettingsModel sender, object? args) => HotReloadSettings(sender);

    private void HotReloadSettings(SettingsModel settings) => ShowDetails = settings.ShowAppDetails;

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();

        _tlcManager.PropertyChanged -= TlcManager_PropertyChanged;
        _tlcManager.TopLevelCommands.CollectionChanged -= Commands_CollectionChanged;

        if (_settings is not null)
        {
            _settings.SettingsChanged -= SettingsChangedHandler;
        }

        WeakReferenceMessenger.Default.UnregisterAll(this);
        GC.SuppressFinalize(this);
    }
}
