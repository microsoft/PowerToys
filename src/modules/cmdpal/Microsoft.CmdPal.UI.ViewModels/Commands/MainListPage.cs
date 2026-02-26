// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*
 #define CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
 #define CMDPAL_FF_MAINPAGE_TIME_FALLBACK_UPDATES
*/

using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Core.Common.Helpers;
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
public sealed partial class MainListPage : DynamicListPage,
    IRecipient<ClearSearchMessage>,
    IRecipient<UpdateFallbackItemsMessage>,
    IDisposable
{
    // Throttle for raising items changed events from external sources
    private static readonly TimeSpan RaiseItemsChangedThrottle = TimeSpan.FromMilliseconds(100);

    // Throttle for raising items changed events from user input - we want this to feel more responsive, so a shorter throttle.
    private static readonly TimeSpan RaiseItemsChangedThrottleForUserInput = TimeSpan.FromMilliseconds(50);

    // For individual fallback item updates - if an item takes longer than this, we will detach it
    // and continue with others.
    private static readonly TimeSpan FallbackItemSlowTimeout = TimeSpan.FromMilliseconds(200);

#if CMDPAL_FF_MAINPAGE_TIME_FALLBACK_UPDATES
    // For reporting only - if an item takes longer than this, we'll log it.
    private static readonly TimeSpan FallbackItemUltraSlowTimeout = TimeSpan.FromMilliseconds(1000);
#endif

    // Initial number of workers to use for fallback updates.
    private const int InitialFallbackWorkers = 2;

    // Upper limit of threads in case things go awry
    private const int MaximumFallbackWorkersMaxThreads = 32;

    // Per-command limit on concurrent in-flight COM calls. Prevents a single
    // misbehaving extension from monopolizing the pool across overlapping query batches.
    // Sized so worst-case saturation (all extensions slow simultaneously) fills the pool:
    // ~8 providers × 4 = 32 == pool max.
    private const int MaxInflightPerFallback = 4;

    private readonly ConcurrentDictionary<string, InflightCounter> _inflightFallbacks = new();

    // Dedicated background threads for fallback COM/RPC calls so they never block the
    // ThreadPool. Stuck extensions consume a dedicated thread, not a pool thread.
    // Max is intentionally above ProcessorCount: blocked threads consume no CPU, so
    // core count is not the right ceiling. Pool expands on demand and shrinks when idle.
    private readonly DedicatedThreadPool _fallbackThreadPool = new(minThreads: InitialFallbackWorkers, maxThreads: MaximumFallbackWorkersMaxThreads, name: "Fallbacks");

    private readonly ThrottledDebouncedAction _refreshThrottledDebouncedAction;
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

#if CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
    private DateTimeOffset _last = DateTimeOffset.UtcNow;
#endif

#if CMDPAL_FF_MAINPAGE_TIME_FALLBACK_UPDATES
    private ulong _updateBatchCounter;
#endif

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
                    RaiseItemsChanged();
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
            _refreshThrottledDebouncedAction.Invoke();
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

            BeginUpdateFallbacks(SearchText, [.. specialFallbacks, .. commonFallbacks], token);

            if (token.IsCancellationRequested)
            {
                return;
            }

            // Cleared out the filter text? easy. Reset _filteredItems, and bail out.
            if (string.IsNullOrWhiteSpace(newSearch))
            {
                _filteredItemsIncludesApps = _includeApps;
                ClearResults();
                var wasAlreadyEmpty = string.IsNullOrWhiteSpace(oldSearch);
                _refreshThrottledDebouncedAction.Invoke(wasAlreadyEmpty ? null : TimeSpan.Zero);

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
                    var pinnedApps = PinnedAppsManager.Instance.GetPinnedAppIdentifiers();

                    if (pinnedApps.Length > 0)
                    {
                        newApps = allNewApps.Where(w => pinnedApps.IndexOf(w.AppIdentifier) < 0);
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

                _refreshThrottledDebouncedAction.Invoke(adjustedInterval);
            }
            else
            {
                _refreshThrottledDebouncedAction.Invoke();
            }

#if CMDPAL_FF_MAINPAGE_TIME_RAISE_ITEMS
            var listPageUpdatedTimestamp = stopwatch.ElapsedMilliseconds;
            Logger.LogDebug($"Render items with '{newSearch}' in {listPageUpdatedTimestamp}ms /d {listPageUpdatedTimestamp - filterDoneTimestamp}ms");
#endif

            stopwatch.Stop();
        }
    }

    private void BeginUpdateFallbacks(string query, IReadOnlyList<TopLevelViewModel> commands, CancellationToken cancellationToken)
    {
        if (commands.Count == 0 || string.IsNullOrWhiteSpace(query))
        {
            return;
        }

#if CMDPAL_FF_MAINPAGE_TIME_FALLBACK_UPDATES
        var batchNumber = _updateBatchCounter++;
        Logger.LogDebug($"UpdateFallbacks: Batch start {batchNumber} for query '{query}'");
#endif

        // Adaptive dispatch on dedicated threads — same semantics as the old
        // ParallelHelper.AdaptiveForEachAdaptiveAsync, but without any ThreadPool involvement:
        //   • Start 2 workers; each claims commands via a shared atomic index (FIFO, no double-work).
        //   • If a command is slow (> FallbackItemSlowTimeout), the worker spawns a sibling so
        //     remaining fast commands aren't blocked waiting in the worker's loop.
        //   • _refreshThrottledDebouncedAction.Invoke() is called on the dedicated thread when a
        //     result changes (ThrottledDebouncedAction is thread-safe).
        var sharedIndex = 0;
        var totalCommands = commands.Count;

        void Worker()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var i = Interlocked.Increment(ref sharedIndex) - 1;
                if (i >= totalCommands)
                {
                    return;
                }

                var command = commands[i];
                var counter = _inflightFallbacks.GetOrAdd(command.Id, static _ => new InflightCounter());
                if (!counter.TryClaim(MaxInflightPerFallback))
                {
                    // At capacity — store this query as a pending retry so it runs
                    // when one of the in-flight calls finishes. Latest query wins.
                    var pendingCommand = command;
                    var pendingQuery = query;
                    var pendingCt = cancellationToken;
                    counter.SetPending(() => RetryFallbackUpdate(pendingCommand, pendingQuery, pendingCt, counter), pendingCt);
                    continue;
                }

                // Arm a timer: if this item is still running after FallbackItemSlowTimeout,
                // spawn a sibling worker WHILE we're blocked in the COM call so remaining
                // commands don't have to wait for us to finish first.
                // Linking to cancellationToken cancels the timer immediately when the outer
                // query is abandoned — preventing stale siblings from being scheduled.
                // Disposing the linked CTS at iteration end removes the link registration.
                using var expandCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                expandCts.CancelAfter(FallbackItemSlowTimeout);
                expandCts.Token.Register(() =>
                {
                    // Fires on timeout (slow item) OR on outer cancellation.
                    // Only spawn a sibling on timeout — when the outer query is still active.
                    if (!cancellationToken.IsCancellationRequested && Volatile.Read(ref sharedIndex) < totalCommands)
                    {
                        _ = _fallbackThreadPool.QueueAsync(Worker, cancellationToken);
                    }
                });

                var changed = false;
                try
                {
#if CMDPAL_FF_MAINPAGE_TIME_FALLBACK_UPDATES
                    var sw = Stopwatch.StartNew();
                    Logger.LogDebug($"UpdateFallbacks: Worker: command id '{command.Id}', '{command.DisplayTitle}' updating with '{query}'");
#endif
                    changed = command.SafeUpdateFallbackTextSynchronous(query);
#if CMDPAL_FF_MAINPAGE_TIME_FALLBACK_UPDATES
                    var elapsed = sw.Elapsed;
                    var tail = elapsed > FallbackItemSlowTimeout ? " is slow" : string.Empty;
                    if (elapsed > FallbackItemUltraSlowTimeout)
                    {
                        tail += " <---------------- (ultra slow)";
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    Logger.LogDebug($"UpdateFallbacks: Worker: command id '{command.Id}', '{command.DisplayTitle}' updated with '{query}' processed in {elapsed}, has {(changed ? "changed" : "not changed")} and title is '{command.Title}'{tail}");
#endif
                }
                catch (Exception ex)
                {
                    Logger.LogError($"UpdateFallbacks: Worker: command id '{command.Id}', '{command.DisplayTitle}' failed to update fallback text with '{query}'", ex);
                }
                finally
                {
                    counter.Release();
                    DispatchPending(counter.TakePending());
                }

                // Guard against a stale refresh if the COM call returned after cancellation.
                if (changed && !cancellationToken.IsCancellationRequested)
                {
                    _refreshThrottledDebouncedAction.Invoke();
                }
            }
        }

        // Dispatches a pending work item to the dedicated pool. The pending's
        // own CT is forwarded so the pool can skip it at dequeue time when the
        // originating query batch has been superseded by a newer keystroke.
        void DispatchPending(PendingWork? pending)
        {
            if (pending == null)
            {
                return;
            }

            _ = _fallbackThreadPool.QueueAsync(pending.Work, pending.CancellationToken);
        }

        // One-shot retry for a command that was skipped due to MaxInflightPerFallback.
        // Claims a slot, runs the COM call, releases, and propagates the next pending (if any).
        void RetryFallbackUpdate(TopLevelViewModel cmd, string q, CancellationToken ct, InflightCounter ctr)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (!ctr.TryClaim(MaxInflightPerFallback))
            {
                // Still at capacity (a newer worker claimed the freed slot first).
                // The pending was already consumed from TakePending, so it's dropped here.
                return;
            }

            var changed = false;
            try
            {
                changed = cmd.SafeUpdateFallbackTextSynchronous(q);
            }
            catch (Exception ex)
            {
                Logger.LogError($"UpdateFallbacks: Pending retry: command id '{cmd.Id}', '{cmd.DisplayTitle}' failed with '{q}'", ex);
            }
            finally
            {
                ctr.Release();
                DispatchPending(ctr.TakePending());
            }

            if (changed && !ct.IsCancellationRequested)
            {
                _refreshThrottledDebouncedAction.Invoke();
            }
        }

        for (var i = 0; i < Math.Min(InitialFallbackWorkers, totalCommands); i++)
        {
            _ = _fallbackThreadPool.QueueAsync(Worker, cancellationToken);
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

    public void Receive(UpdateFallbackItemsMessage message)
    {
        _refreshThrottledDebouncedAction.Invoke();
    }

    private void SettingsChangedHandler(SettingsModel sender, object? args) => HotReloadSettings(sender);

    private void HotReloadSettings(SettingsModel settings) => ShowDetails = settings.ShowAppDetails;

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _fallbackThreadPool.Dispose();
        _inflightFallbacks.Clear();

        _tlcManager.PropertyChanged -= TlcManager_PropertyChanged;
        _tlcManager.TopLevelCommands.CollectionChanged -= Commands_CollectionChanged;

        if (_settings is not null)
        {
            _settings.SettingsChanged -= SettingsChangedHandler;
        }

        WeakReferenceMessenger.Default.UnregisterAll(this);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Thread-safe counter for tracking concurrent in-flight calls per command,
    /// with a single pending retry slot for queries that couldn't claim immediately.
    /// </summary>
    /// <summary>
    /// A pending work item paired with the cancellation token of the query
    /// batch that created it, so the pool can skip it at dequeue time when
    /// a newer keystroke has already superseded the query.
    /// </summary>
    private sealed record PendingWork(Action Work, CancellationToken CancellationToken);

    /// <summary>
    /// Thread-safe counter for tracking concurrent in-flight calls per command,
    /// with a single pending retry slot for queries that couldn't claim immediately.
    /// </summary>
    private sealed class InflightCounter
    {
        private int _count;

        // Latest pending work item. Only one is stored; newer queries overwrite older ones.
        private PendingWork? _pendingWork;

        /// <summary>
        /// Try to claim a slot. Returns true if the count was below
        /// <paramref name="max"/> and was incremented; false if at capacity.
        /// </summary>
        public bool TryClaim(int max)
        {
            while (true)
            {
                var current = Volatile.Read(ref _count);
                if (current >= max)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref _count, current + 1, current) == current)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Stores a pending work item to run when the next slot opens.
        /// Overwrites any previously stored item — latest query always wins.
        /// </summary>
        public void SetPending(Action work, CancellationToken ct) => Interlocked.Exchange(ref _pendingWork, new PendingWork(work, ct));

        /// <summary>
        /// Atomically removes and returns any pending work item, or null if none.
        /// </summary>
        public PendingWork? TakePending() => Interlocked.Exchange(ref _pendingWork, null);

        public void Release() => Interlocked.Decrement(ref _count);
    }
}
