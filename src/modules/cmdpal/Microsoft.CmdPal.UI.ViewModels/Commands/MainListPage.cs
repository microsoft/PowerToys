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
using Microsoft.CmdPal.UI.ViewModels.Services;
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
    private readonly ISettingsService _settingsService;
    private readonly IAppStateService _appStateService;
    private readonly ScoringFunction<IListItem> _scoringFunction;
    private readonly ScoringFunction<IListItem> _fallbackScoringFunction;
    private readonly IFuzzyMatcherProvider _fuzzyMatcherProvider;

    // Stable separator instances so that the VM cache and InPlaceUpdateList
    // recognise them across successive GetItems() calls
    private readonly Separator _pinnedSeparator = new(Resources.home_sections_pinned_title);
    private readonly Separator _resultsSeparator = new(Resources.results);
    private readonly Separator _fallbacksSeparator = new(Resources.fallbacks);
    private readonly Separator _commandsSeparator = new(Resources.home_sections_commands_title);

    private TopLevelViewModel[]? _cachedPinnedViewModels;
    private TopLevelViewModel[]? _cachedRegularViewModels;
    private bool _defaultViewDirty = true;

    private RoScored<IListItem>[]? _filteredItems;
    private RoScored<IListItem>[]? _filteredApps;

    // Global/special fallbacks are re-scored lazily on the render path (GetSearchViewItems)
    // instead of being frozen at keystroke time. Their dynamic titles are resolved
    // asynchronously off the typing path by the fallback update manager, so deferring the
    // score lets slow fallback results fold into the already-rendered list with fresh, correct
    // scores from the same ranker. Fallbacks always classify at the FallbackFloor tier, so this
    // only reorders them among themselves and can never leapfrog deterministic command/app
    // results. We snapshot the source list and the precomputed query together so a superseding
    // keystroke atomically replaces both.
    private IReadOnlyList<IListItem>? _globalFallbackSources;
    private FuzzyQuery _globalFallbackQuery;

    // Common fallbacks use rank-based (query-independent) scores, so freezing them is safe;
    // only their live titles decide whether they render, so they still fold in as they resolve.
    private IEnumerable<RoScored<IListItem>>? _fallbackItems;

    private bool _includeApps;
    private bool _filteredItemsIncludesApps;

    private int AppResultLimit => AllAppsCommandProvider.TopLevelResultLimit;

    private InterlockedBoolean _fullRefreshRequested;
    private InterlockedBoolean _refreshRunning;
    private InterlockedBoolean _refreshRequested;

    private CancellationTokenSource? _cancellationTokenSource;

    // Search telemetry. Emitted only when a search settles (trailing-edge debounce) so we never
    // send an event on every keystroke, and only for non-identifying aggregates (query length,
    // result count, latency) - never the raw query text. Selection telemetry is emitted from
    // UpdateHistory. All emission is measured at boundaries, never inside the per-item scoring loop.
    private static readonly TimeSpan SearchTelemetrySettleDelay = TimeSpan.FromMilliseconds(600);
    private readonly ThrottledDebouncedAction _searchTelemetryDebounce;
    private readonly Lock _searchTelemetryLock = new();
    private (int QueryLength, int ResultCount, long LatencyMs) _pendingSearchTelemetry;

    // Snapshots of the most recent rendered search results, read off the hot path (only when the
    // user invokes a result) to resolve the invoked item's visible rank and ranker tier.
    private IReadOnlyList<IListItem>? _lastSearchViewItems;
    private IReadOnlyList<RoScored<IListItem>>? _lastScoredGlobalFallbacks;

#if CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
    private DateTimeOffset _last = DateTimeOffset.UtcNow;
#endif

    public MainListPage(
        TopLevelCommandManager topLevelCommandManager,
        AliasManager aliasManager,
        IFuzzyMatcherProvider fuzzyMatcherProvider,
        ISettingsService settingsService,
        IAppStateService appStateService)
    {
        Id = "com.microsoft.cmdpal.home";
        Title = Resources.builtin_home_name;
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.altform-unplated_targetsize-256.png");
        PlaceholderText = Properties.Resources.builtin_main_list_page_searchbar_placeholder;

        _settingsService = settingsService;
        _aliasManager = aliasManager;
        _appStateService = appStateService;
        _tlcManager = topLevelCommandManager;
        _fuzzyMatcherProvider = fuzzyMatcherProvider;
        _scoringFunction = (in query, item) => ScoreTopLevelItem(in query, item, _appStateService.State.RecentCommands, _fuzzyMatcherProvider.Current, ResolveProviderSearchWeight);
        _fallbackScoringFunction = (in _, item) => ScoreFallbackItem(item, _settingsService.Settings.FallbackRanks);

        _tlcManager.PropertyChanged += TlcManager_PropertyChanged;
        _tlcManager.TopLevelCommands.CollectionChanged += Commands_CollectionChanged;
        _tlcManager.PinnedCommands.CollectionChanged += PinnedCommands_CollectionChanged;

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

        _searchTelemetryDebounce = new ThrottledDebouncedAction(EmitSearchResultsTelemetry, SearchTelemetrySettleDelay);

        // The all apps page will kick off a BG thread to start loading apps.
        // We just want to know when it is done.
        var allApps = AllAppsCommandProvider.Page;
        allApps.PropChanged += AllApps_PropChanged;

        WeakReferenceMessenger.Default.Register<ClearSearchMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateFallbackItemsMessage>(this);

        _settingsService.SettingsChanged += SettingsChangedHandler;
        HotReloadSettings(_settingsService.Settings);
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

    private void AllApps_PropChanged(object? sender, IPropChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AllAppsCommandProvider.Page.IsLoading))
        {
            IsLoading = ActuallyLoading();
        }
    }

    private void PinnedCommands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _defaultViewDirty = true;
        RaiseItemsChanged();
    }

    private void Commands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _defaultViewDirty = true;
        _includeApps = _tlcManager.IsProviderActive(AllAppsCommandProvider.WellKnownId);
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
            return string.IsNullOrWhiteSpace(SearchText) ? GetDefaultViewItems() : GetSearchViewItems();
        }
    }

    private IListItem[] GetSearchViewItems()
    {
        // Re-score the global fallbacks against their current titles so that any fallback whose
        // dynamic title resolved asynchronously (after first paint) folds into the list with a
        // fresh score from the same ranker. This runs on the render path and is cheap because
        // there are only a handful of configured global fallbacks. Deterministic command/app
        // results below do not depend on this, so first paint never waits on the (async) fallbacks.
        var validScoredFallbacks = ScoreDeferredFallbacks(_globalFallbackSources, _globalFallbackQuery, _scoringFunction);

        var validFallbacks = _fallbackItems?
            .Where(s => !string.IsNullOrWhiteSpace(s.Item.Title))
            .ToList();

        var result = MainListPageResultFactory.Create(
            _filteredItems,
            validScoredFallbacks,
            _filteredApps,
            validFallbacks,
            _resultsSeparator,
            _fallbacksSeparator,
            AppResultLimit);

        // Snapshot the rendered order and the (packed-score) global fallbacks so selection
        // telemetry can resolve an invoked item's rank and tier off the hot path. These are
        // plain reference assignments - no extra allocation on the render path.
        _lastSearchViewItems = result;
        _lastScoredGlobalFallbacks = validScoredFallbacks;

        return result;
    }

    // Scores the current global-fallback snapshot against its query using the supplied ranker,
    // dropping any whose (possibly still unresolved) title is empty. Extracted and made static
    // so the fast-first-paint fold-in can be unit tested with a fake slow source: deterministic
    // command/app results are produced entirely without this method, and re-scoring here always
    // reflects the latest snapshot, so a superseding keystroke's snapshot replaces any stale one.
    internal static List<RoScored<IListItem>>? ScoreDeferredFallbacks(
        IReadOnlyList<IListItem>? sources,
        in FuzzyQuery query,
        ScoringFunction<IListItem> scoringFunction)
    {
        if (sources is null || sources.Count == 0)
        {
            return null;
        }

        var scored = InternalListHelpers.FilterListWithScores(sources, query, scoringFunction);
        if (scored.Length == 0)
        {
            return null;
        }

        List<RoScored<IListItem>>? valid = null;
        foreach (var s in scored)
        {
            if (string.IsNullOrWhiteSpace(s.Item.Title))
            {
                continue;
            }

            valid ??= new List<RoScored<IListItem>>(scored.Length);
            valid.Add(s);
        }

        return valid;
    }

    private IListItem[] GetDefaultViewItems()
    {
        if (_defaultViewDirty)
        {
            RebuildDefaultViewCache();
        }

        var pinned = _cachedPinnedViewModels!;
        var regular = _cachedRegularViewModels!;
        var pinnedCount = pinned.Length;
        var regularCount = regular.Length;

        var sectionCount = (pinnedCount > 0 ? 1 : 0) + (regularCount > 0 ? 1 : 0);
        if (sectionCount == 0)
        {
            return [];
        }

        var result = new IListItem[pinnedCount + regularCount + sectionCount];
        var writeIndex = 0;
        if (pinnedCount > 0)
        {
            result[writeIndex++] = _pinnedSeparator;
            Array.Copy(pinned, 0, result, writeIndex, pinnedCount);
            writeIndex += pinnedCount;
        }

        if (regularCount > 0)
        {
            result[writeIndex++] = _commandsSeparator;
            Array.Copy(regular, 0, result, writeIndex, regularCount);
        }

        return result;
    }

    private void RebuildDefaultViewCache()
    {
        var allCommands = _tlcManager.TopLevelCommands;
        var pinnedSettings = _tlcManager.PinnedCommands;

        // Resolve pinned VMs in settings order
        var pinned = new List<TopLevelViewModel>(pinnedSettings.Count);
        for (var i = 0; i < pinnedSettings.Count; i++)
        {
            var s = pinnedSettings[i];
            for (var j = 0; j < allCommands.Count; j++)
            {
                var cmd = allCommands[j];
                if (IsEligibleTopLevelCommand(cmd) &&
                    cmd.CommandProviderId == s.ProviderId &&
                    cmd.Id == s.CommandId)
                {
                    pinned.Add(cmd);
                    break;
                }
            }
        }

        // Single pass for regular items
        var regular = new List<TopLevelViewModel>(allCommands.Count);
        for (var i = 0; i < allCommands.Count; i++)
        {
            var candidate = allCommands[i];
            if (IsEligibleTopLevelCommand(candidate) && !_tlcManager.IsPinned(candidate.CommandProviderId, candidate.Id))
            {
                regular.Add(candidate);
            }
        }

        _cachedPinnedViewModels = [.. pinned];
        _cachedRegularViewModels = [.. regular];
        _defaultViewDirty = false;
    }

    private static bool IsEligibleTopLevelCommand(TopLevelViewModel command)
    {
        return !command.IsFallback && !string.IsNullOrEmpty(command.Title);
    }

    private void ClearResults()
    {
        _filteredItems = null;
        _filteredApps = null;
        _fallbackItems = null;
        _globalFallbackSources = null;

        // Reset the paired query too. ScoreDeferredFallbacks already short-circuits on a null
        // source list, so a stale query here is harmless, but clearing both keeps the snapshot
        // pair symmetric and avoids a confusing leftover value.
        _globalFallbackQuery = default;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        var oldWasEmpty = string.IsNullOrEmpty(oldSearch);
        var newWasEmpty = string.IsNullOrEmpty(newSearch);
        if (oldWasEmpty != newWasEmpty)
        {
            WeakReferenceMessenger.Default.Send<ExpandCompactModeMessage>(new(!newWasEmpty));
        }

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
            var configuredGlobalFallbackIds = _settingsService.Settings.GetGlobalFallbacks();
            var specialFallbacks = new List<TopLevelViewModel>(configuredGlobalFallbackIds.Length);
            var commonFallbacks = new List<TopLevelViewModel>(Math.Max(commands.Count - configuredGlobalFallbackIds.Length, 0));

            foreach (var s in commands)
            {
                if (!s.IsFallback)
                {
                    continue;
                }

                if (configuredGlobalFallbackIds.Contains(s.Id))
                {
                    specialFallbacks.Add(s);
                }
                else if (s.IsEnabled)
                {
                    commonFallbacks.Add(s);
                }
            }

            _fallbackUpdateManager.BeginUpdate(SearchText, [.. specialFallbacks, .. commonFallbacks], token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            // Cleared out the filter text? easy. Reset _filteredItems, and bail out.
            if (string.IsNullOrWhiteSpace(newSearch))
            {
                _filteredItemsIncludesApps = _includeApps;
                ClearResults();

                // Drop any pending settled-search telemetry so a cleared query never emits.
                _searchTelemetryDebounce.Cancel();
                _lastSearchViewItems = null;
                _lastScoredGlobalFallbacks = null;

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
                    var pinnedCommandIds = _settingsService.Settings.GetPinnedCommandIds(AllAppsCommandProvider.WellKnownId);

                    if (pinnedCommandIds.Count > 0)
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

            // Snapshot the global fallbacks and query, but do NOT score them here. Their dynamic
            // titles are updated asynchronously by the fallback update manager (BeginUpdate, above),
            // which runs off the typing path. Scoring is deferred to the render path so late fallback
            // resolutions fold in with fresh scores instead of a value frozen against a stale title.
            _globalFallbackSources = commands.Where(s => s.IsFallback && configuredGlobalFallbackIds.Contains(s.Id)).ToArray();
            _globalFallbackQuery = searchQuery;

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

#if CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
            var filterDoneTimestamp = stopwatch.ElapsedMilliseconds;
#endif

            // Queue a settled-search telemetry event. This measures the deterministic first-paint
            // results (commands + capped apps) and the latency to produce them, at this boundary -
            // never inside the per-item scoring loop. The event is debounced so it is emitted only
            // when the query settles, not on every keystroke, and it carries the query LENGTH only.
            if (isUserInput)
            {
                var deterministicResultCount = (_filteredItems?.Length ?? 0)
                    + Math.Min(_filteredApps?.Length ?? 0, AppResultLimit);
                QueueSearchResultsTelemetry(newSearch.Length, deterministicResultCount, stopwatch.ElapsedMilliseconds);
            }

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
        IPrecomputedFuzzyMatcher precomputedFuzzyMatcher,
        Func<IListItem, ProviderSearchWeight>? providerWeightLookup = null)
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

        // Score components. Keep the raw matcher scores so "did this signal match at
        // all" is decided before the historical subtitle penalty (which can push a real
        // subtitle match below zero).
        var nameScore = precomputedFuzzyMatcher.Score(query, titleTarget);
        var rawSubtitleScore = precomputedFuzzyMatcher.Score(query, subtitleTarget);
        var rawExtensionScore = extensionDisplayNameTarget is { } extTarget ? precomputedFuzzyMatcher.Score(query, extTarget) : 0;

        var descriptionScore = (rawSubtitleScore - 4) / 2.0;
        var extensionScore = rawExtensionScore / 1.5;

        // Lexical quality preserves the previous relative weighting of the signals: best
        // of title/description (plus the fallback floor), then a smaller extension-name
        // contribution added on top so items matching both title AND extension bubble up.
        var lexicalQuality = Math.Max(Math.Max(nameScore, descriptionScore), isFallback ? 1 : 0) + extensionScore;

        var matchedLexically = nameScore > 0 || rawSubtitleScore > 0 || rawExtensionScore > 0;

        // The hard tier decides ordering; frecency and the alias-substring nudge only
        // reorder items that already share a tier. ClassifyTier returns None precisely when
        // nothing matched (no lexical, alias, or fallback signal), so this single gate also
        // filters non-matches - no separate pre-check is needed.
        var tier = MainListRanker.ClassifyTier(query.Original, title, isFallback, isAliasMatch, isAliasSubstringMatch, matchedLexically);
        if (tier == RankTier.None)
        {
            return 0;
        }

        var frecencyWeight = history.GetCommandHistoryWeight(id);
        var aliasSubstringBonus = isAliasSubstringMatch && !isAliasMatch ? MainListRanker.AliasSubstringBonus : 0.0;

        // Per-provider weight is a within-tier nudge only. Resolving it here (rather than in
        // the tier classifier) guarantees it can never promote an item across a tier boundary.
        var providerWeight = providerWeightLookup?.Invoke(topLevelOrAppItem) ?? ProviderSearchWeight.Normal;
        var providerBonus = MainListRanker.ProviderBonus(providerWeight);

        var withinTier = MainListRanker.WithinTierScore(
            lexicalQuality,
            frecencyWeight,
            aliasSubstringBonus,
            providerBonus: providerBonus);

        return MainListRanker.Pack(tier, withinTier);
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
        _appStateService.UpdateState(state => state with
        {
            RecentCommands = state.RecentCommands.WithHistoryItem(id),
        });

        EmitSelectionTelemetry(topLevelOrAppItem);
    }

    // Emits selection telemetry when the user invokes a result during an active search. Runs only
    // on invoke (a deliberate, infrequent user action - never on the typing/scoring path) and
    // captures only non-identifying aggregates: the query LENGTH, the invoked item's visible rank,
    // and its ranker tier. Nothing is emitted for the default (no-search) view, or when the invoked
    // item is not among the last rendered search results.
    private void EmitSelectionTelemetry(IListItem invoked)
    {
        var searchText = SearchText;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        var index = ResolveVisibleIndex(_lastSearchViewItems, invoked, _resultsSeparator, _fallbacksSeparator);
        if (index < 0)
        {
            return;
        }

        var packed = (_filteredItems ?? Enumerable.Empty<RoScored<IListItem>>())
            .Concat(_filteredApps ?? Enumerable.Empty<RoScored<IListItem>>())
            .Concat(_lastScoredGlobalFallbacks ?? Enumerable.Empty<RoScored<IListItem>>());

        var tier = ResolveSelectedTier(invoked, packed, _fallbackItems);
        if (tier == RankTier.None)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(BuildSearchSelectedMessage(searchText, index, tier));
    }

    // Stores the latest settled-search metrics and (re)arms the debounce. Only the query LENGTH is
    // retained - the query text is never stored for telemetry.
    private void QueueSearchResultsTelemetry(int queryLength, int resultCount, long latencyMs)
    {
        lock (_searchTelemetryLock)
        {
            _pendingSearchTelemetry = (queryLength, resultCount, latencyMs);
        }

        _searchTelemetryDebounce.Invoke();
    }

    private void EmitSearchResultsTelemetry()
    {
        (int QueryLength, int ResultCount, long LatencyMs) snapshot;
        lock (_searchTelemetryLock)
        {
            snapshot = _pendingSearchTelemetry;
        }

        if (snapshot.QueryLength <= 0)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(
            BuildSearchResultsMessage(snapshot.QueryLength, snapshot.ResultCount, snapshot.LatencyMs));
    }

    // Builds the settled-search telemetry payload from a query string, capturing only its LENGTH.
    // Exposed for tests to prove the raw query text is never carried.
    internal static TelemetrySearchResultsMessage BuildSearchResultsMessage(string query, int resultCount, long latencyMs)
        => BuildSearchResultsMessage(query?.Length ?? 0, resultCount, latencyMs);

    internal static TelemetrySearchResultsMessage BuildSearchResultsMessage(int queryLength, int resultCount, long latencyMs)
    {
        var length = Math.Max(queryLength, 0);
        var count = Math.Max(resultCount, 0);
        var latency = latencyMs < 0 ? 0UL : (ulong)latencyMs;
        return new TelemetrySearchResultsMessage(length, count, count == 0, latency);
    }

    // Builds the selection telemetry payload, capturing only the query LENGTH, the selected rank,
    // and the ranker tier. Exposed for tests to prove the raw query text is never carried.
    internal static TelemetrySearchResultSelectedMessage BuildSearchSelectedMessage(string query, int selectedIndex, RankTier selectedTier)
        => new(query?.Length ?? 0, selectedIndex, selectedTier);

    // Zero-based visible rank of an invoked item within the rendered results, skipping the section
    // separators. Returns -1 when the item is not present (e.g. it was invoked from a different view).
    internal static int ResolveVisibleIndex(IReadOnlyList<IListItem>? renderedResults, IListItem invoked, params IListItem[] separators)
    {
        if (renderedResults is null)
        {
            return -1;
        }

        var visible = 0;
        foreach (var item in renderedResults)
        {
            var isSeparator = false;
            foreach (var separator in separators)
            {
                if (ReferenceEquals(item, separator))
                {
                    isSeparator = true;
                    break;
                }
            }

            if (isSeparator)
            {
                continue;
            }

            if (ReferenceEquals(item, invoked))
            {
                return visible;
            }

            visible++;
        }

        return -1;
    }

    // Resolves the ranker tier of an invoked item. Packed sources (commands, apps, global
    // fallbacks) decode their tier via MainListRanker.TierOf; common fallbacks carry rank-based
    // (non-packed) scores, so they are reported at the fallback floor. Returns None when the item
    // is not found in any source.
    internal static RankTier ResolveSelectedTier(
        IListItem invoked,
        IEnumerable<RoScored<IListItem>>? packedResults,
        IEnumerable<RoScored<IListItem>>? fallbackResults)
    {
        if (packedResults is not null)
        {
            foreach (var scored in packedResults)
            {
                if (ReferenceEquals(scored.Item, invoked))
                {
                    return MainListRanker.TierOf(scored.Score);
                }
            }
        }

        if (fallbackResults is not null)
        {
            foreach (var scored in fallbackResults)
            {
                if (ReferenceEquals(scored.Item, invoked))
                {
                    return RankTier.FallbackFloor;
                }
            }
        }

        return RankTier.None;
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

    // Resolves the user-configured per-provider search weight for an item. Top-level commands
    // carry their own provider id; installed apps all belong to the well-known "AllApps"
    // provider, so app items are weighted by that provider's setting.
    private ProviderSearchWeight ResolveProviderSearchWeight(IListItem topLevelOrAppItem)
    {
        var providerId = topLevelOrAppItem is TopLevelViewModel topLevel
            ? topLevel.CommandProviderId
            : AllAppsCommandProvider.WellKnownId;

        if (string.IsNullOrEmpty(providerId))
        {
            return ProviderSearchWeight.Normal;
        }

        return _settingsService.Settings.ProviderSettings.TryGetValue(providerId, out var providerSettings)
            ? providerSettings.SearchWeight
            : ProviderSearchWeight.Normal;
    }

    public void Receive(ClearSearchMessage message) => SearchText = string.Empty;

    public void Receive(UpdateFallbackItemsMessage message)
    {
        _tlcManager.RebuildPinnedCache();
        _defaultViewDirty = true;
        RequestRefresh(fullRefresh: false);
    }

    private void SettingsChangedHandler(ISettingsService sender, SettingsModel args) => HotReloadSettings(args);

    private void HotReloadSettings(SettingsModel settings) => ShowDetails = settings.ShowAppDetails;

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _fallbackUpdateManager.Dispose();
        _searchTelemetryDebounce.Dispose();

        _tlcManager.PropertyChanged -= TlcManager_PropertyChanged;
        _tlcManager.TopLevelCommands.CollectionChanged -= Commands_CollectionChanged;
        _tlcManager.PinnedCommands.CollectionChanged -= PinnedCommands_CollectionChanged;

        AllAppsCommandProvider.Page.PropChanged -= AllApps_PropChanged;

        if (_settingsService is not null)
        {
            _settingsService.SettingsChanged -= SettingsChangedHandler;
        }

        WeakReferenceMessenger.Default.UnregisterAll(this);
        GC.SuppressFinalize(this);
    }
}
