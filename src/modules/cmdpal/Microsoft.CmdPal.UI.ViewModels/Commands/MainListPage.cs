// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*
 #define CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
*/

using System.Collections.Specialized;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Core.Common.Helpers;
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
    IRecipient<UpdateFallbackItemsMessage>,
    IDisposable
{
    // Throttle for raising items changed events from external sources
    private static readonly TimeSpan RaiseItemsChangedThrottle = TimeSpan.FromMilliseconds(100);

    // Throttle for raising items changed events from user input - we want this to feel more responsive, so a shorter throttle.
    private static readonly TimeSpan RaiseItemsChangedThrottleForUserInput = TimeSpan.FromMilliseconds(50);

    private readonly FallbackUpdateManager _fallbackUpdateManager;
    private readonly ThrottledDebouncedAction _refreshThrottledDebouncedAction;
    private readonly TopLevelCommandManager _tlcManager;
    private readonly AliasManager _aliasManager;
    private readonly SettingsModel _settings;
    private readonly AppStateModel _appStateModel;
    private readonly ScoringFunction<IListItem> _scoringFunction;
    private readonly IFuzzyMatcherProvider _fuzzyMatcherProvider;

    // Stable separator instances so that the VM cache and InPlaceUpdateList
    // recognise them across successive GetItems() calls
    private readonly Separator _resultsSeparator = new(Resources.results);
    private readonly Separator _fallbacksSeparator = new(Resources.fallbacks);
    private readonly Separator _commandsSeparator = new(Resources.home_sections_commands_title);
    private readonly Dictionary<string, Separator> _fallbackSourceSeparators = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CachedFallbackRenderBlock> _fallbackRenderBlocks = new(StringComparer.Ordinal);

    private RoScored<IListItem>[]? _filteredItems;
    private RoScored<IListItem>[]? _filteredApps;
    private IReadOnlyList<TopLevelViewModel> _globalFallbackSources = [];
    private IReadOnlyList<TopLevelViewModel> _rankedFallbackSources = [];
    private IReadOnlyList<FallbackDescriptor> _fallbackDescriptors = [];
    private FuzzyQuery _currentSearchQuery;
    private string _currentSearchQueryText = string.Empty;
    private string _fallbackRenderQueryText = string.Empty;

    private bool _includeApps;
    private bool _filteredItemsIncludesApps;
    private bool _fallbackDescriptorsDirty = true;
    private uint _fallbackRenderMatcherSchemaId;

    private int AppResultLimit => AllAppsCommandProvider.TopLevelResultLimit;

    private InterlockedBoolean _fullRefreshRequested;
    private InterlockedBoolean _refreshRunning;
    private InterlockedBoolean _refreshRequested;

    private CancellationTokenSource? _cancellationTokenSource;

#if CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
    private DateTimeOffset _last = DateTimeOffset.UtcNow;
#endif

    public MainListPage(
        TopLevelCommandManager topLevelCommandManager,
        SettingsModel settings,
        AliasManager aliasManager,
        AppStateModel appStateModel,
        IFuzzyMatcherProvider fuzzyMatcherProvider)
    {
        Id = "com.microsoft.cmdpal.home";
        Title = Resources.builtin_home_name;
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.scale-200.png");
        PlaceholderText = Properties.Resources.builtin_main_list_page_searchbar_placeholder;

        _settings = settings;
        _aliasManager = aliasManager;
        _appStateModel = appStateModel;
        _tlcManager = topLevelCommandManager;
        _fuzzyMatcherProvider = fuzzyMatcherProvider;
        _scoringFunction = (in query, item) => ScoreTopLevelItem(in query, item, _appStateModel.RecentCommands, _fuzzyMatcherProvider.Current);

        _tlcManager.PropertyChanged += TlcManager_PropertyChanged;
        _tlcManager.TopLevelCommands.CollectionChanged += Commands_CollectionChanged;

        _refreshThrottledDebouncedAction = new ThrottledDebouncedAction(
            () =>
            {
                try
                {
#if CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
                    var delta = DateTimeOffset.UtcNow - _last;
                    _last = DateTimeOffset.UtcNow;
                    Logger.LogDebug($"UpdateFallbacks: RaiseItemsChanged, delta {delta}");

                    var sw = Stopwatch.StartNew();
#endif
                    if (_fullRefreshRequested.Clear())
                    {
                        // full refresh
                        RaiseItemsChanged();
                    }
                    else
                    {
                        // preserve selection
                        RaiseItemsChanged(ListViewModel.IncrementalRefresh);
                    }

#if CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
                    Logger.LogInfo($"UpdateFallbacks: RaiseItemsChanged took {sw.Elapsed}");
#endif
                }
                catch (Exception ex)
                {
                    Logger.LogError("Unhandled exception in MainListPage refresh debounced action", ex);
                }
            },
            RaiseItemsChangedThrottle);

        _fallbackUpdateManager = new FallbackUpdateManager(() => RequestRefresh(fullRefresh: false));

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
        InvalidateFallbackDescriptorCache();
        if (_includeApps != _filteredItemsIncludesApps)
        {
            ReapplySearchInBackground();
        }
        else
        {
            RequestRefresh(fullRefresh: false);
        }
    }

    private void RequestRefresh(bool fullRefresh, TimeSpan? interval = null)
    {
        if (fullRefresh)
        {
            _fullRefreshRequested.Set();
        }

        _refreshThrottledDebouncedAction.Invoke(interval);
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
                UpdateSearchTextCore(currentSearchText, currentSearchText, isUserInput: false);
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
                result[0] = _commandsSeparator;

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
                var fallbackRenderPlan = BuildFallbackRenderPlan(GetCurrentSearchQuery());

                return MainListPageResultFactory.Create(
                    _filteredItems,
                    fallbackRenderPlan.ScoredGlobalItems,
                    _filteredApps,
                    fallbackRenderPlan.LeadingItems,
                    fallbackRenderPlan.TrailingGlobalItems,
                    fallbackRenderPlan.OrderedFallbackItems,
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
        _currentSearchQuery = default;
        _currentSearchQueryText = string.Empty;
        InvalidateFallbackRenderBlocks();
    }

    private void InvalidateFallbackDescriptorCache()
    {
        _fallbackDescriptors = [];
        _fallbackDescriptorsDirty = true;
        InvalidateFallbackRenderBlocks();
    }

    private void InvalidateFallbackRenderBlocks()
    {
        _fallbackRenderBlocks.Clear();
        _fallbackRenderQueryText = string.Empty;
        _fallbackRenderMatcherSchemaId = 0;
    }

    private void SetFallbackSources(IReadOnlyList<TopLevelViewModel> globalFallbackSources, IReadOnlyList<TopLevelViewModel> rankedFallbackSources)
    {
        var fallbackTopologyChanged = !SameFallbackSources(_globalFallbackSources, globalFallbackSources) ||
            !SameFallbackSources(_rankedFallbackSources, rankedFallbackSources);

        _globalFallbackSources = globalFallbackSources;
        _rankedFallbackSources = rankedFallbackSources;

        if (fallbackTopologyChanged)
        {
            InvalidateFallbackDescriptorCache();
        }
    }

    private static bool SameFallbackSources(IReadOnlyList<TopLevelViewModel> left, IReadOnlyList<TopLevelViewModel> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!ReferenceEquals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        UpdateSearchTextCore(oldSearch, newSearch, isUserInput: true);
    }

    private void UpdateSearchTextCore(string oldSearch, string newSearch, bool isUserInput)
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
                lock (_tlcManager.TopLevelCommands)
                {
                    CancelFallbackQueries(_tlcManager.TopLevelCommands);
                    if (_filteredItemsIncludesApps != _includeApps)
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
            var commonFallbacks = new List<TopLevelViewModel>(commands.Count - globalFallbacks.Length);

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

            SetFallbackSources(specialFallbacks, commonFallbacks);
            var allFallbacks = new List<TopLevelViewModel>(specialFallbacks.Count + commonFallbacks.Count);
            allFallbacks.AddRange(specialFallbacks);
            allFallbacks.AddRange(commonFallbacks);

            UpdateInlineFallbacks(newSearch, allFallbacks);
            _fallbackUpdateManager.BeginUpdate(newSearch, GetRemoteFallbacks(allFallbacks), token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            // Cleared out the filter text? easy. Reset _filteredItems, and bail out.
            if (string.IsNullOrWhiteSpace(newSearch))
            {
                CancelFallbackQueries(specialFallbacks);
                CancelFallbackQueries(commonFallbacks);
                _filteredItemsIncludesApps = _includeApps;
                ClearResults();
                var wasAlreadyEmpty = string.IsNullOrWhiteSpace(oldSearch);
                RequestRefresh(fullRefresh: true, interval: wasAlreadyEmpty ? null : TimeSpan.Zero);

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

            // If we don't have any previous filter results to work with, start
            // with a list of all our commands & apps.
            if (!newFilteredItems.Any() && !newApps.Any())
            {
                newFilteredItems = commands.Where(s => !s.IsFallback);

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
            _currentSearchQuery = searchQuery;
            _currentSearchQueryText = SearchText;

            // Produce a list of everything that matches the current filter.
            _filteredItems = InternalListHelpers.FilterListWithScores(newFilteredItems, searchQuery, _scoringFunction);

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

#if CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
            var filterDoneTimestamp = stopwatch.ElapsedMilliseconds;
#endif
            if (isUserInput)
            {
                // Make sure that the throttle delay is consistent from the user's perspective, even if filtering
                // takes a long time. If we always use the full throttle duration, then a slow filter could make the UI feel sluggish.
                var adjustedInterval = RaiseItemsChangedThrottleForUserInput - stopwatch.Elapsed;
                if (adjustedInterval < TimeSpan.Zero)
                {
                    adjustedInterval = TimeSpan.Zero;
                }

                RequestRefresh(fullRefresh: true, adjustedInterval);
            }
            else
            {
                RequestRefresh(fullRefresh: true);
            }

#if CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
            var listPageUpdatedTimestamp = stopwatch.ElapsedMilliseconds;
            Logger.LogDebug($"Render items with '{newSearch}' in {listPageUpdatedTimestamp}ms /d {listPageUpdatedTimestamp - filterDoneTimestamp}ms");
#endif

            stopwatch.Stop();
        }
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
        else if (topLevelOrAppItem is FallbackQueryResultItem cachedFallbackResult)
        {
            isFallback = true;
            extensionDisplayNameTarget = cachedFallbackResult.GetExtensionNameTarget(precomputedFuzzyMatcher);

            if (cachedFallbackResult.HasAlias)
            {
                var alias = cachedFallbackResult.AliasText;
                isAliasMatch = alias == query.Original;
                isAliasSubstringMatch = isAliasMatch || alias.StartsWith(query.Original, StringComparison.CurrentCultureIgnoreCase);
            }
        }
        else if (topLevelOrAppItem is IFallbackResultItem fallbackResult)
        {
            isFallback = true;
            extensionDisplayNameTarget = precomputedFuzzyMatcher.PrecomputeTarget(fallbackResult.ExtensionName);

            if (fallbackResult.HasAlias)
            {
                var alias = fallbackResult.AliasText;
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
            return ScoreFallbackSourceId(topLevelViewModel.Id, fallbackRanks);
        }
        else if (topLevelOrAppItem is IFallbackResultItem fallbackResultItem)
        {
            return ScoreFallbackSourceId(fallbackResultItem.FallbackSourceId, fallbackRanks);
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

    private FuzzyQuery GetCurrentSearchQuery()
    {
        if (!string.Equals(_currentSearchQueryText, SearchText, StringComparison.Ordinal))
        {
            _currentSearchQuery = _fuzzyMatcherProvider.Current.PrecomputeQuery(SearchText);
            _currentSearchQueryText = SearchText;
        }

        return _currentSearchQuery;
    }

    private FallbackRenderPlan BuildFallbackRenderPlan(in FuzzyQuery searchQuery)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return FallbackRenderPlan.Empty;
        }

        EnsureFallbackRenderBlocksForQuery(in searchQuery);

        List<RoScored<IListItem>> scoredGlobalItems = [];
        List<IListItem> leadingItems = [];
        List<IListItem> trailingGlobalItems = [];
        List<RoScored<IListItem>> orderedFallbackItems = [];
        HashSet<string> activeSourceIds = new(StringComparer.Ordinal);

        foreach (var descriptor in GetFallbackDescriptors())
        {
            activeSourceIds.Add(descriptor.SourceId);

            var block = GetOrCreateFallbackRenderBlock(descriptor, in searchQuery);
            if (block.LeadingItems.Length > 0)
            {
                leadingItems.AddRange(block.LeadingItems);
            }

            if (block.ScoredGlobalItems.Length > 0)
            {
                scoredGlobalItems.AddRange(block.ScoredGlobalItems);
            }

            if (block.TrailingGlobalItems.Length > 0)
            {
                trailingGlobalItems.AddRange(block.TrailingGlobalItems);
            }

            if (block.OrderedFallbackItems.Length > 0)
            {
                orderedFallbackItems.AddRange(block.OrderedFallbackItems);
            }
        }

        TrimUnusedFallbackRenderBlocks(activeSourceIds);

        return new FallbackRenderPlan(scoredGlobalItems, leadingItems, trailingGlobalItems, orderedFallbackItems);
    }

    private IReadOnlyList<FallbackDescriptor> GetFallbackDescriptors()
    {
        if (!_fallbackDescriptorsDirty)
        {
            return _fallbackDescriptors;
        }

        List<FallbackDescriptor> descriptors = new(_globalFallbackSources.Count + _rankedFallbackSources.Count);
        for (var i = 0; i < _globalFallbackSources.Count; i++)
        {
            descriptors.Add(CreateFallbackDescriptor(_globalFallbackSources[i], treatAsGlobal: true, score: 0, index: i));
        }

        foreach (var entry in GetOrderedRankedFallbackSources())
        {
            descriptors.Add(CreateFallbackDescriptor(entry.Source, treatAsGlobal: false, entry.Score, entry.Index));
        }

        _fallbackDescriptors = [.. descriptors];
        _fallbackDescriptorsDirty = false;
        return _fallbackDescriptors;
    }

    private FallbackDescriptor CreateFallbackDescriptor(TopLevelViewModel source, bool treatAsGlobal, int score, int index)
    {
        return new FallbackDescriptor(
            Source: source,
            SourceId: source.Id,
            TreatAsGlobal: treatAsGlobal,
            Score: score,
            Index: index,
            DisplayOptions: GetFallbackDisplayOptions(source),
            ExecutionPolicy: source.GetFallbackExecutionPolicy(),
            UsesInlineEvaluation: source.UsesInlineFallbackEvaluation,
            UsesAsyncEvaluation: source.UsesAsyncFallbackEvaluation,
            HostMatchKind: source.FallbackHostMatchKind);
    }

    private IEnumerable<OrderedFallbackSource> GetOrderedRankedFallbackSources()
    {
        return _rankedFallbackSources
            .Select((source, index) => new OrderedFallbackSource(source, ScoreFallbackSourceId(source.Id, _settings.FallbackRanks), index))
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Index);
    }

    private static int ScoreFallbackSourceId(string fallbackSourceId, string[] fallbackRanks)
    {
        var index = Array.IndexOf(fallbackRanks, fallbackSourceId);
        return index >= 0 ? fallbackRanks.Length - index + 1 : 1;
    }

    private FallbackDisplayOptions GetFallbackDisplayOptions(TopLevelViewModel fallbackSource)
    {
        return TryGetFallbackSettings(fallbackSource, out var fallbackSettings)
            ? new FallbackDisplayOptions(
                fallbackSettings.ShowResultsInDedicatedSection,
                fallbackSettings.ShowResultsInDedicatedSection && fallbackSettings.ShowResultsBeforeMainResults)
            : FallbackDisplayOptions.Default;
    }

    private bool TryGetFallbackSettings(TopLevelViewModel fallbackSource, out FallbackSettings fallbackSettings)
    {
        fallbackSettings = default!;

        if (!_settings.ProviderSettings.TryGetValue(fallbackSource.CommandProviderId, out var providerSettings))
        {
            return false;
        }

        if (providerSettings.FallbackCommands.TryGetValue(fallbackSource.Id, out var settings) && settings is not null)
        {
            fallbackSettings = settings;
            return true;
        }

        return false;
    }

    private void EnsureFallbackRenderBlocksForQuery(in FuzzyQuery searchQuery)
    {
        var matcherSchemaId = _fuzzyMatcherProvider.Current.SchemaId;
        if (string.Equals(_fallbackRenderQueryText, searchQuery.Original, StringComparison.Ordinal) &&
            _fallbackRenderMatcherSchemaId == matcherSchemaId)
        {
            return;
        }

        _fallbackRenderQueryText = searchQuery.Original;
        _fallbackRenderMatcherSchemaId = matcherSchemaId;
        _fallbackRenderBlocks.Clear();
    }

    private FallbackRenderBlock GetOrCreateFallbackRenderBlock(FallbackDescriptor descriptor, in FuzzyQuery searchQuery)
    {
        var currentItems = descriptor.Source.GetCurrentFallbackItems();
        var sectionSeparator = (descriptor.DisplayOptions.ShowResultsInDedicatedSection || descriptor.DisplayOptions.ShowBeforeMainResults)
            ? GetFallbackSectionSeparator(descriptor.Source)
            : null;

        if (_fallbackRenderBlocks.TryGetValue(descriptor.SourceId, out var cachedBlock) &&
            cachedBlock.CanReuse(currentItems, descriptor))
        {
            return cachedBlock.Block;
        }

        var block = FallbackRenderBlockFactory.Create(
            currentItems,
            descriptor.TreatAsGlobal,
            descriptor.Score,
            descriptor.DisplayOptions.ShowResultsInDedicatedSection,
            descriptor.DisplayOptions.ShowBeforeMainResults,
            sectionSeparator,
            searchQuery,
            _scoringFunction);

        _fallbackRenderBlocks[descriptor.SourceId] = new CachedFallbackRenderBlock(currentItems, descriptor, block);
        return block;
    }

    private void TrimUnusedFallbackRenderBlocks(HashSet<string> activeSourceIds)
    {
        if (_fallbackRenderBlocks.Count <= activeSourceIds.Count)
        {
            return;
        }

        var staleSourceIds = _fallbackRenderBlocks.Keys
            .Where(sourceId => !activeSourceIds.Contains(sourceId))
            .ToArray();

        for (var i = 0; i < staleSourceIds.Length; i++)
        {
            _fallbackRenderBlocks.Remove(staleSourceIds[i]);
        }
    }

    private Separator GetFallbackSectionSeparator(TopLevelViewModel fallbackSource)
    {
        var title = !string.IsNullOrWhiteSpace(fallbackSource.DisplayTitle)
            ? fallbackSource.DisplayTitle
            : !string.IsNullOrWhiteSpace(fallbackSource.Title)
                ? fallbackSource.Title
                : fallbackSource.ExtensionName;

        if (!_fallbackSourceSeparators.TryGetValue(fallbackSource.Id, out var separator))
        {
            separator = new Separator(title);
            _fallbackSourceSeparators[fallbackSource.Id] = separator;
        }
        else if (!string.Equals(separator.Title, title, StringComparison.Ordinal))
        {
            separator.Title = title;
        }

        return separator;
    }

    private readonly record struct FallbackRenderPlan(
        List<RoScored<IListItem>> ScoredGlobalItems,
        List<IListItem> LeadingItems,
        List<IListItem> TrailingGlobalItems,
        List<RoScored<IListItem>> OrderedFallbackItems)
    {
        public static readonly FallbackRenderPlan Empty = new([], [], [], []);
    }

    private readonly record struct FallbackDescriptor(
        TopLevelViewModel Source,
        string SourceId,
        bool TreatAsGlobal,
        int Score,
        int Index,
        FallbackDisplayOptions DisplayOptions,
        FallbackExecutionPolicy ExecutionPolicy,
        bool UsesInlineEvaluation,
        bool UsesAsyncEvaluation,
        HostMatchKind? HostMatchKind);

    private readonly record struct CachedFallbackRenderBlock(
        IListItem[] SourceItems,
        FallbackDescriptor Descriptor,
        FallbackRenderBlock Block)
    {
        internal bool CanReuse(IListItem[] currentItems, FallbackDescriptor descriptor)
        {
            return ReferenceEquals(SourceItems, currentItems) &&
                Descriptor.TreatAsGlobal == descriptor.TreatAsGlobal &&
                Descriptor.Score == descriptor.Score &&
                Descriptor.DisplayOptions == descriptor.DisplayOptions;
        }
    }

    private readonly record struct OrderedFallbackSource(TopLevelViewModel Source, int Score, int Index);

    private readonly record struct FallbackDisplayOptions(bool ShowResultsInDedicatedSection, bool ShowBeforeMainResults)
    {
        public static readonly FallbackDisplayOptions Default = new(false, false);
    }

    private static void CancelFallbackQueries(IEnumerable<TopLevelViewModel> commands)
    {
        foreach (var command in commands)
        {
            if (command.IsFallback)
            {
                command.CancelOutstandingFallbackQuery();
            }
        }
    }

    private static IReadOnlyList<TopLevelViewModel> GetRemoteFallbacks(IEnumerable<TopLevelViewModel> commands)
    {
        List<TopLevelViewModel> remoteFallbacks = [];
        foreach (var command in commands)
        {
            if (!command.UsesInlineFallbackEvaluation)
            {
                remoteFallbacks.Add(command);
            }
        }

        return remoteFallbacks;
    }

    private static void UpdateInlineFallbacks(string query, IEnumerable<TopLevelViewModel> commands)
    {
        foreach (var command in commands)
        {
            if (command.UsesInlineFallbackEvaluation)
            {
                command.SafeUpdateFallbackTextInline(query);
            }
        }
    }

    public void Receive(ClearSearchMessage message) => SearchText = string.Empty;

    public void Receive(UpdateFallbackItemsMessage message)
    {
        RequestRefresh(fullRefresh: false, interval: RaiseItemsChangedThrottleForUserInput);
    }

    private void SettingsChangedHandler(SettingsModel sender, object? args)
    {
        InvalidateFallbackDescriptorCache();
        HotReloadSettings(sender);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            RequestRefresh(fullRefresh: true, interval: TimeSpan.Zero);
        }
    }

    private void HotReloadSettings(SettingsModel settings) => ShowDetails = settings.ShowAppDetails;

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _fallbackUpdateManager.Dispose();
        lock (_tlcManager.TopLevelCommands)
        {
            CancelFallbackQueries(_tlcManager.TopLevelCommands);
        }

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
