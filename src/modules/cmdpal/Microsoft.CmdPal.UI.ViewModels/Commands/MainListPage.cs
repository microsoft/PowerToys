// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.Programs;
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
public sealed partial class MainListPage : DynamicListPage,
    IRecipient<ClearSearchMessage>,
    IRecipient<UpdateFallbackItemsMessage>, IDisposable
{
    private readonly TopLevelCommandManager _tlcManager;
    private readonly AliasManager _aliasManager;
    private readonly SettingsModel _settings;
    private readonly AppStateModel _appStateModel;
    private readonly ScoringFunction<IListItem> _scoringFunction;
    private readonly ScoringFunction<IListItem> _fallbackScoringFunction;
    private readonly IFuzzyMatcherProvider _fuzzyMatcherProvider;

    // Stable separator instances so that the VM cache and InPlaceUpdateList
    // recognise them across successive GetItems() calls
    private readonly Separator _resultsSeparator = new(Resources.results);
    private readonly Separator _fallbacksSeparator = new(Resources.fallbacks);

    private RoScored<IListItem>[]? _filteredItems;
    private RoScored<IListItem>[]? _filteredApps;

    // Keep as IEnumerable for deferred execution. Fallback item titles are updated
    // asynchronously, so scoring must happen lazily when GetItems is called.
    private IEnumerable<RoScored<IListItem>>? _scoredFallbackItems;
    private IEnumerable<RoScored<IListItem>>? _fallbackItems;

    private bool _includeApps;
    private bool _filteredItemsIncludesApps;

    private int AppResultLimit => AllAppsCommandProvider.TopLevelResultLimit;

    private InterlockedBoolean _refreshRunning;
    private InterlockedBoolean _refreshRequested;

    private CancellationTokenSource? _cancellationTokenSource;

    public MainListPage(
        TopLevelCommandManager topLevelCommandManager,
        SettingsModel settings,
        AliasManager aliasManager,
        AppStateModel appStateModel,
        IFuzzyMatcherProvider fuzzyMatcherProvider)
    {
        Title = Resources.builtin_home_name;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.scale-200.png");
        PlaceholderText = Properties.Resources.builtin_main_list_page_searchbar_placeholder;

        _settings = settings;
        _aliasManager = aliasManager;
        _appStateModel = appStateModel;
        _tlcManager = topLevelCommandManager;
        _fuzzyMatcherProvider = fuzzyMatcherProvider;
        _scoringFunction = (in query, item) => ScoreTopLevelItem(in query, item, _appStateModel.RecentCommands, _fuzzyMatcherProvider.Current);
        _fallbackScoringFunction = (in _, item) => ScoreFallbackItem(item, _settings.FallbackRanks);

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
                var allCommands = _tlcManager.TopLevelCommands;

                // First pass: count eligible commands
                var eligibleCount = 0;
                for (var i = 0; i < allCommands.Count; i++)
                {
                    var cmd = allCommands[i];
                    if (!cmd.IsFallback && !string.IsNullOrEmpty(cmd.Title))
                    {
                        eligibleCount++;
                    }
                }

                if (eligibleCount == 0)
                {
                    return [];
                }

                // +1 for the separator
                var result = new IListItem[eligibleCount + 1];
                result[0] = _resultsSeparator;

                // Second pass: populate
                var writeIndex = 1;
                for (var i = 0; i < allCommands.Count; i++)
                {
                    var cmd = allCommands[i];
                    if (!cmd.IsFallback && !string.IsNullOrEmpty(cmd.Title))
                    {
                        result[writeIndex++] = cmd;
                    }
                }

                return result;
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
                    _resultsSeparator,
                    _fallbacksSeparator,
                    AppResultLimit);
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
        var stopwatch = Stopwatch.StartNew();

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
                    var allNewApps = AllAppsCommandProvider.Page.GetItems().Cast<AppListItem>().ToList();

                    // We need to remove pinned apps from allNewApps so they don't show twice.
                    // Pinned app command IDs are stored in ProviderSettings.PinnedCommandIds.
                    _settings.ProviderSettings.TryGetValue(AllAppsCommandProvider.WellKnownId, out var providerSettings);
                    var pinnedCommandIds = providerSettings?.PinnedCommandIds;

                    if (pinnedCommandIds is not null && pinnedCommandIds.Count > 0)
                    {
                        newApps = allNewApps.Where(li => li.Command != null && !pinnedCommandIds.Contains(li.Command.Id));
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

            var searchQuery = _fuzzyMatcherProvider.Current.PrecomputeQuery(SearchText);

            // Produce a list of everything that matches the current filter.
            _filteredItems = InternalListHelpers.FilterListWithScores(newFilteredItems, searchQuery, _scoringFunction);

            if (token.IsCancellationRequested)
            {
                return;
            }

            IEnumerable<IListItem> newFallbacksForScoring = commands.Where(s => s.IsFallback && globalFallbacks.Contains(s.Id));
            _scoredFallbackItems = InternalListHelpers.FilterListWithScores(newFallbacksForScoring, searchQuery, _scoringFunction);

            if (token.IsCancellationRequested)
            {
                return;
            }

            _fallbackItems = InternalListHelpers.FilterListWithScores<IListItem>(newFallbacks ?? [], searchQuery, _fallbackScoringFunction);

            if (token.IsCancellationRequested)
            {
                return;
            }

            // Produce a list of filtered apps with the appropriate limit
            if (newApps.Any())
            {
                _filteredApps = InternalListHelpers.FilterListWithScores(newApps, searchQuery, _scoringFunction);

                if (token.IsCancellationRequested)
                {
                    return;
                }
            }

            var filterDoneTimestamp = stopwatch.ElapsedMilliseconds;
            Logger.LogDebug($"Filter with '{newSearch}' in {filterDoneTimestamp}ms");

            RaiseItemsChanged();

            var listPageUpdatedTimestamp = stopwatch.ElapsedMilliseconds;
            Logger.LogDebug($"Render items with '{newSearch}' in {listPageUpdatedTimestamp}ms /d {listPageUpdatedTimestamp - filterDoneTimestamp}ms");

            stopwatch.Stop();
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
    internal static int ScoreTopLevelItem(
        in FuzzyQuery query,
        IListItem topLevelOrAppItem,
        IRecentCommandsManager history,
        IPrecomputedFuzzyMatcher precomputedFuzzyMatcher)
    {
        var title = topLevelOrAppItem.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            return 0;
        }

        var isFallback = false;
        var isAliasSubstringMatch = false;
        var isAliasMatch = false;
        var id = IdForTopLevelOrAppItem(topLevelOrAppItem);

        FuzzyTarget? extensionDisplayNameTarget = null;
        if (topLevelOrAppItem is TopLevelViewModel topLevel)
        {
            isFallback = topLevel.IsFallback;
            extensionDisplayNameTarget = topLevel.GetExtensionNameTarget(precomputedFuzzyMatcher);

            if (topLevel.HasAlias)
            {
                var alias = topLevel.AliasText;
                isAliasMatch = alias == query.Original;
                isAliasSubstringMatch = isAliasMatch || alias.StartsWith(query.Original, StringComparison.CurrentCultureIgnoreCase);
            }
        }

        // Handle whitespace query separately - FuzzySearch doesn't handle it well
        if (string.IsNullOrWhiteSpace(query.Original))
        {
            return ScoreWhitespaceQuery(query.Original, title, topLevelOrAppItem.Subtitle, isFallback);
        }

        // Get precomputed targets
        var (titleTarget, subtitleTarget) = topLevelOrAppItem is IPrecomputedListItem precomputedItem
            ? (precomputedItem.GetTitleTarget(precomputedFuzzyMatcher), precomputedItem.GetSubtitleTarget(precomputedFuzzyMatcher))
            : (precomputedFuzzyMatcher.PrecomputeTarget(title), precomputedFuzzyMatcher.PrecomputeTarget(topLevelOrAppItem.Subtitle));

        // Score components
        var nameScore = precomputedFuzzyMatcher.Score(query, titleTarget);
        var descriptionScore = (precomputedFuzzyMatcher.Score(query, subtitleTarget) - 4) / 2.0;
        var extensionScore = extensionDisplayNameTarget is { } extTarget ? precomputedFuzzyMatcher.Score(query, extTarget) / 1.5 : 0;

        // Take best match from title/description/fallback, then add extension score
        // Extension adds to max so items matching both title AND extension bubble up
        var baseScore = Math.Max(Math.Max(nameScore, descriptionScore), isFallback ? 1 : 0);
        var matchScore = baseScore + extensionScore;

        // Apply a penalty to fallback items so they rank below direct matches.
        // Fallbacks that dynamically match queries (like RDP connections) should
        // appear after apps and direct command matches.
        if (isFallback && matchScore > 1)
        {
            // Reduce fallback scores by 50% to prioritize direct matches
            matchScore = matchScore * 0.5;
        }

        // Alias matching: exact match is overwhelming priority, substring match adds a small boost
        var aliasBoost = isAliasMatch ? 9001 : (isAliasSubstringMatch ? 1 : 0);
        var totalMatch = matchScore + aliasBoost;

        // Apply scaling and history boost only if we matched something real
        var finalScore = totalMatch * 10;
        if (totalMatch > 0)
        {
            finalScore += history.GetCommandHistoryWeight(id);
        }

        return (int)finalScore;
    }

    private static int ScoreWhitespaceQuery(string query, string title, string subtitle, bool isFallback)
    {
        // Simple contains check for whitespace queries
        var nameMatch = title.Contains(query, StringComparison.Ordinal) ? 1.0 : 0;
        var descriptionMatch = subtitle.Contains(query, StringComparison.Ordinal) ? 0.5 : 0;
        var baseScore = Math.Max(Math.Max(nameMatch, descriptionMatch), isFallback ? 1 : 0);

        return (int)(baseScore * 10);
    }

    private static int ScoreFallbackItem(IListItem topLevelOrAppItem, string[] fallbackRanks)
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
