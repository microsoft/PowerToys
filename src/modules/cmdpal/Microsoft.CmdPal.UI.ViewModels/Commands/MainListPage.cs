// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*
 #define CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
*/

using System.Collections.Immutable;
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

    // All main-page search telemetry state and emission is owned by this dedicated type, keeping
    // MainListPage responsible for producing results rather than for tracking telemetry bookkeeping.
    private readonly MainListPageSearchTelemetry _searchTelemetry = new();

    // Stable separator instances so that the VM cache and InPlaceUpdateList
    // recognise them across successive GetItems() calls
    private readonly Separator _pinnedSeparator = new(Resources.home_sections_pinned_title);
    private readonly Separator _recentSeparator = new(Resources.home_sections_recent_title);
    private readonly Separator _resultsSeparator = new(Resources.results);
    private readonly Separator _fallbacksSeparator = new(Resources.fallbacks);
    private readonly Separator _commandsSeparator = new(Resources.home_sections_commands_title);

    private IListItem[]? _cachedPinnedViewModels;
    private IListItem[]? _cachedRecentViewModels;
    private IListItem[]? _cachedRegularViewModels;
    private bool _defaultViewDirty = true;
    private volatile RecentCommandsManager _recentCommands;
    private HomeRecentCommandsPlacement _recentCommandsOnHome;

    private RoScored<IListItem>[]? _filteredItems;
    private RoScored<IListItem>[]? _filteredApps;

    // Published with _filteredApps so filtering uses the query that produced the scores.
    private int _filteredAppsQueryLength;

    // Global/special fallbacks are scored on the render path, not at keystroke time, because
    // their titles resolve asynchronously. We snapshot the source list and query together so a
    // superseding keystroke replaces both atomically.
    private IReadOnlyList<IListItem>? _globalFallbackSources;
    private FuzzyQuery _globalFallbackQuery;

    // Common fallbacks use query-independent scores, so freezing them is safe; only their live
    // titles decide whether they render.
    private IEnumerable<RoScored<IListItem>>? _fallbackItems;

    private bool _includeApps;
    private bool _filteredItemsIncludesApps;

    // Last per-provider settings we reacted to, so a settings reload can tell whether any
    // provider's search weight actually changed and only then re-rank the active query.
    private ImmutableDictionary<string, ProviderSettings>? _lastProviderSettingsSnapshot;

    private int AppResultLimit => AllAppsCommandProvider.TopLevelResultLimit;

    // Longest query to filter fuzzy app matches on. This prevents weak app matches on short queries.
    private const int ShortQueryAppFilterMaxLength = 2;

    // Minimum tier an app must reach to appear for a short query.
    private const RankTier ShortQueryAppFilterMinTier = RankTier.AcronymWordBoundary;

    private InterlockedBoolean _fullRefreshRequested;
    private InterlockedBoolean _refreshRunning;
    private InterlockedBoolean _refreshRequested;

    private CancellationTokenSource? _cancellationTokenSource;

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
        _recentCommands = _appStateService.State.RecentCommands;
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
        _appStateService.StateChanged += AppStateService_StateChanged;

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
            if (!AllAppsCommandProvider.Page.IsLoading && _recentCommandsOnHome != HomeRecentCommandsPlacement.Hidden)
            {
                _defaultViewDirty = true;
                RequestRefresh(fullRefresh: false);
            }
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

    private void AppStateService_StateChanged(IAppStateService sender, AppStateModel args)
    {
        if (ReferenceEquals(_recentCommands, args.RecentCommands))
        {
            return;
        }

        _recentCommands = args.RecentCommands;
        if (_recentCommandsOnHome != HomeRecentCommandsPlacement.Hidden)
        {
            _defaultViewDirty = true;
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
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return GetDefaultViewItems();
        }

        lock (_tlcManager.TopLevelCommands)
        {
            return GetSearchViewItems();
        }
    }

    private IListItem[] GetSearchViewItems()
    {
        // Score global fallbacks against their current titles so a fallback whose title
        // resolved after first paint gets the right score. Cheap: only a handful are configured.
        var validScoredFallbacks = ScoreDeferredFallbacks(_globalFallbackSources, _globalFallbackQuery, _scoringFunction);

        var validFallbacks = _fallbackItems?
            .Where(s => !string.IsNullOrWhiteSpace(s.Item.Title))
            .ToList();

        // Remove fuzzy-only app matches for short queries so frecency-boosted weak matches don't
        // appear while typing.
        var filteredApps = FilterAppsForShortQueries(_filteredApps, _filteredAppsQueryLength);

        var result = MainListPageResultFactory.Create(
            _filteredItems,
            validScoredFallbacks,
            filteredApps,
            validFallbacks,
            _resultsSeparator,
            _fallbacksSeparator,
            AppResultLimit);

        // Snapshot the rendered order plus every scored input and the query length together, so
        // selection telemetry resolves an invoked item's rank, tier, and query length from this one
        // generation off the hot path. These are plain reference assignments - no extra allocation.
        _searchTelemetry.CaptureSearchView(
            result,
            _filteredItems,
            _filteredApps,
            validScoredFallbacks,
            _fallbackItems,
            SearchText?.Length ?? 0);

        return result;
    }

    // Scores the current global-fallback snapshot against its query, dropping any whose title is
    // still empty. Static so it can be unit tested with a fake slow source.
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

    // Returns a filtered view of the scored array without reordering it.
    internal static IList<RoScored<IListItem>>? FilterAppsForShortQueries(
        RoScored<IListItem>[]? scoredApps,
        int queryLength)
    {
        if (scoredApps is null || scoredApps.Length == 0)
        {
            return scoredApps;
        }

        if (queryLength <= 0 || queryLength > ShortQueryAppFilterMaxLength)
        {
            return scoredApps;
        }

        var keep = GetHighConfidenceAppsCount(scoredApps, ShortQueryAppFilterMinTier);
        return keep == scoredApps.Length
            ? scoredApps
            : new ArraySegment<RoScored<IListItem>>(scoredApps, 0, keep);
    }

    // Qualifying apps form a contiguous prefix because the array is already sorted by score.
    internal static int GetHighConfidenceAppsCount(IReadOnlyList<RoScored<IListItem>> scored, RankTier minTier)
    {
        var min = (int)minTier;
        for (var i = 0; i < scored.Count; i++)
        {
            if ((int)MainListRanker.TierOf(scored[i].Score) < min)
            {
                return i;
            }
        }

        return scored.Count;
    }

    // Applies the short-query filter and result limit to the telemetry count.
    internal static int GetVisibleAppCount(RoScored<IListItem>[]? scoredApps, int queryLength, int appResultLimit)
    {
        if (scoredApps is null || scoredApps.Length == 0)
        {
            return 0;
        }

        var count = scoredApps.Length;
        if (queryLength > 0 && queryLength <= ShortQueryAppFilterMaxLength)
        {
            count = GetHighConfidenceAppsCount(scoredApps, ShortQueryAppFilterMinTier);
        }

        return Math.Min(count, appResultLimit);
    }

    private IListItem[] GetDefaultViewItems()
    {
        if (_defaultViewDirty)
        {
            RebuildDefaultViewCache();
        }

        var pinned = _cachedPinnedViewModels!;
        var recent = _cachedRecentViewModels!;
        var regular = _cachedRegularViewModels!;
        var pinnedCount = pinned.Length;
        var recentCount = recent.Length;
        var regularCount = regular.Length;

        var sectionCount =
            (pinnedCount > 0 ? 1 : 0) +
            (recentCount > 0 ? 1 : 0) +
            (regularCount > 0 ? 1 : 0);
        if (sectionCount == 0)
        {
            return [];
        }

        var result = new IListItem[pinnedCount + recentCount + regularCount + sectionCount];
        var writeIndex = 0;

        void AppendSection(Separator separator, IListItem[] items)
        {
            if (items.Length == 0)
            {
                return;
            }

            result[writeIndex++] = separator;
            Array.Copy(items, 0, result, writeIndex, items.Length);
            writeIndex += items.Length;
        }

        if (_recentCommandsOnHome == HomeRecentCommandsPlacement.BeforePinned)
        {
            AppendSection(_recentSeparator, recent);
            AppendSection(_pinnedSeparator, pinned);
        }
        else
        {
            AppendSection(_pinnedSeparator, pinned);
            AppendSection(_recentSeparator, recent);
        }

        AppendSection(_commandsSeparator, regular);

        return result;
    }

    private void RebuildDefaultViewCache()
    {
        PinnedCommandSettings[] pinnedSettings;
        lock (_tlcManager.PinnedCommands)
        {
            pinnedSettings = [.. _tlcManager.PinnedCommands];
        }

        TopLevelViewModel[] allCommands;
        lock (_tlcManager.TopLevelCommands)
        {
            allCommands = [.. _tlcManager.TopLevelCommands];
        }

        IEnumerable<string> recentCommandIds = _recentCommandsOnHome == HomeRecentCommandsPlacement.Hidden
            ? []
            : _recentCommands.EnumerateRecentCommandIds();
        var sections = TopLevelCommandResolver.Resolve(
            pinnedSettings,
            recentCommandIds,
            allCommands,
            includeApps: _includeApps);

        _cachedPinnedViewModels = [.. sections.Pinned];
        _cachedRecentViewModels = [.. sections.Recent];
        _cachedRegularViewModels = [.. sections.Regular];
        _defaultViewDirty = false;
    }

    private void ClearResults()
    {
        _filteredItems = null;
        _filteredApps = null;
        _filteredAppsQueryLength = 0;
        _fallbackItems = null;
        _globalFallbackSources = null;

        // Clear the paired query too, so both are reset together.
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
                // An alias query supersedes any normal query whose settled-search telemetry is
                // still pending in the debounce; drop it so the superseded query never emits.
                _searchTelemetry.CancelPendingResults();

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

        // Inputs captured under the lock so the heavy scoring below can run off it. GetItems()
        // takes the same lock, so it now only contends with the short snapshot and publish sections.
        IReadOnlyList<IListItem> itemsSource;
        IReadOnlyList<IListItem> appsSource;
        IReadOnlyList<IListItem> fallbackSource;
        IListItem[] globalFallbackSources;
        bool includeAppsSnapshot;
        bool tookFullCatalog = false;

        // ===== SNAPSHOT PHASE (under lock) =====
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
                _searchTelemetry.ClearSearchView();

                var wasAlreadyEmpty = string.IsNullOrWhiteSpace(oldSearch);
                RequestRefresh(fullRefresh: true, interval: wasAlreadyEmpty ? null : TimeSpan.Zero);

                return;
            }

            includeAppsSnapshot = _includeApps;

            // A query that doesn't extend the old one, or a change in app inclusion, means we
            // can't re-use the previous results and have to rebuild from the full catalog. On an
            // extend we re-score only the previously matched subset.
            var reset = !newSearch.StartsWith(oldSearch, StringComparison.CurrentCultureIgnoreCase)
                || _filteredItemsIncludesApps != includeAppsSnapshot;

            var prevFilteredItems = reset ? null : _filteredItems;
            var prevApps = reset ? null : _filteredApps;
            var prevFallbacks = reset ? null : _fallbackItems;

            IEnumerable<IListItem> newFilteredItems = prevFilteredItems is not null
                ? prevFilteredItems.Select(s => s.Item)
                : Enumerable.Empty<IListItem>();
            IEnumerable<IListItem> newApps = prevApps is not null
                ? prevApps.Select(s => s.Item)
                : Enumerable.Empty<IListItem>();
            IEnumerable<IListItem> newFallbacks = prevFallbacks is not null
                ? prevFallbacks.Select(s => s.Item)
                : Enumerable.Empty<IListItem>();

            if (token.IsCancellationRequested)
            {
                return;
            }

            // If we don't have any previous filter results to work with, start
            // with a list of all our commands & apps.
            if (!newFilteredItems.Any() && !newApps.Any())
            {
                tookFullCatalog = true;
                newFilteredItems = commands.Where(s => !s.IsFallback);

                // Fallbacks are always included in the list, even if they
                // don't match the search text. But we don't want to
                // consider them when filtering the list.
                newFallbacks = commonFallbacks;

                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (includeAppsSnapshot)
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

            // Materialize every source while still under the lock, so the scoring passes never
            // touch the live TopLevelCommands collection or the app provider.
            itemsSource = MaterializeSource(newFilteredItems);
            appsSource = MaterializeSource(newApps);
            fallbackSource = MaterializeSource(newFallbacks);
            globalFallbackSources = [.. specialFallbacks];
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        // ===== SCORING PHASE (off the lock) =====
        // The dominant apps pass is parallelized, commands and fallbacks stay serial, and none of
        // it holds the TopLevelCommands lock any more, so it no longer blocks GetItems()/render.
        //
        // Snapshot every scoring input once, up front: the live fields can be swapped mid-pass when
        // a selection calls WithHistoryItem on another thread, which would mix two frecency
        // snapshots into one pass or race a parallel thread against an unwarmed history index.
        var recent = _appStateService.State.RecentCommands;
        recent.PrewarmIndex();
        var matcher = _fuzzyMatcherProvider.Current;
        var settings = _settingsService.Settings;
        var scoringNow = DateTimeOffset.UtcNow;

        // Precompute from the snapshotted newSearch, not the live SearchText, which a newer
        // keystroke may already have advanced past.
        var searchQuery = matcher.PrecomputeQuery(newSearch);

        // Every installed app belongs to the well-known AllApps provider, so its weight is constant
        // for the whole pass and we resolve it once instead of once per app.
        var appsProviderWeight = ResolveProviderSearchWeight(settings, AllAppsCommandProvider.WellKnownId);
        Func<IListItem, ProviderSearchWeight> commandsProviderLookup = item => ResolveProviderSearchWeight(settings, item);
        Func<IListItem, ProviderSearchWeight> appsProviderLookup = _ => appsProviderWeight;

        ScoringFunction<IListItem> commandsScorer = (in FuzzyQuery q, IListItem item) =>
            ScoreTopLevelItem(in q, item, recent, matcher, commandsProviderLookup, scoringNow);
        ScoringFunction<IListItem> appsScorer = (in FuzzyQuery q, IListItem item) =>
            ScoreTopLevelItem(in q, item, recent, matcher, appsProviderLookup, scoringNow);

        var scoredFilteredItems = InternalListHelpers.FilterListWithScores(itemsSource, searchQuery, commandsScorer);

        if (token.IsCancellationRequested)
        {
            return;
        }

        var scoredFallbackItems = InternalListHelpers.FilterListWithScores(fallbackSource, searchQuery, _fallbackScoringFunction);

        if (token.IsCancellationRequested)
        {
            return;
        }

        RoScored<IListItem>[]? scoredApps = null;
        if (appsSource.Count > 0)
        {
            scoredApps = InternalListHelpers.FilterListWithScoresParallel(appsSource, searchQuery, appsScorer);

            if (token.IsCancellationRequested)
            {
                return;
            }
        }

#if CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
        var filterDoneTimestamp = stopwatch.ElapsedMilliseconds;
#endif

        // ===== PUBLISH PHASE (under lock) =====
        // The critical section is the field swaps only. Telemetry debounce and refresh throttling
        // happen after the lock so _searchTelemetryLock never nests under the commands lock.
        var deterministicResultCount = 0;
        lock (commands)
        {
            // A newer keystroke cancels this token before doing its own work, so a stale snapshot
            // can never overwrite a newer query's results.
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (tookFullCatalog)
            {
                _filteredItemsIncludesApps = includeAppsSnapshot;
            }

            _filteredItems = scoredFilteredItems;
            _fallbackItems = scoredFallbackItems;

            // Snapshot the global fallbacks and query, but score them later on the render path,
            // since their titles are still resolving asynchronously (BeginUpdate, above).
            _globalFallbackSources = globalFallbackSources;
            _globalFallbackQuery = searchQuery;

            // With no apps source, publish null so a rebuild clears any stale set, matching the old
            // ClearResults behavior.
            _filteredApps = appsSource.Count > 0 ? scoredApps : null;

            // Publish the length with the array so filtering and telemetry use the same query.
            _filteredAppsQueryLength = _filteredApps is null ? 0 : newSearch.Length;

            if (isUserInput)
            {
                deterministicResultCount = (_filteredItems?.Length ?? 0)
                    + GetVisibleAppCount(_filteredApps, _filteredAppsQueryLength, AppResultLimit);
            }

#if CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
            var listPageUpdatedTimestamp = stopwatch.ElapsedMilliseconds;
            Logger.LogDebug($"Render items with '{newSearch}' in {listPageUpdatedTimestamp}ms /d {listPageUpdatedTimestamp - filterDoneTimestamp}ms");
#endif
        }

        // Getting here means the swap happened, since the superseded path returns inside the lock.
        stopwatch.Stop();

        if (isUserInput)
        {
            // Queue a settled-search telemetry event. It's debounced so it only fires once the
            // query settles, and it carries the query LENGTH only, never the text.
            _searchTelemetry.QueueSearchResults(newSearch.Length, deterministicResultCount, stopwatch.ElapsedMilliseconds);

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
    }

    // Materializes a source into a stable, indexable snapshot so scoring can run off the lock.
    // Anything already an IReadOnlyList passes through; lazy LINQ over live data gets copied.
    private static IReadOnlyList<IListItem> MaterializeSource(IEnumerable<IListItem> items)
        => items as IReadOnlyList<IListItem> ?? items.ToArray();

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
        Func<IListItem, ProviderSearchWeight>? providerWeightLookup = null,
        DateTimeOffset? now = null)
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

        var frecencyWeight = history.GetCommandHistoryWeight(id, now ?? DateTimeOffset.UtcNow);
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

        _searchTelemetry.ReportSelection(topLevelOrAppItem, _resultsSeparator, _fallbacksSeparator);
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
    // provider, so app items are weighted by that provider's setting. The static overloads take a
    // settings snapshot so the hot path resolves against one captured SettingsModel.
    private ProviderSearchWeight ResolveProviderSearchWeight(IListItem topLevelOrAppItem)
        => ResolveProviderSearchWeight(_settingsService.Settings, topLevelOrAppItem);

    private static ProviderSearchWeight ResolveProviderSearchWeight(SettingsModel settings, IListItem topLevelOrAppItem)
    {
        var providerId = topLevelOrAppItem is TopLevelViewModel topLevel
            ? topLevel.CommandProviderId
            : AllAppsCommandProvider.WellKnownId;

        return ResolveProviderSearchWeight(settings, providerId);
    }

    private static ProviderSearchWeight ResolveProviderSearchWeight(SettingsModel settings, string providerId)
    {
        if (string.IsNullOrEmpty(providerId))
        {
            return ProviderSearchWeight.Normal;
        }

        return settings.ProviderSettings.TryGetValue(providerId, out var providerSettings)
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

    private void HotReloadSettings(SettingsModel settings)
    {
        ShowDetails = settings.ShowAppDetails;

        if (_recentCommandsOnHome != settings.RecentCommandsOnHome)
        {
            _recentCommandsOnHome = settings.RecentCommandsOnHome;
            _defaultViewDirty = true;
            RequestRefresh(fullRefresh: false);
        }

        // A per-provider search-weight change has to reorder the query that is already on screen.
        // Scoring reads the weight live, but scored results are cached, so without an explicit
        // re-score the active query keeps its old order until the next keystroke. Detect a weight
        // change and re-rank the current search in place.
        var providerSettings = settings.ProviderSettings;
        var weightsChanged = ProviderWeightsChanged(_lastProviderSettingsSnapshot, providerSettings);
        _lastProviderSettingsSnapshot = providerSettings;

        if (weightsChanged && !string.IsNullOrEmpty(SearchText))
        {
            RerankActiveSearch();
        }
    }

    // Re-scores the current query off the UI thread so a settings change (e.g. a per-provider
    // search-weight change) reorders the results already shown. This reuses the same non-reset
    // re-score path as an app-inclusion refresh: the retained matches are re-scored with the new
    // weights, which is sufficient because provider weight only nudges order within a tier and
    // never changes which items match.
    private void RerankActiveSearch()
    {
        var current = SearchText;
        if (!string.IsNullOrEmpty(current))
        {
            _ = Task.Run(() => UpdateSearchTextCore(current, current, isUserInput: false));
        }
    }

    // True when the effective per-provider search weight differs between two snapshots. A provider
    // absent from a snapshot is treated as Normal, so adding or removing an entry whose weight is
    // Normal does not count as a change.
    private static bool ProviderWeightsChanged(
        ImmutableDictionary<string, ProviderSettings>? previous,
        ImmutableDictionary<string, ProviderSettings> current)
    {
        previous ??= ImmutableDictionary<string, ProviderSettings>.Empty;
        if (ReferenceEquals(previous, current))
        {
            return false;
        }

        var keys = new HashSet<string>(previous.Keys, StringComparer.Ordinal);
        keys.UnionWith(current.Keys);
        foreach (var key in keys)
        {
            var previousWeight = previous.TryGetValue(key, out var p) ? p.SearchWeight : ProviderSearchWeight.Normal;
            var currentWeight = current.TryGetValue(key, out var c) ? c.SearchWeight : ProviderSearchWeight.Normal;
            if (previousWeight != currentWeight)
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _fallbackUpdateManager.Dispose();
        _searchTelemetry.Dispose();

        _tlcManager.PropertyChanged -= TlcManager_PropertyChanged;
        _tlcManager.TopLevelCommands.CollectionChanged -= Commands_CollectionChanged;
        _tlcManager.PinnedCommands.CollectionChanged -= PinnedCommands_CollectionChanged;
        _appStateService.StateChanged -= AppStateService_StateChanged;

        AllAppsCommandProvider.Page.PropChanged -= AllApps_PropChanged;

        if (_settingsService is not null)
        {
            _settingsService.SettingsChanged -= SettingsChangedHandler;
        }

        WeakReferenceMessenger.Default.UnregisterAll(this);
        GC.SuppressFinalize(this);
    }
}
