// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*
 #define CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
*/

using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.AppList;
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

    // Longest query to filter fuzzy app matches on. This prevents weak app matches on short queries.
    private const int ShortQueryAppFilterMaxLength = 2;

    // Minimum title tier for a short query without an explicit metadata or user-alias match.
    private const RankTier ShortQueryAppFilterMinTier = RankTier.AcronymWordBoundary;

    private static readonly IComparer<RoScored<IListItem>> SearchResultComparer = Comparer<RoScored<IListItem>>.Create(static (left, right) =>
    {
        var scoreComparison = right.Score.CompareTo(left.Score);
        return scoreComparison != 0 ? scoreComparison : CultureInfo.CurrentCulture.CompareInfo.Compare(left.Item.Title, right.Item.Title, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);
    });

    private readonly FallbackUpdateManager _fallbackUpdateManager;
    private readonly ThrottledDebouncedAction _refreshThrottledDebouncedAction;
    private readonly TopLevelCommandManager _tlcManager;
    private readonly AliasManager _aliasManager;
    private readonly ISettingsService _settingsService;
    private readonly IAppStateService _appStateService;
    private readonly IAppListItemSource _appListItemSource;
    private readonly ScoringFunction<IListItem> _globalFallbackScoringFunction;
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

    private DefaultViewCache? _defaultViewCache;
    private volatile RecentCommandsManager _recentCommands;
    private int _defaultViewGeneration;
    private RecentCommandsPlacement _recentCommandsOnHome;
    private int _recentCommandsDisplayLimit = SettingsModel.DefaultRecentCommandsDisplayLimit;

    private RoScored<IListItem>[]? _filteredItems;
    private RoScored<IListItem>[]? _filteredApps;

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
    private bool _appItemsChangedWhileLoading;
    private AppListItemSnapshot _lastAppSnapshot;
    private int _lastAppResultLimit;
    private Tuple<RecentCommandsManager, AppListItemSnapshot, RecentCommandsManager>? _appHistoryCache;

    // Last per-provider settings we reacted to, so a settings reload can tell whether any
    // provider's search weight actually changed and only then re-rank the active query.
    private ImmutableDictionary<string, ProviderSettings>? _lastProviderSettingsSnapshot;

    private int AppResultLimit => _appListItemSource.TopLevelResultLimit;

    private InterlockedBoolean _fullRefreshRequested;
    private InterlockedBoolean _forceSearchReapply;
    private InterlockedBoolean _refreshRunning;
    private InterlockedBoolean _refreshRequested;

    private CancellationTokenSource? _cancellationTokenSource;

#if CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
    private DateTimeOffset _last = DateTimeOffset.UtcNow;
#endif

    /// <summary>Initializes a new instance of the <see cref="MainListPage"/> class. Creates Home over extension commands, shared Apps snapshots, aliases, preferences, and usage history.</summary>
    public MainListPage(
        TopLevelCommandManager topLevelCommandManager,
        AliasManager aliasManager,
        IFuzzyMatcherProvider fuzzyMatcherProvider,
        ISettingsService settingsService,
        IAppStateService appStateService,
        IAppListItemSource appListItemSource)
    {
        ArgumentNullException.ThrowIfNull(appListItemSource);

        Id = "com.microsoft.cmdpal.home";
        Title = Resources.builtin_home_name;
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.altform-unplated_targetsize-256.png");
        PlaceholderText = Properties.Resources.builtin_main_list_page_searchbar_placeholder;

        _settingsService = settingsService;
        _aliasManager = aliasManager;
        _appStateService = appStateService;
        _recentCommands = _appStateService.State.RecentCommands;
        _appListItemSource = appListItemSource;
        _lastAppSnapshot = _appListItemSource.GetSnapshot();
        _lastAppResultLimit = AppResultLimit;
        _tlcManager = topLevelCommandManager;
        _fuzzyMatcherProvider = fuzzyMatcherProvider;
        _globalFallbackScoringFunction = (in query, item) => ScoreTopLevelItem(
            in query,
            item,
            _appStateService.State.RecentCommands,
            _fuzzyMatcherProvider.Current,
            null,
            ResolveProviderSearchWeight);
        _fallbackScoringFunction = (in _, item) => ScoreFallbackItem(item, _settingsService.Settings.FallbackRanks);

        _tlcManager.PropertyChanged += TlcManager_PropertyChanged;
        _tlcManager.TopLevelCommands.CollectionChanged += Commands_CollectionChanged;
        _tlcManager.PinnedCommandsChanged += PinnedCommands_Changed;

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

        // The app list item source will kick off a BG thread to start loading apps.
        // We just want to know when it is done.
        _appListItemSource.Changed += AppListItemSource_Changed;

        WeakReferenceMessenger.Default.Register<ClearSearchMessage>(this);
        WeakReferenceMessenger.Default.Register<UpdateFallbackItemsMessage>(this);

        _settingsService.SettingsChanged += SettingsChangedHandler;
        HotReloadSettings(_settingsService.Settings);
        _includeApps = _tlcManager.IsProviderActive(AllAppsCommandProvider.WellKnownId);

        IsLoading = ActuallyLoading();
    }

    private void TlcManager_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsLoading))
        {
            IsLoading = ActuallyLoading();
        }
    }

    private void AppListItemSource_Changed(object? sender, EventArgs e)
    {
        var appSnapshot = _appListItemSource.GetSnapshot();
        var appsChanged = !ReferenceEquals(_lastAppSnapshot.VisibleItems, appSnapshot.VisibleItems)
            || _lastAppSnapshot.ExecutableNameMatchMode != appSnapshot.ExecutableNameMatchMode
            || !ReferenceEquals(_lastAppSnapshot.ExecutionAliasOwners, appSnapshot.ExecutionAliasOwners);
        var resultLimit = AppResultLimit;
        var resultLimitChanged = _lastAppResultLimit != resultLimit;
        _lastAppResultLimit = resultLimit;
        _lastAppSnapshot = appSnapshot;
        _appItemsChangedWhileLoading |= appsChanged;
        IsLoading = ActuallyLoading();
        var shouldReapplyApps = !_appListItemSource.IsLoading && _appItemsChangedWhileLoading;
        if (appsChanged || shouldReapplyApps)
        {
            InvalidateDefaultView();
            if (!_appListItemSource.IsLoading && _recentCommandsOnHome != RecentCommandsPlacement.Hidden)
            {
                RequestRefresh(fullRefresh: false);
            }
        }

        if (!_appListItemSource.IsLoading)
        {
            _appItemsChangedWhileLoading = false;
        }

        if (shouldReapplyApps
            && _includeApps
            && !string.IsNullOrWhiteSpace(SearchText))
        {
            ReapplySearchInBackground(force: true);
        }
        else if (resultLimitChanged && _includeApps && !string.IsNullOrWhiteSpace(SearchText))
        {
            RequestRefresh(fullRefresh: false);
        }
    }

    private void PinnedCommands_Changed(object? sender, EventArgs e)
    {
        InvalidateDefaultView();
        RaiseItemsChanged();
    }

    private void Commands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _includeApps = _tlcManager.IsProviderActive(AllAppsCommandProvider.WellKnownId);
        InvalidateDefaultView();
        if (_includeApps != _filteredItemsIncludesApps)
        {
            ReapplySearchInBackground();
        }
        else if (!string.IsNullOrWhiteSpace(SearchText)
            && (e.Action == NotifyCollectionChangedAction.Reset
                || e.NewItems?.OfType<TopLevelViewModel>().Any(IsAppCommand) == true
                || e.OldItems?.OfType<TopLevelViewModel>().Any(IsAppCommand) == true))
        {
            ReapplySearchInBackground(force: true);
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
        if (_recentCommandsOnHome != RecentCommandsPlacement.Hidden)
        {
            InvalidateDefaultView();
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

    private void ReapplySearchInBackground(bool force = false)
    {
        if (force)
        {
            _forceSearchReapply.Set();
        }

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
                var force = _forceSearchReapply.Clear();
                lock (_tlcManager.TopLevelCommands)
                {
                    if (!force && _filteredItemsIncludesApps == _includeApps)
                    {
                        break;
                    }
                }

                var currentSearchText = SearchText;
                UpdateSearchTextCore(currentSearchText, currentSearchText, isUserInput: false, forceReset: force);
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
        var validScoredFallbacks = ScoreDeferredFallbacks(_globalFallbackSources, _globalFallbackQuery, _globalFallbackScoringFunction);

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

    // Admission happens during scoring, so rendering and telemetry count the same app results.

    /// <summary>Limits the number of scored apps admitted to Home using the current app result limit.</summary>
    internal static int GetVisibleAppCount(RoScored<IListItem>[]? scoredApps, int appResultLimit)
        => Math.Min(scoredApps?.Length ?? 0, appResultLimit);

    private IListItem[] GetDefaultViewItems()
    {
        var (_, pinned, recent, regular) = GetDefaultViewCache();

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

        if (_recentCommandsOnHome == RecentCommandsPlacement.BeforePinned)
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

    private DefaultViewCache GetDefaultViewCache()
    {
        var existing = Volatile.Read(ref _defaultViewCache);
        var generation = Volatile.Read(ref _defaultViewGeneration);
        if (existing?.Generation == generation)
        {
            return existing;
        }

        var rebuilt = BuildDefaultViewCache(existing, generation);
        if (generation == Volatile.Read(ref _defaultViewGeneration))
        {
            Interlocked.CompareExchange(ref _defaultViewCache, rebuilt, existing);
        }

        var current = Volatile.Read(ref _defaultViewCache);
        return current?.Generation == Volatile.Read(ref _defaultViewGeneration) ? current : rebuilt;
    }

    private DefaultViewCache BuildDefaultViewCache(DefaultViewCache? existing, int generation)
    {
        var pinnedSettings = _tlcManager.GetPinnedCommandsSnapshot();

        TopLevelViewModel[] allCommands;
        lock (_tlcManager.TopLevelCommands)
        {
            allCommands = [.. _tlcManager.TopLevelCommands];
        }

        IEnumerable<string> recentCommandIds = _recentCommandsOnHome == RecentCommandsPlacement.Hidden
            ? []
            : _recentCommands.EnumerateRecentCommandIds();
        var sections = TopLevelCommandResolver.Resolve(
            pinnedSettings,
            recentCommandIds,
            allCommands,
            _appListItemSource.GetSnapshot(),
            includeApps: _includeApps,
            recentCommandLimit: _recentCommandsDisplayLimit,
            recentCommandsFirst: _recentCommandsOnHome == RecentCommandsPlacement.BeforePinned);

        var recent = sections.Recent
            .Select(item => (IListItem)RecentCommandListItem.CreateOrReuse(
                existing?.Recent,
                item,
                IdForTopLevelOrAppItem(item)))
            .ToArray();
        return new(generation, [.. sections.Pinned], recent, [.. sections.Regular]);
    }

    private void InvalidateDefaultView()
    {
        Interlocked.Increment(ref _defaultViewGeneration);
    }

    private void ClearResults()
    {
        _filteredItems = null;
        _filteredApps = null;
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

    private void UpdateSearchTextCore(string oldSearch, string newSearch, bool isUserInput, bool forceReset = false)
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
        AppListItemSnapshot appSnapshot;
        bool includeAppsSnapshot;

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
            appSnapshot = _appListItemSource.GetSnapshot();

            // A query that doesn't extend the old one, or a change in app inclusion, means we
            // can't re-use the previous results and have to rebuild from the full catalog. On an
            // extend we re-score only the previously matched non-app commands.
            var reset = forceReset || !newSearch.StartsWith(oldSearch, StringComparison.CurrentCultureIgnoreCase)
                || _filteredItemsIncludesApps != includeAppsSnapshot;

            var prevFilteredItems = reset ? null : _filteredItems;
            var prevApps = reset ? null : _filteredApps;
            var prevFallbacks = reset ? null : _fallbackItems;

            IEnumerable<IListItem> newFilteredItems = prevFilteredItems is not null
                ? prevFilteredItems.Select(s => s.Item)
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
            if (!newFilteredItems.Any() && (prevApps is null || prevApps.Length == 0))
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
            }

            // Metadata thresholds and short-query admission can gain matches as a query grows.
            // Reconsider every app, including pins, rather than narrowing to the previous results.
            var appCommands = commands.Where(IsAppCommand)
                .Where(item => includeAppsSnapshot && appSnapshot.GetApp(item.Id) is not null)
                .ToArray();
            newFilteredItems = newFilteredItems
                .Where(item => item is not TopLevelViewModel topLevel || !IsAppCommand(topLevel))
                .Concat(appCommands);

            var pinnedAppIds = appCommands
                .Select(item => appSnapshot.GetApp(item.Id)!.Command!.Id)
                .ToHashSet(StringComparer.Ordinal);
            var newApps = includeAppsSnapshot
                ? appSnapshot.VisibleItems.Where(item => !pinnedAppIds.Contains(item.Command!.Id))
                : [];

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
        var recent = GetAppHistory(_appStateService.State.RecentCommands, appSnapshot);
        recent.PrewarmIndex();
        var matcher = _fuzzyMatcherProvider.Current;
        var settings = _settingsService.Settings;
        var scoringNow = DateTimeOffset.UtcNow;

        // Precompute from the snapshotted newSearch, not the live SearchText, which a newer
        // keystroke may already have advanced past.
        var searchQuery = matcher.PrecomputeQuery(newSearch);
        AppSearch? appSearch = null;
        if (includeAppsSnapshot)
        {
            _appListItemSource.RequestExecutionAliasRefresh();
            appSearch = new AppSearch(newSearch, matcher, appSnapshot.ExecutableNameMatchMode, appSnapshot.GetExecutionAliasOwner(newSearch));
        }

        // Every installed app belongs to the well-known AllApps provider, so its weight is constant
        // for the whole pass and we resolve it once instead of once per app.
        var appsProviderWeight = ResolveProviderSearchWeight(settings, AllAppsCommandProvider.WellKnownId);
        Func<IListItem, ProviderSearchWeight> commandsProviderLookup = item => ResolveProviderSearchWeight(settings, item);
        Func<IListItem, ProviderSearchWeight> appsProviderLookup = _ => appsProviderWeight;

        ScoringFunction<IListItem> commandsScorer = (in FuzzyQuery q, IListItem item) =>
            ScoreTopLevelItem(
                in q,
                item,
                recent,
                matcher,
                appSearch,
                commandsProviderLookup,
                scoringNow,
                item is TopLevelViewModel topLevel && IsAppCommand(topLevel) ? appSnapshot.GetApp(topLevel.Id) : null);
        ScoringFunction<IListItem> appsScorer = (in FuzzyQuery q, IListItem item) =>
            ScoreTopLevelItem(in q, item, recent, matcher, appSearch, appsProviderLookup, scoringNow);

        var scoredFilteredItems = InternalListHelpers.FilterListWithScores(itemsSource, searchQuery, commandsScorer, SearchResultComparer);

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
            scoredApps = InternalListHelpers.FilterListWithScoresParallel(appsSource, searchQuery, appsScorer, SearchResultComparer);

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

            _filteredItemsIncludesApps = includeAppsSnapshot;

            _filteredItems = scoredFilteredItems;
            _fallbackItems = scoredFallbackItems;

            // Snapshot the global fallbacks and query, but score them later on the render path,
            // since their titles are still resolving asynchronously (BeginUpdate, above).
            _globalFallbackSources = globalFallbackSources;
            _globalFallbackQuery = searchQuery;

            // With no apps source, publish null so a rebuild clears any stale set, matching the old
            // ClearResults behavior.
            _filteredApps = appsSource.Count > 0 ? scoredApps : null;

            if (isUserInput)
            {
                deterministicResultCount = (_filteredItems?.Length ?? 0)
                    + GetVisibleAppCount(_filteredApps, AppResultLimit);
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

    private static bool IsAppCommand(TopLevelViewModel item)
        => item.CommandProviderId == AllAppsCommandProvider.WellKnownId && item.CommandViewModel.IsInvokableCommand;

    private bool ActuallyLoading()
    {
        return _appListItemSource.IsLoading || _tlcManager.IsLoading;
    }

    // Almost verbatim ListHelpers.ScoreListItem. Fallbacks tier by the same title match as any
    // other item, except a non-match floors to FallbackFloor (see MainListRanker.ClassifyTier) so
    // the handler stays available instead of dropping.
    // Apps require a prepared search carrying the captured query and executable-name policy.

    /// <summary>Scores Home items by relevance tier, then usage history and provider weight within that tier.</summary>
    /// <param name="query">The precomputed query shared by the current scoring pass.</param>
    /// <param name="topLevelOrAppItem">The command row being scored.</param>
    /// <param name="history">The usage history projected to current command IDs.</param>
    /// <param name="precomputedFuzzyMatcher">The matcher used for non-app text targets.</param>
    /// <param name="appSearch">The captured Apps search policy; required when scoring an app.</param>
    /// <param name="providerWeightLookup">Optional provider preference applied within the relevance tier.</param>
    /// <param name="now">Optional clock value for reproducible recency scoring.</param>
    /// <param name="app">The underlying app for a wrapped saved command, or null for direct inference.</param>
    /// <returns>A positive rank for an admitted result, or zero when the item should be excluded.</returns>
    internal static int ScoreTopLevelItem(
        in FuzzyQuery query,
        IListItem topLevelOrAppItem,
        IRecentCommandsManager history,
        IPrecomputedFuzzyMatcher precomputedFuzzyMatcher,
        AppSearch? appSearch,
        Func<IListItem, ProviderSearchWeight>? providerWeightLookup = null,
        DateTimeOffset? now = null,
        AppListItem? app = null)
    {
        app ??= topLevelOrAppItem as AppListItem;
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
            extensionDisplayNameTarget = app is null ? topLevel.GetExtensionNameTarget(precomputedFuzzyMatcher) : null;

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

        double lexicalQuality;
        bool matchedLexically;
        var exactMetadataMatch = false;
        var exactExecutableMatch = false;
        var preferredExecutionAliasMatch = false;
        if (app is not null)
        {
            ArgumentNullException.ThrowIfNull(appSearch);
            var match = appSearch.Evaluate(app);
            lexicalQuality = match.LexicalScore;
            matchedLexically = match.HasMatch;
            exactMetadataMatch = match.IsExactMetadataMatch;
            exactExecutableMatch = match.IsExactExecutableMatch;
            preferredExecutionAliasMatch = match.IsPreferredExecutionAliasMatch;
        }
        else
        {
            var (titleTarget, subtitleTarget) = topLevelOrAppItem is IPrecomputedListItem precomputedItem
                ? (precomputedItem.GetTitleTarget(precomputedFuzzyMatcher), precomputedItem.GetSubtitleTarget(precomputedFuzzyMatcher))
                : (precomputedFuzzyMatcher.PrecomputeTarget(title), precomputedFuzzyMatcher.PrecomputeTarget(topLevelOrAppItem.Subtitle));

            // Admission uses raw scores before the description penalty, which can be negative.
            var nameScore = precomputedFuzzyMatcher.Score(query, titleTarget);
            var rawSubtitleScore = precomputedFuzzyMatcher.Score(query, subtitleTarget);
            var rawExtensionScore = extensionDisplayNameTarget is { } extTarget ? precomputedFuzzyMatcher.Score(query, extTarget) : 0;
            lexicalQuality = Math.Max(Math.Max(nameScore, (rawSubtitleScore - 4) / 2.0), isFallback ? 1 : 0) + (rawExtensionScore / 1.5);
            matchedLexically = nameScore > 0 || rawSubtitleScore > 0 || rawExtensionScore > 0;
        }

        // The hard tier decides ordering; frecency and the alias-substring nudge only
        // reorder items that already share a tier. ClassifyTier returns None precisely when
        // nothing matched (no lexical, alias, or fallback signal), so this single gate also
        // filters non-matches - no separate pre-check is needed.
        var tier = MainListRanker.ClassifyTier(query.Original, title, isFallback, isAliasMatch, isAliasSubstringMatch, matchedLexically, exactExecutableMatch, preferredExecutionAliasMatch);
        if (tier == RankTier.None)
        {
            return 0;
        }

        // Apply short-query admission before publication for both ordinary and pinned apps.
        // Other metadata stays in the fuzzy tier, below stronger title matches.
        if (app is not null && appSearch!.QueryLength is > 0 and <= ShortQueryAppFilterMaxLength
            && tier < ShortQueryAppFilterMinTier && !exactMetadataMatch && !isAliasSubstringMatch)
        {
            return 0;
        }

        var frecencyWeight = history.GetCommandHistoryWeight(app?.Command?.Id ?? id, now ?? DateTimeOffset.UtcNow);
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

    /// <summary>Gets a cached ranking view that merges legacy and current app history, including hidden pins.</summary>
    /// <remarks>The stored history is not rewritten by this projection.</remarks>
    internal RecentCommandsManager GetAppHistory(RecentCommandsManager history, AppListItemSnapshot appSnapshot)
    {
        var cached = Volatile.Read(ref _appHistoryCache);
        if (cached is not null && ReferenceEquals(cached.Item1, history) && ReferenceEquals(cached.Item2, appSnapshot))
        {
            return cached.Item3;
        }

        var projected = history.WithCanonicalCommandIds(id => appSnapshot.GetApp(id)?.Command?.Id ?? id);
        Volatile.Write(ref _appHistoryCache, Tuple.Create(history, appSnapshot, projected));
        return projected;
    }

    public void UpdateHistory(IListItem topLevelOrAppItem)
    {
        var appSnapshot = _appListItemSource.GetSnapshot();
        var id = IdForTopLevelOrAppItem(topLevelOrAppItem);
        if (topLevelOrAppItem is AppListItem || (topLevelOrAppItem is TopLevelViewModel topLevel && IsAppCommand(topLevel)))
        {
            id = appSnapshot.GetApp(id)?.Command?.Id ?? id;
        }

        _appStateService.UpdateState(state => state with
        {
            RecentCommands = state.RecentCommands.WithHistoryItem(id),
        });

        _searchTelemetry.ReportSelection(topLevelOrAppItem, _resultsSeparator, _fallbacksSeparator);
    }

    internal static string IdForTopLevelOrAppItem(IListItem topLevelOrAppItem)
    {
        if (topLevelOrAppItem is RecentCommandListItem recentItem)
        {
            return recentItem.CommandId;
        }
        else if (topLevelOrAppItem is TopLevelViewModel topLevel)
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
        InvalidateDefaultView();
        RequestRefresh(fullRefresh: false);
    }

    private void SettingsChangedHandler(ISettingsService sender, SettingsModel args) => HotReloadSettings(args);

    private void HotReloadSettings(SettingsModel settings)
    {
        ShowDetails = settings.ShowAppDetails;

        if (_recentCommandsOnHome != settings.RecentCommandsOnHome ||
            _recentCommandsDisplayLimit != settings.RecentCommandsDisplayLimit)
        {
            _recentCommandsOnHome = settings.RecentCommandsOnHome;
            _recentCommandsDisplayLimit = settings.RecentCommandsDisplayLimit;
            InvalidateDefaultView();
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
        _tlcManager.PinnedCommandsChanged -= PinnedCommands_Changed;
        _appStateService.StateChanged -= AppStateService_StateChanged;

        _appListItemSource.Changed -= AppListItemSource_Changed;

        if (_settingsService is not null)
        {
            _settingsService.SettingsChanged -= SettingsChangedHandler;
        }

        WeakReferenceMessenger.Default.UnregisterAll(this);
        GC.SuppressFinalize(this);
    }

    private sealed record DefaultViewCache(
        int Generation,
        IListItem[] Pinned,
        IListItem[] Recent,
        IListItem[] Regular);
}
