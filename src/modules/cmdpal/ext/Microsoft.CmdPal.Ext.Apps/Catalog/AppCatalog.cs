// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

public sealed partial class AppCatalog : IAppCatalog
{
    public event EventHandler<AppCatalogChangedEventArgs>? Changed;

    public event EventHandler? RefreshStateChanged;

    public event EventHandler<AppVisibilityChangedEventArgs>? VisibilityChanged;

    private const int MaxIncrementalPathChangesPerSource = 256;

    private static readonly TimeSpan DefaultInvalidationDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CachedSourceReconciliationDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan BackgroundReconciliationInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(40)];

    private readonly Lock _stateLock = new();
    private readonly IAppSourceProvider _sourceProvider;
    private readonly Dictionary<string, IAppSource> _sourcesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceRefreshRequest> _pendingRefreshes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceRefreshRequest> _debouncedRefreshes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceRefreshRequest> _pausedBackgroundRefreshes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource> _initialSourcePublications = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceRetryState> _sourceRetries = new(StringComparer.Ordinal);
    private readonly IAppCatalogCache _cache;
    private readonly IAppVisibilityStore _visibilityStore;
    private readonly IReadOnlyList<IAppCatalogFilter> _filters;
    private readonly MEL.ILogger<AppCatalog> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _invalidationDelay;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly CancellationToken _disposeToken;
    private readonly ITimer _retryTimer;
    private readonly ITimer _reconciliationTimer;

    private CancellationTokenSource? _backgroundCancellation;

    private IReadOnlyList<IAppSource> _sources;

    private PublishedState _publishedState = PublishedState.Empty;
    private Task? _initializationTask;
    private Task? _refreshTask;
    private long _sourceProviderGeneration;
    private bool _isRefreshing;
    private bool _disposed;
    private bool _invalidationFlushScheduled;

    public bool IsRefreshing
    {
        get
        {
            lock (_stateLock)
            {
                return _isRefreshing;
            }
        }
    }

    internal AppCatalog(
        IAppSourceProvider sourceProvider,
        IAppCatalogCache cache,
        IAppVisibilityStore visibilityStore,
        IReadOnlyList<IAppCatalogFilter>? filters = null,
        TimeProvider? timeProvider = null,
        TimeSpan? invalidationDelay = null,
        MEL.ILogger<AppCatalog>? logger = null)
    {
        _sourceProvider = sourceProvider ?? throw new ArgumentNullException(nameof(sourceProvider));
        _sources = _sourceProvider.GetSources();
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _visibilityStore = visibilityStore ?? throw new ArgumentNullException(nameof(visibilityStore));
        _filters = filters ?? [];
        _logger = logger ?? NullLogger<AppCatalog>.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _invalidationDelay = invalidationDelay ?? DefaultInvalidationDelay;
        _disposeToken = _disposeCancellation.Token;
        _retryTimer = _timeProvider.CreateTimer(_ => QueueRetries(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _reconciliationTimer = _timeProvider.CreateTimer(_ => ReconcileInBackground(), null, BackgroundReconciliationInterval, BackgroundReconciliationInterval);

        foreach (var source in _sources)
        {
            if (!_sourcesById.TryAdd(source.Id, source))
            {
                throw new ArgumentException($"Application source ID '{source.Id}' is not unique.", nameof(sourceProvider));
            }

            source.Invalidated += OnSourceInvalidated;
        }

        foreach (var filter in _filters)
        {
            filter.Changed += OnFilterChanged;
        }

        _sourceProvider.Changed += OnSourceProviderChanged;
        _visibilityStore.Changed += OnFilterChanged;
    }

    public AppCatalogSnapshot GetSnapshot()
    {
        return Volatile.Read(ref _publishedState).Snapshot;
    }

    public Task InitializeAsync()
    {
        lock (_stateLock)
        {
            ThrowIfDisposed();
            return _initializationTask ??= Task.Run(InitializeCoreAsync);
        }
    }

    public Task RefreshAsync()
    {
        IReadOnlyList<IAppSource> sources;
        lock (_stateLock)
        {
            ThrowIfDisposed();
            sources = _sources;
        }

        return QueueFullRefresh(sources);
    }

    private async Task InitializeCoreAsync()
    {
        var sources = GetSourcesSnapshot();
        var context = AppCatalogCacheContext.Create(sources, _timeProvider.GetUtcNow());
        var cache = await _cache.LoadAsync(context, _disposeToken).ConfigureAwait(false);
        if (cache is not null)
        {
            var snapshots = new Dictionary<string, IReadOnlyList<AppCatalogItem>>(StringComparer.Ordinal);
            var cachedSources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in cache.Sources)
            {
                snapshots[source.SourceId] = source.Items;
                cachedSources.Add(source.SourceId);
            }

            PublishCachedSnapshots(snapshots, sources);

            List<IAppSource> missingSources = [];
            List<IAppSource> cachedSourcesToReconcile = [];
            foreach (var source in sources)
            {
                if (cachedSources.Contains(source.Id))
                {
                    cachedSourcesToReconcile.Add(source);
                }
                else
                {
                    missingSources.Add(source);
                }
            }

            if (cachedSourcesToReconcile.Count > 0)
            {
                _ = Task.Run(() => ReconcileCachedSourcesAfterDelayAsync(cachedSourcesToReconcile), _disposeToken);
            }

            if (missingSources.Count > 0)
            {
                await InitializeSourcesAsync(missingSources).ConfigureAwait(false);
                await RefreshInitialSourcesAsync(missingSources).ConfigureAwait(false);
            }

            return;
        }

        await InitializeSourcesAsync(sources).ConfigureAwait(false);
        await RefreshInitialSourcesAsync(GetSourcesSnapshot()).ConfigureAwait(false);
    }

    private async Task InitializeSourcesAsync(IReadOnlyList<IAppSource> sources)
    {
        foreach (var source in sources)
        {
            if (!IsActiveSource(source))
            {
                continue;
            }

            try
            {
                await source.InitializeAsync(_disposeToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException) when (!IsActiveSource(source))
            {
            }
            catch (Exception ex)
            {
                LogSourceInitializationFailed(_logger, source.Id, ex);
            }
        }
    }

    private bool IsActiveSource(IAppSource source)
    {
        lock (_stateLock)
        {
            return !_disposed && _sourcesById.TryGetValue(source.Id, out var activeSource)
                && ReferenceEquals(activeSource, source);
        }
    }

    private async Task RefreshInitialSourcesAsync(IReadOnlyList<IAppSource> sources)
    {
        try
        {
            await QueueFullRefresh(sources, waitForPublication: true).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task ReconcileCachedSourcesAfterDelayAsync(IReadOnlyList<IAppSource> sources)
    {
        try
        {
            // Establish watchers before reconciling changes that occurred while the cache was being shown.
            await InitializeSourcesAsync(sources).ConfigureAwait(false);
            await Task.Delay(CachedSourceReconciliationDelay, _disposeToken).ConfigureAwait(false);
            await QueueFullRefresh(sources, background: true).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            LogCatalogRefreshFailed(_logger, ex);
        }
    }

    private IReadOnlyList<IAppSource> GetSourcesSnapshot()
    {
        lock (_stateLock)
        {
            return _sources;
        }
    }

    private Task QueueFullRefresh(IReadOnlyList<IAppSource> sources, bool waitForPublication = false, bool background = false)
    {
        Task refreshTask;
        List<Task>? publicationTasks = waitForPublication ? [] : null;
        var refreshStarted = false;

        lock (_stateLock)
        {
            ThrowIfDisposed();
            foreach (var source in sources)
            {
                if (!_sourcesById.TryGetValue(source.Id, out var activeSource)
                    || (!waitForPublication && !ReferenceEquals(activeSource, source)))
                {
                    continue;
                }

                if (!background)
                {
                    _debouncedRefreshes.Remove(source.Id);
                }

                var request = GetOrAddRefreshRequest(_pendingRefreshes, activeSource, background);
                request.RequireFullRefresh();
                if (publicationTasks is not null)
                {
                    if (!_initialSourcePublications.TryGetValue(source.Id, out var publication))
                    {
                        publication = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        _initialSourcePublications.Add(source.Id, publication);
                    }

                    publicationTasks.Add(publication.Task);
                }
            }

            refreshTask = StartRefreshUnderLock(out refreshStarted);
        }

        if (refreshStarted)
        {
            RaiseRefreshStateChanged();
        }

        // Initial publication follows a source ID through replacements, even while the refresh loop is idle.
        // Explicit refreshes also wait for later invalidations to drain.
        return publicationTasks is null
            ? refreshTask
            : Task.WhenAll(publicationTasks).WaitAsync(_disposeToken);
    }

    private async Task RefreshUntilCurrentAsync()
    {
        var completedNormally = false;
        try
        {
            while (true)
            {
                List<SourceRefreshRequest> requests;
                IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots;

                lock (_stateLock)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    var foregroundPending = _pendingRefreshes.Values.Any(request => !request.Background);
                    requests = _pendingRefreshes.Values.Where(request => request.Background != foregroundPending).ToList();
                    foreach (var request in requests)
                    {
                        _pendingRefreshes.Remove(request.Source.Id);
                    }

                    snapshots = _publishedState.SourceSnapshots;
                }

                List<(IAppSource Source, IReadOnlyList<AppCatalogItem> Items, bool FullyReconciled)> refreshedSources = [];

                foreach (var request in requests)
                {
                    using var backgroundCancellation = request.Background ? CancellationTokenSource.CreateLinkedTokenSource(_disposeToken) : null;
                    lock (_stateLock)
                    {
                        if (request.Background && (_pendingRefreshes.Values.Any(pending => !pending.Background)
                            || _debouncedRefreshes.Values.Any(pending => !pending.Background)))
                        {
                            RequeueBackgroundUnderLock(request);
                            continue;
                        }

                        _backgroundCancellation = backgroundCancellation;
                    }

                    var requestToken = backgroundCancellation?.Token ?? _disposeToken;
                    var hadSnapshot = snapshots.TryGetValue(request.Source.Id, out var currentItems);
                    currentItems ??= [];

                    try
                    {
                        IReadOnlyList<AppCatalogItem> refreshedItems;
                        var incremental = hadSnapshot && !request.RequiresFullRefresh && request.PathChanges.Count > 0;
                        if (incremental)
                        {
                            refreshedItems = await request.Source
                                .ApplyChangesAsync(currentItems, request.PathChanges, requestToken, request.Background)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            refreshedItems = await request.Source
                                .LoadAsync(requestToken, request.Background)
                                .ConfigureAwait(false);
                        }

                        requestToken.ThrowIfCancellationRequested();
                        var scan = refreshedItems as AppSourceScanResult;
                        incremental = incremental && scan?.IsFullScan != true;
                        var complete = scan?.IsComplete != false;
                        if (!complete)
                        {
                            // Preserve uncertainty only where reads failed, while applying confirmed source changes.
                            var retained = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
                            foreach (var item in currentItems)
                            {
                                var retainedItem = scan!.GetRetainedItem(item);
                                if (retainedItem is not null)
                                {
                                    retained[retainedItem.Identity] = retainedItem;
                                }
                            }

                            foreach (var item in refreshedItems)
                            {
                                retained[item.Identity] = item;
                            }

                            refreshedItems = new List<AppCatalogItem>(retained.Values);
                        }

                        lock (_stateLock)
                        {
                            if (!request.Background && !incremental)
                            {
                                _pausedBackgroundRefreshes.Remove(request.Source.Id);
                            }

                            var retrySource = (!complete && scan?.FailedPaths is null) || scan?.RetryPaths.Contains(string.Empty) == true;
                            UpdateRetriesUnderLock(request.Source, scan?.RetryPaths ?? [], retrySource, incremental ? request.PathChanges : null);
                        }

                        refreshedSources.Add((request.Source, refreshedItems, !incremental && complete));
                    }
                    catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (OperationCanceledException) when (backgroundCancellation?.IsCancellationRequested == true)
                    {
                        lock (_stateLock)
                        {
                            RequeueBackgroundUnderLock(request);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogSourceRefreshFailed(_logger, request.Source.Id, ex);
                        lock (_stateLock)
                        {
                            ScheduleRetryUnderLock(request.Source, string.Empty);
                            ArmRetryTimerUnderLock();
                        }
                    }
                    finally
                    {
                        lock (_stateLock)
                        {
                            if (ReferenceEquals(_backgroundCancellation, backgroundCancellation))
                            {
                                _backgroundCancellation = null;
                            }
                        }
                    }
                }

                IReadOnlyList<AppCatalogItemChange> changes = [];
                var snapshotsChanged = false;
                List<string> fullyReconciledSourceIds = [];
                List<TaskCompletionSource> initialPublications = [];
                lock (_stateLock)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    var updated = new Dictionary<string, IReadOnlyList<AppCatalogItem>>(
                        _publishedState.SourceSnapshots,
                        StringComparer.Ordinal);
                    foreach (var refreshed in refreshedSources)
                    {
                        if (!_sourcesById.TryGetValue(refreshed.Source.Id, out var activeSource)
                            || !ReferenceEquals(activeSource, refreshed.Source))
                        {
                            continue;
                        }

                        if (refreshed.FullyReconciled)
                        {
                            fullyReconciledSourceIds.Add(refreshed.Source.Id);
                        }

                        if (!updated.TryGetValue(refreshed.Source.Id, out var currentItems)
                            || !AppCatalogItem.HaveSamePersistedContent(currentItems, refreshed.Items))
                        {
                            updated[refreshed.Source.Id] = refreshed.Items;
                            snapshotsChanged = true;
                        }
                    }

                    snapshots = updated;
                    if (snapshotsChanged)
                    {
                        changes = PublishSnapshotsUnderLock(snapshots);
                    }

                    foreach (var request in requests)
                    {
                        if (_sourcesById.TryGetValue(request.Source.Id, out var activeSource)
                            && ReferenceEquals(activeSource, request.Source)
                            && _initialSourcePublications.Remove(request.Source.Id, out var publication))
                        {
                            initialPublications.Add(publication);
                        }
                    }
                }

                if (changes.Count > 0)
                {
                    RaiseChanged(changes);
                }

                foreach (var publication in initialPublications)
                {
                    publication.TrySetResult();
                }

                if (snapshotsChanged || fullyReconciledSourceIds.Count > 0)
                {
                    try
                    {
                        var context = AppCatalogCacheContext.Create(GetSourcesSnapshot(), _timeProvider.GetUtcNow());
                        await _cache.SaveAsync(
                            snapshots,
                            fullyReconciledSourceIds,
                            context,
                            _disposeToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogCacheSaveFailed(_logger, ex);
                    }
                }

                var refreshCompleted = false;
                lock (_stateLock)
                {
                    if (!_disposed && _pendingRefreshes.Count == 0)
                    {
                        _isRefreshing = false;
                        _refreshTask = null;
                        completedNormally = true;
                        refreshCompleted = true;
                    }
                }

                if (refreshCompleted)
                {
                    RaiseRefreshStateChanged();
                    ScheduleInvalidationFlush();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            LogCatalogRefreshFailed(_logger, ex);
        }
        finally
        {
            var notify = false;
            Task? recoveryTask = null;
            lock (_stateLock)
            {
                if (!completedNormally && !_disposed && _refreshTask is not null)
                {
                    // A failed refresh must not leave startup waiting for a publication it cannot produce.
                    foreach (var publication in _initialSourcePublications.Values)
                    {
                        publication.TrySetResult();
                    }

                    _initialSourcePublications.Clear();
                    _refreshTask = null;
                    if (_pendingRefreshes.Count > 0)
                    {
                        recoveryTask = StartRefreshUnderLock(out _);
                    }
                    else
                    {
                        _isRefreshing = false;
                        notify = true;
                    }
                }
            }

            if (notify)
            {
                try
                {
                    RaiseRefreshStateChanged();
                }
                catch (Exception ex)
                {
                    LogRefreshStateNotificationFailed(_logger, ex);
                }
            }

            if (recoveryTask is not null)
            {
                try
                {
                    await recoveryTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    LogRefreshRecoveryFailed(_logger, ex);
                }
            }
        }
    }

    private Task StartRefreshUnderLock(out bool refreshStarted)
    {
        refreshStarted = false;
        if (_refreshTask is null)
        {
            _isRefreshing = true;
            _refreshTask = Task.Run(RefreshUntilCurrentAsync);
            refreshStarted = true;
        }

        return _refreshTask;
    }

    private void RequeueBackgroundUnderLock(SourceRefreshRequest request)
    {
        if (_disposed || !_sourcesById.TryGetValue(request.Source.Id, out var active) || !ReferenceEquals(active, request.Source))
        {
            return;
        }

        // Keep recovery separate so a targeted foreground request does not become a full source scan.
        GetOrAddRefreshRequest(_pausedBackgroundRefreshes, request.Source, background: true).Merge(request);
    }

    private void PublishCachedSnapshots(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots,
        IReadOnlyList<IAppSource> initializedSources)
    {
        IReadOnlyList<AppCatalogItemChange> changes;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            var combined = new Dictionary<string, IReadOnlyList<AppCatalogItem>>(_publishedState.SourceSnapshots, StringComparer.Ordinal);
            foreach (var source in initializedSources)
            {
                if (_sourcesById.TryGetValue(source.Id, out var activeSource)
                    && ReferenceEquals(source, activeSource)
                    && snapshots.TryGetValue(source.Id, out var cachedItems))
                {
                    // A refresh may have completed while the cache was being read.
                    combined.TryAdd(source.Id, cachedItems);
                }
            }

            changes = PublishSnapshotsUnderLock(combined);
        }

        if (changes.Count > 0)
        {
            RaiseChanged(changes);
        }
    }

    public Task SetAppHiddenAsync(string catalogId, bool hidden)
    {
        if (string.IsNullOrWhiteSpace(catalogId))
        {
            return Task.CompletedTask;
        }

        return Task.Run(() => SetAppHiddenCore(catalogId, hidden));
    }

    private void SetAppHiddenCore(string catalogId, bool hidden)
    {
        lock (_stateLock)
        {
            ThrowIfDisposed();

            var publishedState = _publishedState;
            var sourceIndex = FindAppIndex(
                hidden ? publishedState.Snapshot.Items : publishedState.Snapshot.HiddenItems,
                catalogId);
            if (!publishedState.CatalogItems.TryGetValue(catalogId, out var catalogItem)
                || sourceIndex < 0
                || !_visibilityStore.SetHidden(catalogItem, hidden))
            {
                return;
            }

            MovePublishedAppUnderLock(publishedState, sourceIndex, hidden);
        }

        RaiseVisibilityChanged(catalogId, hidden);

        // TODO: Persist visibility changes fire-and-forget so disk I/O never extends the user-visible operation.
        // Persistence failures must still be observed and logged.
        _visibilityStore.Persist();
    }

    private void MovePublishedAppUnderLock(PublishedState publishedState, int sourceIndex, bool hidden)
    {
        var source = hidden ? publishedState.Snapshot.Items : publishedState.Snapshot.HiddenItems;
        var destination = hidden ? publishedState.Snapshot.HiddenItems : publishedState.Snapshot.Items;
        var app = source[sourceIndex];

        var newSource = source.Where((_, index) => index != sourceIndex).ToArray();
        AppItem[] newDestination = [.. destination, app];
        var visibilityById = new Dictionary<string, AppVisibility>(
            publishedState.VisibilityById,
            StringComparer.OrdinalIgnoreCase)
        {
            [app.CatalogId] = hidden ? AppVisibility.Hidden : AppVisibility.Visible,
        };

        var snapshot = hidden
            ? new AppCatalogSnapshot(newSource, newDestination, publishedState.Snapshot.PatternHiddenItems)
            : new AppCatalogSnapshot(newDestination, newSource, publishedState.Snapshot.PatternHiddenItems);
        Volatile.Write(
            ref _publishedState,
            publishedState with
            {
                Snapshot = snapshot,
                VisibilityById = visibilityById,
            });
    }

    private static int FindAppIndex(IReadOnlyList<AppItem> items, string catalogId)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i].CatalogId, catalogId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private IReadOnlyList<AppCatalogItemChange> PublishSnapshotsUnderLock(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots)
    {
        var publication = BuildPublication(snapshots, _publishedState);
        Volatile.Write(ref _publishedState, publication.State);
        return publication.Changes;
    }

    private Publication BuildPublication(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots,
        PublishedState previousState)
    {
        var merged = MergeSnapshots(snapshots);
        var catalogItems = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
        var publishedApps = new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase);
        var visibilityById = new Dictionary<string, AppVisibility>(StringComparer.OrdinalIgnoreCase);
        List<AppItem> visibleItems = [];
        List<AppItem> hiddenItems = [];
        List<AppItem> patternHiddenItems = [];
        List<AppCatalogItemChange> changes = [];

        foreach (var item in merged.Values)
        {
            if (!IsIncluded(item))
            {
                continue;
            }

            var existed = previousState.CatalogItems.TryGetValue(item.Identity, out var previousCatalogItem);
            AppItem? app = null;
            var unchanged = existed
                && previousCatalogItem!.CanReuseMaterializedApp(item)
                && previousState.PublishedApps.TryGetValue(item.Identity, out app);

            if (!unchanged)
            {
                try
                {
                    app = item.ToAppItem();
                }
                catch (Exception ex)
                {
                    LogCachedApplicationMaterializationFailed(_logger, item.Identity, ex);
                    continue;
                }
            }

            var visibility = _visibilityStore.GetVisibility(item);
            var hidden = visibility != AppVisibility.Visible;
            catalogItems[item.Identity] = item;
            publishedApps[item.Identity] = app!;
            visibilityById[item.Identity] = visibility;
            if (visibility == AppVisibility.HiddenByPattern)
            {
                patternHiddenItems.Add(app!);
            }
            else if (visibility == AppVisibility.Hidden)
            {
                hiddenItems.Add(app!);
            }
            else
            {
                visibleItems.Add(app!);
            }

            previousState.VisibilityById.TryGetValue(item.Identity, out var previousVisibility);
            if (!existed)
            {
                changes.Add(new AppCatalogItemChange(AppCatalogChangeKind.Added, item.Identity, app, hidden));
            }
            else if (!unchanged || visibility != previousVisibility)
            {
                changes.Add(new AppCatalogItemChange(AppCatalogChangeKind.Updated, item.Identity, app, hidden));
            }
        }

        foreach (var previous in previousState.CatalogItems)
        {
            if (!catalogItems.ContainsKey(previous.Key))
            {
                changes.Add(new AppCatalogItemChange(AppCatalogChangeKind.Removed, previous.Key, null, hidden: false));
            }
        }

        return new Publication(
            new PublishedState(
                snapshots,
                new AppCatalogSnapshot(visibleItems.AsReadOnly(), hiddenItems.AsReadOnly(), patternHiddenItems.AsReadOnly()),
                catalogItems,
                publishedApps,
                visibilityById),
            changes.AsReadOnly());
    }

    private bool IsIncluded(AppCatalogItem item)
    {
        foreach (var filter in _filters)
        {
            if (!filter.Includes(item))
            {
                return false;
            }
        }

        return true;
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        _ = Task.Run(ReapplyFilters);
    }

    private void ReapplyFilters()
    {
        try
        {
            IReadOnlyList<AppCatalogItemChange> changes;
            lock (_stateLock)
            {
                if (_disposed)
                {
                    return;
                }

                changes = PublishSnapshotsUnderLock(_publishedState.SourceSnapshots);
            }

            if (changes.Count > 0)
            {
                RaiseChanged(changes);
            }
        }
        catch (Exception ex)
        {
            LogCatalogReprojectionFailed(_logger, ex);
        }
    }

    private Dictionary<string, AppCatalogItem> MergeSnapshots(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots)
    {
        var canonicalIdentityByTarget = BuildCanonicalIdentityByTarget(snapshots);
        var merged = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots.Values)
        {
            foreach (var item in snapshot)
            {
                if (string.IsNullOrWhiteSpace(item.Identity))
                {
                    continue;
                }

                var canonicalItem = CanonicalizeItem(item, canonicalIdentityByTarget);
                if (!merged.TryGetValue(canonicalItem.Identity, out var existing))
                {
                    merged.Add(canonicalItem.Identity, canonicalItem);
                    continue;
                }

                try
                {
                    merged[canonicalItem.Identity] = existing.MergeProvenance(canonicalItem);
                }
                catch (InvalidOperationException ex)
                {
                    LogCatalogMergeConflict(_logger, canonicalItem.Identity, ex);
                }
            }
        }

        return merged;
    }

    private static Dictionary<string, string?> BuildCanonicalIdentityByTarget(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots)
    {
        var canonicalIdentityByTarget = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots.Values)
        {
            foreach (var item in snapshot)
            {
                var canonicalIdentity = item.Payload.GetCanonicalIdentityHint();
                var targetPath = item.Payload.GetCanonicalTargetPath();
                if (string.IsNullOrWhiteSpace(canonicalIdentity)
                    || string.IsNullOrWhiteSpace(targetPath))
                {
                    continue;
                }

                var target = PathHelpers.NormalizePath(targetPath);
                if (!canonicalIdentityByTarget.TryAdd(target, canonicalIdentity)
                    && !string.Equals(
                        canonicalIdentityByTarget[target],
                        canonicalIdentity,
                        StringComparison.OrdinalIgnoreCase))
                {
                    canonicalIdentityByTarget[target] = null;
                }
            }
        }

        return canonicalIdentityByTarget;
    }

    private static AppCatalogItem CanonicalizeItem(
        AppCatalogItem item,
        IReadOnlyDictionary<string, string?> canonicalIdentityByTarget)
    {
        var canonicalIdentity = item.Payload.GetCanonicalIdentityHint();
        if (!string.IsNullOrWhiteSpace(canonicalIdentity))
        {
            return item.WithIdentity(canonicalIdentity);
        }

        var targetPath = item.Payload.GetCanonicalTargetPath();
        if (string.IsNullOrWhiteSpace(targetPath)
            || !canonicalIdentityByTarget.TryGetValue(
                PathHelpers.NormalizePath(targetPath),
                out canonicalIdentity)
            || canonicalIdentity is null)
        {
            return item;
        }

        return item.WithIdentity(canonicalIdentity);
    }

    private void OnSourceProviderChanged(object? sender, EventArgs e)
    {
        IReadOnlyList<IAppSource> sources;
        long generation;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                sources = _sourceProvider.GetSources();
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            generation = ++_sourceProviderGeneration;
        }

        _ = Task.Run(() => ReconcileSourcesAsync(sources, generation));
    }

    private async Task ReconcileSourcesAsync(IReadOnlyList<IAppSource> sources, long generation)
    {
        List<IAppSource> addedSources = [];
        List<IAppSource> removedSources = [];
        List<TaskCompletionSource> removedPublications = [];
        IReadOnlyList<AppCatalogItemChange> changes = [];
        var sourcesById = new Dictionary<string, IAppSource>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (!sourcesById.TryAdd(source.Id, source))
            {
                LogDuplicateSourceId(_logger, source.Id);
                return;
            }
        }

        lock (_stateLock)
        {
            if (_disposed || generation != _sourceProviderGeneration)
            {
                return;
            }

            foreach (var existing in _sourcesById)
            {
                if (!sourcesById.TryGetValue(existing.Key, out var replacement)
                    || !ReferenceEquals(existing.Value, replacement))
                {
                    existing.Value.Invalidated -= OnSourceInvalidated;
                    removedSources.Add(existing.Value);
                    _pendingRefreshes.Remove(existing.Key);
                    _debouncedRefreshes.Remove(existing.Key);
                    _pausedBackgroundRefreshes.Remove(existing.Key);
                    _sourceRetries.Remove(existing.Key);
                    if (!sourcesById.ContainsKey(existing.Key)
                        && _initialSourcePublications.Remove(existing.Key, out var publication))
                    {
                        removedPublications.Add(publication);
                    }
                }
            }

            foreach (var source in sources)
            {
                if (!_sourcesById.TryGetValue(source.Id, out var existing)
                    || !ReferenceEquals(existing, source))
                {
                    source.Invalidated += OnSourceInvalidated;
                    addedSources.Add(source);
                }
            }

            _sources = sources;
            _sourcesById.Clear();
            foreach (var source in sources)
            {
                _sourcesById.Add(source.Id, source);
            }

            var snapshots = new Dictionary<string, IReadOnlyList<AppCatalogItem>>(
                _publishedState.SourceSnapshots,
                StringComparer.Ordinal);
            var snapshotsChanged = false;
            foreach (var removedSource in removedSources)
            {
                if (!sourcesById.ContainsKey(removedSource.Id) && snapshots.Remove(removedSource.Id))
                {
                    snapshotsChanged = true;
                }
            }

            if (snapshotsChanged)
            {
                changes = PublishSnapshotsUnderLock(snapshots);
            }
        }

        foreach (var source in removedSources)
        {
            source.Dispose();
        }

        if (changes.Count > 0)
        {
            RaiseChanged(changes);
        }

        foreach (var publication in removedPublications)
        {
            publication.TrySetResult();
        }

        await InitializeSourcesAsync(addedSources).ConfigureAwait(false);

        if (addedSources.Count > 0)
        {
            try
            {
                await QueueFullRefresh(addedSources).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private void OnSourceInvalidated(object? sender, AppSourceInvalidatedEventArgs e)
    {
        if (sender is not IAppSource source)
        {
            return;
        }

        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            if (!_sourcesById.TryGetValue(source.Id, out var currentSource)
                || !ReferenceEquals(currentSource, source))
            {
                return;
            }

            GetOrAddRefreshRequest(_debouncedRefreshes, source).Add(e);
        }

        if (_invalidationDelay == TimeSpan.Zero)
        {
            QueueDebouncedRefreshes();
        }
        else
        {
            ScheduleInvalidationFlush();
        }
    }

    private void UpdateRetriesUnderLock(
        IAppSource source,
        IReadOnlyList<string> retryPaths,
        bool incomplete,
        IReadOnlyList<AppSourcePathChange>? checkedPaths)
    {
        if (_disposed || !_sourcesById.TryGetValue(source.Id, out var active) || !ReferenceEquals(active, source))
        {
            return;
        }

        var rejected = new HashSet<string>(retryPaths, StringComparer.OrdinalIgnoreCase);
        if (_sourceRetries.TryGetValue(source.Id, out var state))
        {
            foreach (var path in state.Paths.Keys.ToArray())
            {
                var checkedPath = checkedPaths is null || checkedPaths.Any(change => string.Equals(change.Path, path, StringComparison.OrdinalIgnoreCase));
                if ((path.Length == 0 && !incomplete && checkedPaths is null) || (path.Length > 0 && checkedPath && !rejected.Contains(path)))
                {
                    state.Paths.Remove(path);
                }
            }
        }

        if (incomplete)
        {
            ScheduleRetryUnderLock(source, string.Empty);
        }

        foreach (var path in retryPaths)
        {
            ScheduleRetryUnderLock(source, path);
        }

        ArmRetryTimerUnderLock();
    }

    private void ScheduleRetryUnderLock(IAppSource source, string path)
    {
        if (_disposed || !_sourcesById.TryGetValue(source.Id, out var active) || !ReferenceEquals(active, source))
        {
            return;
        }

        if (!_sourceRetries.TryGetValue(source.Id, out var state))
        {
            state = new SourceRetryState(source);
            _sourceRetries.Add(source.Id, state);
        }

        state.Paths.TryGetValue(path, out var retry);
        if (retry?.Attempt >= RetryDelays.Length || retry?.DueAtUtc < DateTimeOffset.MaxValue)
        {
            return;
        }

        // Exhausted candidates remain here until a successful scan or the next reconciliation.
        // Repeated notifications must not continually renew their retry budget.
        if (path.Length > 0 && retry is null && state.Paths.Count >= MaxIncrementalPathChangesPerSource)
        {
            return;
        }

        var attempt = retry?.Attempt ?? 0;
        state.Paths[path] = new PendingRetry(attempt, _timeProvider.GetUtcNow() + RetryDelays[attempt]);
    }

    private void ArmRetryTimerUnderLock()
    {
        if (_disposed)
        {
            return;
        }

        var dueAt = DateTimeOffset.MaxValue;
        foreach (var state in _sourceRetries.Values)
        {
            foreach (var retry in state.Paths.Values)
            {
                if (retry.DueAtUtc < dueAt)
                {
                    dueAt = retry.DueAtUtc;
                }
            }
        }

        var delay = dueAt == DateTimeOffset.MaxValue ? Timeout.InfiniteTimeSpan : dueAt - _timeProvider.GetUtcNow();
        _retryTimer.Change(delay < TimeSpan.Zero && delay != Timeout.InfiniteTimeSpan ? TimeSpan.Zero : delay, Timeout.InfiniteTimeSpan);
    }

    private void QueueRetries()
    {
        try
        {
            var refreshStarted = false;
            lock (_stateLock)
            {
                if (_disposed)
                {
                    return;
                }

                var now = _timeProvider.GetUtcNow();
                foreach (var state in _sourceRetries.Values)
                {
                    foreach (var path in state.Paths.Keys.ToArray())
                    {
                        var retry = state.Paths[path];
                        if (retry.DueAtUtc > now)
                        {
                            continue;
                        }

                        state.Paths[path] = new PendingRetry(retry.Attempt + 1, DateTimeOffset.MaxValue);
                        var request = GetOrAddRefreshRequest(_pendingRefreshes, state.Source, background: true);
                        if (path.Length == 0)
                        {
                            request.RequireFullRefresh();
                        }
                        else
                        {
                            request.Add(AppSourceInvalidatedEventArgs.ForPath(new AppSourcePathChange(WatcherChangeTypes.Changed, path)), background: true);
                        }
                    }
                }

                ArmRetryTimerUnderLock();
                if (_pendingRefreshes.Count > 0)
                {
                    _ = StartRefreshUnderLock(out refreshStarted);
                }
            }

            if (refreshStarted)
            {
                RaiseRefreshStateChanged();
            }
        }
        catch (Exception ex)
        {
            LogRefreshRecoveryFailed(_logger, ex);
        }
    }

    private void ReconcileInBackground()
    {
        try
        {
            IReadOnlyList<IAppSource> sources;
            lock (_stateLock)
            {
                if (_disposed || _initializationTask is null)
                {
                    return;
                }

                _sourceRetries.Clear();
                _retryTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                sources = _sources;
            }

            _ = QueueFullRefresh(sources, background: true);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            LogRefreshRecoveryFailed(_logger, ex);
        }
    }

    private void ScheduleInvalidationFlush()
    {
        lock (_stateLock)
        {
            if (_disposed || _invalidationFlushScheduled || (_debouncedRefreshes.Count == 0 && _pausedBackgroundRefreshes.Count == 0))
            {
                return;
            }

            _invalidationFlushScheduled = true;
        }

        _ = FlushInvalidationsAfterDelayAsync();
    }

    private async Task FlushInvalidationsAfterDelayAsync()
    {
        try
        {
            await Task.Delay(_invalidationDelay, _timeProvider, _disposeToken).ConfigureAwait(false);
            QueueDebouncedRefreshes(endScheduledWindow: true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void QueueDebouncedRefreshes(bool endScheduledWindow = false)
    {
        var refreshStarted = false;
        lock (_stateLock)
        {
            if (endScheduledWindow)
            {
                _invalidationFlushScheduled = false;
            }

            if (_disposed || (_debouncedRefreshes.Count == 0 && _pausedBackgroundRefreshes.Count == 0))
            {
                return;
            }

            foreach (var pending in _debouncedRefreshes.Values)
            {
                GetOrAddRefreshRequest(_pendingRefreshes, pending.Source, pending.Background).Merge(pending);
            }

            _debouncedRefreshes.Clear();
            if (!_pendingRefreshes.Values.Any(pending => !pending.Background))
            {
                foreach (var paused in _pausedBackgroundRefreshes.Values)
                {
                    GetOrAddRefreshRequest(_pendingRefreshes, paused.Source, background: true).Merge(paused);
                }

                _pausedBackgroundRefreshes.Clear();
            }

            _ = StartRefreshUnderLock(out refreshStarted);
        }

        if (refreshStarted)
        {
            RaiseRefreshStateChanged();
        }
    }

    private SourceRefreshRequest GetOrAddRefreshRequest(
        Dictionary<string, SourceRefreshRequest> requests,
        IAppSource source,
        bool background = false)
    {
        if (ReferenceEquals(requests, _pendingRefreshes))
        {
            if (background && ((requests.TryGetValue(source.Id, out var foreground) && !foreground.Background)
                || (_debouncedRefreshes.TryGetValue(source.Id, out var debounced) && !debounced.Background)))
            {
                return GetOrAddRefreshRequest(_pausedBackgroundRefreshes, source, background: true);
            }

            if (!background && requests.TryGetValue(source.Id, out var pendingBackground) && pendingBackground.Background)
            {
                RequeueBackgroundUnderLock(pendingBackground);
                requests.Remove(source.Id);
            }
        }

        if (!background && _backgroundCancellation is not null)
        {
            // Cancellation callbacks run outside the catalog lock; the worker yields at its next checkpoint.
            _ = _backgroundCancellation.CancelAsync();
        }

        if (!requests.TryGetValue(source.Id, out var request)
            || !ReferenceEquals(request.Source, source))
        {
            request = new SourceRefreshRequest(source, background);
            requests[source.Id] = request;
        }
        else
        {
            request.IncludePriority(background);
        }

        return request;
    }

    private void RaiseChanged(IReadOnlyList<AppCatalogItemChange> changes)
    {
        if (!_disposed)
        {
            RaiseNotification(Changed, new AppCatalogChangedEventArgs(changes));
        }
    }

    private void RaiseRefreshStateChanged()
    {
        if (!_disposed)
        {
            RefreshStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RaiseVisibilityChanged(string catalogId, bool hidden)
    {
        if (!_disposed)
        {
            RaiseNotification(VisibilityChanged, new AppVisibilityChangedEventArgs(catalogId, hidden));
        }
    }

    private void RaiseNotification<TEventArgs>(EventHandler<TEventArgs>? handlers, TEventArgs args)
        where TEventArgs : EventArgs
    {
        foreach (var handler in Delegate.EnumerateInvocationList(handlers))
        {
            try
            {
                handler(this, args);
            }
            catch (Exception ex)
            {
                LogCatalogChangeNotificationFailed(_logger, ex);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        IReadOnlyList<IAppSource> sources;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Changed = null;
            RefreshStateChanged = null;
            VisibilityChanged = null;
            _pendingRefreshes.Clear();
            _debouncedRefreshes.Clear();
            _pausedBackgroundRefreshes.Clear();
            _initialSourcePublications.Clear();
            _sourceRetries.Clear();
            sources = _sources;
        }

        _retryTimer.Dispose();
        _reconciliationTimer.Dispose();
        _disposeCancellation.Cancel();
        _sourceProvider.Changed -= OnSourceProviderChanged;
        _sourceProvider.Dispose();
        _visibilityStore.Changed -= OnFilterChanged;
        _visibilityStore.Dispose();

        foreach (var filter in _filters)
        {
            filter.Changed -= OnFilterChanged;
            filter.Dispose();
        }

        foreach (var source in sources)
        {
            source.Invalidated -= OnSourceInvalidated;
            source.Dispose();
        }

        _disposeCancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Failed to initialize application source '{SourceId}'.")]
    private static partial void LogSourceInitializationFailed(MEL.ILogger logger, string sourceId, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to refresh application source '{SourceId}'.")]
    private static partial void LogSourceRefreshFailed(MEL.ILogger logger, string sourceId, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to save the application catalog cache.")]
    private static partial void LogCacheSaveFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Application catalog refresh failed.")]
    private static partial void LogCatalogRefreshFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Application catalog refresh-state notification failed.")]
    private static partial void LogRefreshStateNotificationFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Application catalog refresh recovery failed.")]
    private static partial void LogRefreshRecoveryFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "Failed to materialize cached application '{Identity}'.")]
    private static partial void LogCachedApplicationMaterializationFailed(MEL.ILogger logger, string identity, Exception exception);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Application source provider returned duplicate ID '{SourceId}'.")]
    private static partial void LogDuplicateSourceId(MEL.ILogger logger, string sourceId);

    [LoggerMessage(EventId = 9, Level = LogLevel.Error, Message = "Application catalog policy reprojection failed.")]
    private static partial void LogCatalogReprojectionFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 10, Level = LogLevel.Warning, Message = "Ignored a conflicting representation of canonical application '{Identity}'.")]
    private static partial void LogCatalogMergeConflict(MEL.ILogger logger, string identity, Exception exception);

    [LoggerMessage(EventId = 11, Level = LogLevel.Error, Message = "Application catalog change notification failed.")]
    private static partial void LogCatalogChangeNotificationFailed(MEL.ILogger logger, Exception exception);

    private sealed class SourceRefreshRequest
    {
        private readonly List<AppSourcePathChange> _pathChanges = [];
        private readonly Dictionary<string, int> _pathChangeIndexes = new(StringComparer.OrdinalIgnoreCase);

        public IAppSource Source { get; }

        public bool RequiresFullRefresh { get; private set; }

        public bool Background { get; private set; }

        public IReadOnlyList<AppSourcePathChange> PathChanges => _pathChanges;

        public SourceRefreshRequest(IAppSource source, bool background)
        {
            Source = source;
            Background = background;
        }

        public void IncludePriority(bool background)
        {
            // Manual and watcher work takes priority over coalesced background recovery.
            Background &= background;
        }

        public void Add(AppSourceInvalidatedEventArgs invalidation, bool background = false)
        {
            IncludePriority(background);
            if (invalidation.RequiresFullRefresh)
            {
                RequireFullRefresh();
            }
            else if (!RequiresFullRefresh && invalidation.PathChange is not null)
            {
                AddPathChange(invalidation.PathChange);
            }
        }

        public void Merge(SourceRefreshRequest other)
        {
            IncludePriority(other.Background);
            if (other.RequiresFullRefresh)
            {
                RequireFullRefresh();
                return;
            }

            if (RequiresFullRefresh)
            {
                return;
            }

            foreach (var pathChange in other.PathChanges)
            {
                AddPathChange(pathChange);
            }
        }

        public void RequireFullRefresh()
        {
            RequiresFullRefresh = true;
            _pathChanges.Clear();
            _pathChangeIndexes.Clear();
        }

        private void AddPathChange(AppSourcePathChange change)
        {
            if (!string.IsNullOrWhiteSpace(change.OldPath))
            {
                AddOrReplacePathChange(new AppSourcePathChange(WatcherChangeTypes.Deleted, change.OldPath));
            }

            AddOrReplacePathChange(new AppSourcePathChange(change.ObservedKind, change.Path));
        }

        private void AddOrReplacePathChange(AppSourcePathChange change)
        {
            if (RequiresFullRefresh)
            {
                return;
            }

            if (_pathChangeIndexes.TryGetValue(change.Path, out var index))
            {
                _pathChanges[index] = change;
                return;
            }

            if (_pathChanges.Count >= MaxIncrementalPathChangesPerSource)
            {
                RequireFullRefresh();
                return;
            }

            _pathChangeIndexes.Add(change.Path, _pathChanges.Count);
            _pathChanges.Add(change);
        }
    }

    private sealed record PendingRetry(int Attempt, DateTimeOffset DueAtUtc);

    private sealed class SourceRetryState
    {
        public IAppSource Source { get; }

        public Dictionary<string, PendingRetry> Paths { get; } = new(StringComparer.OrdinalIgnoreCase);

        public SourceRetryState(IAppSource source)
        {
            Source = source;
        }
    }

    private sealed record PublishedState(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> SourceSnapshots,
        AppCatalogSnapshot Snapshot,
        IReadOnlyDictionary<string, AppCatalogItem> CatalogItems,
        IReadOnlyDictionary<string, AppItem> PublishedApps,
        IReadOnlyDictionary<string, AppVisibility> VisibilityById)
    {
        public static PublishedState Empty { get; } = new(
            new Dictionary<string, IReadOnlyList<AppCatalogItem>>(StringComparer.Ordinal),
            new AppCatalogSnapshot([], []),
            new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, AppVisibility>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed record Publication(
        PublishedState State,
        IReadOnlyList<AppCatalogItemChange> Changes);
}
