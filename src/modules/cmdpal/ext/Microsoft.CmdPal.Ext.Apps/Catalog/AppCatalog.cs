// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

public sealed partial class AppCatalog : IAppCatalog
{
    private const int MaxIncrementalPathChangesPerSource = 256;

    private static readonly TimeSpan DefaultInvalidationDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CachedSourceReconciliationDelay = TimeSpan.FromSeconds(30);

    private readonly Lock _stateLock = new();
    private readonly IAppSourceProvider _sourceProvider;
    private readonly Dictionary<string, IAppSource> _sourcesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceRefreshRequest> _pendingRefreshes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceRefreshRequest> _debouncedRefreshes = new(StringComparer.Ordinal);
    private readonly IAppCatalogCache _cache;
    private readonly IAppVisibilityStore _visibilityStore;
    private readonly IReadOnlyList<IAppCatalogFilter> _filters;
    private readonly MEL.ILogger<AppCatalog> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _invalidationDelay;
    private readonly CancellationTokenSource _disposeCancellation = new();

    private IReadOnlyList<IAppSource> _sources;

    private PublishedState _publishedState = PublishedState.Empty;
    private Task? _initializationTask;
    private Task? _refreshTask;
    private long _sourceProviderGeneration;
    private bool _isRefreshing;
    private bool _disposed;
    private bool _invalidationFlushScheduled;

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
    }

    public event EventHandler<AppCatalogChangedEventArgs>? Changed;

    public event EventHandler? RefreshStateChanged;

    public event EventHandler<AppVisibilityChangedEventArgs>? VisibilityChanged;

    public IReadOnlyList<AppItem> Items => Volatile.Read(ref _publishedState).Snapshot.Items;

    public IReadOnlyList<AppItem> HiddenItems => Volatile.Read(ref _publishedState).Snapshot.HiddenItems;

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

    public AppCatalogSnapshot GetSnapshot() => Volatile.Read(ref _publishedState).Snapshot;

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
        foreach (var source in sources)
        {
            try
            {
                await source.InitializeAsync(_disposeCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogSourceInitializationFailed(_logger, source.Id, ex);
            }
        }

        var context = AppCatalogCacheContext.Create(sources, _timeProvider.GetUtcNow());
        var cache = await _cache.LoadAsync(context, _disposeCancellation.Token).ConfigureAwait(false);
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

            if (missingSources.Count > 0)
            {
                _ = QueueFullRefresh(missingSources);
            }

            if (cachedSourcesToReconcile.Count > 0)
            {
                _ = ReconcileCachedSourcesAfterDelayAsync(cachedSourcesToReconcile);
            }

            return;
        }

        await RefreshAsync().ConfigureAwait(false);
    }

    private async Task ReconcileCachedSourcesAfterDelayAsync(IReadOnlyList<IAppSource> sources)
    {
        try
        {
            await Task.Delay(CachedSourceReconciliationDelay, _disposeCancellation.Token).ConfigureAwait(false);
            await QueueFullRefresh(sources).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private IReadOnlyList<IAppSource> GetSourcesSnapshot()
    {
        lock (_stateLock)
        {
            return _sources;
        }
    }

    private Task QueueFullRefresh(IReadOnlyList<IAppSource> sources)
    {
        Task refreshTask;
        var refreshStarted = false;

        lock (_stateLock)
        {
            ThrowIfDisposed();
            foreach (var source in sources)
            {
                if (!_sourcesById.TryGetValue(source.Id, out var activeSource)
                    || !ReferenceEquals(activeSource, source))
                {
                    continue;
                }

                _debouncedRefreshes.Remove(source.Id);
                GetOrAddRefreshRequest(_pendingRefreshes, source).RequireFullRefresh();
            }

            refreshTask = StartRefreshUnderLock(out refreshStarted);
        }

        if (refreshStarted)
        {
            RaiseRefreshStateChanged();
        }

        return refreshTask;
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

                    requests = new List<SourceRefreshRequest>(_pendingRefreshes.Values);
                    _pendingRefreshes.Clear();
                    snapshots = _publishedState.SourceSnapshots;
                }

                List<(IAppSource Source, IReadOnlyList<AppCatalogItem> Items, bool FullyReconciled)> refreshedSources = [];

                foreach (var request in requests)
                {
                    var hadSnapshot = snapshots.TryGetValue(request.Source.Id, out var currentItems);
                    currentItems ??= [];

                    try
                    {
                        IReadOnlyList<AppCatalogItem> refreshedItems;
                        var incremental = hadSnapshot && !request.RequiresFullRefresh && request.PathChanges.Count > 0;
                        if (incremental)
                        {
                            refreshedItems = await request.Source
                                .ApplyChangesAsync(currentItems, request.PathChanges, _disposeCancellation.Token)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            refreshedItems = await request.Source
                                .LoadAsync(_disposeCancellation.Token)
                                .ConfigureAwait(false);
                        }

                        refreshedSources.Add((request.Source, refreshedItems, !incremental));
                    }
                    catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogSourceRefreshFailed(_logger, request.Source.Id, ex);
                    }
                }

                IReadOnlyList<AppCatalogItemChange> changes = [];
                var snapshotsChanged = false;
                List<string> fullyReconciledSourceIds = [];
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
                            || !HasSameSourceSnapshot(currentItems, refreshed.Items))
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
                }

                if (changes.Count > 0)
                {
                    RaiseChanged(changes);
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
                            _disposeCancellation.Token).ConfigureAwait(false);
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

        try
        {
            RaiseVisibilityChanged(catalogId, hidden);
        }
        finally
        {
            // TODO: Persist visibility changes fire-and-forget so disk I/O never extends the user-visible operation.
            // Persistence failures must still be observed and logged.
            _visibilityStore.Persist();
        }
    }

    private void MovePublishedAppUnderLock(PublishedState publishedState, int sourceIndex, bool hidden)
    {
        var source = hidden ? publishedState.Snapshot.Items : publishedState.Snapshot.HiddenItems;
        var destination = hidden ? publishedState.Snapshot.HiddenItems : publishedState.Snapshot.Items;
        var app = source[sourceIndex];

        var newSource = source.Where((_, index) => index != sourceIndex).ToArray();
        AppItem[] newDestination = [.. destination, app];
        var hiddenCatalogIds = new HashSet<string>(
            publishedState.HiddenCatalogIds,
            StringComparer.OrdinalIgnoreCase);
        _ = hidden
            ? hiddenCatalogIds.Add(app.CatalogId)
            : hiddenCatalogIds.Remove(app.CatalogId);

        var snapshot = hidden
            ? new AppCatalogSnapshot(newSource, newDestination)
            : new AppCatalogSnapshot(newDestination, newSource);
        Volatile.Write(
            ref _publishedState,
            publishedState with
            {
                Snapshot = snapshot,
                HiddenCatalogIds = hiddenCatalogIds,
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
        var hiddenCatalogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<AppItem> visibleItems = [];
        List<AppItem> hiddenItems = [];
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
                    app.CatalogId = item.Identity;
                }
                catch (Exception ex)
                {
                    LogCachedApplicationMaterializationFailed(_logger, item.Identity, ex);
                    continue;
                }
            }

            var hidden = _visibilityStore.IsHidden(item);
            catalogItems[item.Identity] = item;
            publishedApps[item.Identity] = app!;
            if (hidden)
            {
                hiddenItems.Add(app!);
                hiddenCatalogIds.Add(item.Identity);
            }
            else
            {
                visibleItems.Add(app!);
            }

            var wasHidden = previousState.HiddenCatalogIds.Contains(item.Identity);
            if (!existed)
            {
                changes.Add(new AppCatalogItemChange(AppCatalogChangeKind.Added, item.Identity, app, hidden));
            }
            else if (!unchanged || hidden != wasHidden)
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
                new AppCatalogSnapshot(visibleItems.AsReadOnly(), hiddenItems.AsReadOnly()),
                catalogItems,
                publishedApps,
                hiddenCatalogIds),
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

    private static Dictionary<string, AppCatalogItem> MergeSnapshots(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots)
    {
        var merged = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in snapshots.Values)
        {
            foreach (var item in snapshot)
            {
                if (string.IsNullOrWhiteSpace(item.Identity))
                {
                    continue;
                }

                merged[item.Identity] = merged.TryGetValue(item.Identity, out var existing)
                    ? existing.MergeProvenance(item)
                    : item;
            }
        }

        return merged;
    }

    private static bool HasSameSourceSnapshot(
        IReadOnlyList<AppCatalogItem> currentItems,
        IReadOnlyList<AppCatalogItem> refreshedItems)
    {
        if (currentItems.Count != refreshedItems.Count)
        {
            return false;
        }

        var currentById = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in currentItems)
        {
            if (!currentById.TryAdd(item.Identity, item))
            {
                return false;
            }
        }

        foreach (var item in refreshedItems)
        {
            if (!currentById.TryGetValue(item.Identity, out var current)
                || !current.HasSamePersistedContent(item))
            {
                return false;
            }
        }

        return true;
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

        foreach (var source in addedSources)
        {
            try
            {
                await source.InitializeAsync(_disposeCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogSourceInitializationFailed(_logger, source.Id, ex);
            }
        }

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

    private void ScheduleInvalidationFlush()
    {
        lock (_stateLock)
        {
            if (_disposed || _invalidationFlushScheduled)
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
            await Task.Delay(_invalidationDelay, _disposeCancellation.Token).ConfigureAwait(false);
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

            if (_disposed || _debouncedRefreshes.Count == 0)
            {
                return;
            }

            foreach (var pending in _debouncedRefreshes.Values)
            {
                GetOrAddRefreshRequest(_pendingRefreshes, pending.Source).Merge(pending);
            }

            _debouncedRefreshes.Clear();
            _ = StartRefreshUnderLock(out refreshStarted);
        }

        if (refreshStarted)
        {
            RaiseRefreshStateChanged();
        }
    }

    private static SourceRefreshRequest GetOrAddRefreshRequest(
        Dictionary<string, SourceRefreshRequest> requests,
        IAppSource source)
    {
        if (!requests.TryGetValue(source.Id, out var request)
            || !ReferenceEquals(request.Source, source))
        {
            request = new SourceRefreshRequest(source);
            requests[source.Id] = request;
        }

        return request;
    }

    private void RaiseChanged(IReadOnlyList<AppCatalogItemChange> changes)
    {
        if (!_disposed)
        {
            Changed?.Invoke(this, new AppCatalogChangedEventArgs(changes));
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
            VisibilityChanged?.Invoke(this, new AppVisibilityChangedEventArgs(catalogId, hidden));
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
            sources = _sources;
        }

        _disposeCancellation.Cancel();
        _sourceProvider.Changed -= OnSourceProviderChanged;
        _sourceProvider.Dispose();

        foreach (var filter in _filters)
        {
            filter.Changed -= OnFilterChanged;
        }

        foreach (var source in sources)
        {
            source.Invalidated -= OnSourceInvalidated;
            source.Dispose();
        }

        _disposeCancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class SourceRefreshRequest
    {
        private readonly List<AppSourcePathChange> _pathChanges = [];
        private readonly Dictionary<string, int> _pathChangeIndexes = new(StringComparer.OrdinalIgnoreCase);

        public SourceRefreshRequest(IAppSource source)
        {
            Source = source;
        }

        public IAppSource Source { get; }

        public bool RequiresFullRefresh { get; private set; }

        public IReadOnlyList<AppSourcePathChange> PathChanges => _pathChanges;

        public void Add(AppSourceInvalidatedEventArgs invalidation)
        {
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

    private sealed record PublishedState(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> SourceSnapshots,
        AppCatalogSnapshot Snapshot,
        IReadOnlyDictionary<string, AppCatalogItem> CatalogItems,
        IReadOnlyDictionary<string, AppItem> PublishedApps,
        IReadOnlySet<string> HiddenCatalogIds)
    {
        public static PublishedState Empty { get; } = new(
            new Dictionary<string, IReadOnlyList<AppCatalogItem>>(StringComparer.Ordinal),
            new AppCatalogSnapshot([], []),
            new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, AppItem>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed record Publication(
        PublishedState State,
        IReadOnlyList<AppCatalogItemChange> Changes);
}
