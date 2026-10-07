// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MEL = Microsoft.Extensions.Logging;
using PublishedState = Microsoft.CmdPal.Ext.Apps.Catalog.AppCatalogPublicationBuilder.PublishedState;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

public sealed partial class AppCatalog : IAppCatalog
{
    public event EventHandler<AppCatalogChangedEventArgs>? Changed;

    public event EventHandler? RefreshStateChanged;

    public event EventHandler<AppVisibilityChangedEventArgs>? VisibilityChanged;

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
    private readonly AppCommandAliasStore _commandAliases;
    private readonly AllAppsSettings _settings;
    private readonly IReadOnlyList<IAppCatalogFilter> _filters;
    private readonly MEL.ILogger<AppCatalog> _logger;
    private readonly AppCatalogPublicationBuilder _publicationBuilder;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _invalidationDelay;
    private readonly Func<bool> _diagnosticsEnabled;
    private readonly CancellationTokenSource _disposeCancellation = new();
    private readonly CancellationToken _disposeToken;
    private readonly ITimer _retryTimer;
    private readonly ITimer _reconciliationTimer;

    private CancellationTokenSource? _backgroundCancellation;

    private IReadOnlyList<IAppSource> _sources;

    private PublishedState _publishedState = PublishedState.Empty;
    private AppCatalogVisibility _visibilityRules;
    private Task? _initializationTask;
    private Task? _refreshTask;
    private long _sourceProviderGeneration;
    private long _diagnosticBatchSequence;
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

    /// <summary>Initializes a new instance of the <see cref="AppCatalog"/> class. Creates a catalog over the supplied discovery sources, persistence stores, and visibility rules.</summary>
    /// <param name="sourceProvider">The provider of active discovery sources; owned by this catalog.</param>
    /// <param name="cache">The cache of raw source snapshots.</param>
    /// <param name="visibilityStore">The store of explicitly hidden identities.</param>
    /// <param name="commandAliases">The retained-alias store; owned and flushed by this catalog.</param>
    /// <param name="settings">Preferences that supply global name and path exclusions.</param>
    /// <param name="filters">Optional catalog filters, disposed with this catalog.</param>
    /// <param name="timeProvider">The clock and timer provider, or the system provider when omitted.</param>
    /// <param name="invalidationDelay">The watcher invalidation debounce interval, or the default interval when omitted.</param>
    /// <param name="logger">The catalog diagnostic logger, or a disabled logger when omitted.</param>
    /// <param name="diagnosticsEnabled">Optional predicate controlling detailed timing diagnostics.</param>
    internal AppCatalog(
        IAppSourceProvider sourceProvider,
        IAppCatalogCache cache,
        IAppVisibilityStore visibilityStore,
        AppCommandAliasStore commandAliases,
        AllAppsSettings settings,
        IReadOnlyList<IAppCatalogFilter>? filters = null,
        TimeProvider? timeProvider = null,
        TimeSpan? invalidationDelay = null,
        MEL.ILogger<AppCatalog>? logger = null,
        Func<bool>? diagnosticsEnabled = null)
    {
        ArgumentNullException.ThrowIfNull(sourceProvider);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(visibilityStore);
        ArgumentNullException.ThrowIfNull(commandAliases);
        ArgumentNullException.ThrowIfNull(settings);

        _sourceProvider = sourceProvider;
        _sources = _sourceProvider.GetSources();
        _cache = cache;
        _visibilityStore = visibilityStore;
        _filters = filters ?? [];
        _logger = logger ?? NullLogger<AppCatalog>.Instance;
        _commandAliases = commandAliases;
        _settings = settings;
        _visibilityRules = new(settings.ExcludedAppNames, settings.ExcludedAppPaths);
        _publicationBuilder = new(_filters, _logger);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _invalidationDelay = invalidationDelay ?? DefaultInvalidationDelay;
        _diagnosticsEnabled = diagnosticsEnabled ?? (() => false);
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
        _settings.Settings.SettingsChanged += OnVisibilitySettingsChanged;
    }

    /// <inheritdoc />
    public AppCatalogSnapshot GetSnapshot()
    {
        return Volatile.Read(ref _publishedState).Snapshot;
    }

    /// <inheritdoc />
    public Task InitializeAsync()
    {
        lock (_stateLock)
        {
            ThrowIfDisposed();
            return _initializationTask ??= Task.Run(InitializeCoreAsync);
        }
    }

    /// <inheritdoc />
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
        var diagnostics = ShouldLogDiagnostics();
        var started = diagnostics ? _timeProvider.GetTimestamp() : 0;
        var sources = GetSourcesSnapshot();
        if (diagnostics)
        {
            LogDiagnosticStartup(_logger, "started", 0, sources.Count, 0, 0);
        }

        var context = AppCatalogCacheContext.Create(sources, _timeProvider.GetUtcNow());
        var cacheStarted = diagnostics ? _timeProvider.GetTimestamp() : 0;
        var cache = await _cache.LoadAsync(context, _disposeToken).ConfigureAwait(false);
        if (diagnostics)
        {
            var durationMs = _timeProvider.GetElapsedTime(cacheStarted).TotalMilliseconds;
            LogDiagnosticStartup(_logger, "cache-read", durationMs, sources.Count, cache?.Sources.Count ?? 0, 0);
        }

        if (cache is not null)
        {
            var snapshots = new Dictionary<string, IReadOnlyList<AppCatalogItem>>(StringComparer.Ordinal);
            var cachedSources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in cache.Sources)
            {
                snapshots[source.SourceId] = source.Items;
                cachedSources.Add(source.SourceId);
            }

            var publicationStarted = diagnostics ? _timeProvider.GetTimestamp() : 0;
            PublishCachedSnapshots(snapshots, sources, out var buildMs);
            if (diagnostics)
            {
                var durationMs = _timeProvider.GetElapsedTime(publicationStarted).TotalMilliseconds;
                var visibleCount = GetSnapshot().Items.Count;
                LogDiagnosticStartup(_logger, "cached-publication", durationMs, sources.Count, cachedSources.Count, visibleCount);
                LogDiagnosticCachePublication(_logger, buildMs, visibleCount);
            }

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

            if (diagnostics)
            {
                var durationMs = _timeProvider.GetElapsedTime(started).TotalMilliseconds;
                var visibleCount = GetSnapshot().Items.Count;
                LogDiagnosticStartup(_logger, "ready", durationMs, sources.Count, cachedSources.Count, visibleCount);
            }

            return;
        }

        await InitializeSourcesAsync(sources).ConfigureAwait(false);
        await RefreshInitialSourcesAsync(GetSourcesSnapshot()).ConfigureAwait(false);
        if (diagnostics)
        {
            var durationMs = _timeProvider.GetElapsedTime(started).TotalMilliseconds;
            var visibleCount = GetSnapshot().Items.Count;
            LogDiagnosticStartup(_logger, "ready", durationMs, sources.Count, 0, visibleCount);
        }
    }

    private async Task InitializeSourcesAsync(IReadOnlyList<IAppSource> sources)
    {
        foreach (var source in sources)
        {
            if (!IsActiveSource(source))
            {
                continue;
            }

            var diagnostics = ShouldLogDiagnostics();
            var started = diagnostics ? _timeProvider.GetTimestamp() : 0;
            var outcome = "ready";
            if (diagnostics)
            {
                LogDiagnosticSourceInitialization(_logger, source.Id, "started", 0);
            }

            try
            {
                await source.InitializeAsync(_disposeToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeToken.IsCancellationRequested)
            {
                outcome = "canceled";
                return;
            }
            catch (ObjectDisposedException) when (!IsActiveSource(source))
            {
                outcome = "retired";
            }
            catch (Exception ex)
            {
                outcome = "failed";
                LogSourceInitializationFailed(_logger, source.Id, ex);
            }
            finally
            {
                if (diagnostics)
                {
                    var durationMs = _timeProvider.GetElapsedTime(started).TotalMilliseconds;
                    LogDiagnosticSourceInitialization(_logger, source.Id, outcome, durationMs);
                }
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
        List<Task>? publicationTasks = waitForPublication || !background ? [] : null;
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
                if (waitForPublication)
                {
                    if (!_initialSourcePublications.TryGetValue(source.Id, out var publication))
                    {
                        publication = new(TaskCreationOptions.RunContinuationsAsynchronously);
                        _initialSourcePublications.Add(source.Id, publication);
                    }

                    publicationTasks!.Add(publication.Task);
                }
                else if (publicationTasks is not null)
                {
                    publicationTasks.Add(request.GetPublicationTask());
                }
            }

            refreshTask = StartRefreshUnderLock(out refreshStarted);
        }

        if (refreshStarted)
        {
            RaiseRefreshStateChanged();
        }

        // Initial publication follows a source ID through replacements, even while the refresh loop is idle.
        // Explicit refreshes wait for their own requests, while later recovery continues independently.
        return publicationTasks is null
            ? refreshTask
            : Task.WhenAll(publicationTasks).WaitAsync(_disposeToken);
    }

    private async Task RefreshUntilCurrentAsync()
    {
        var completedNormally = false;
        List<SourceRefreshRequest> currentRequests = [];
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
                    currentRequests = requests;
                    foreach (var request in requests)
                    {
                        _pendingRefreshes.Remove(request.Source.Id);
                    }

                    snapshots = _publishedState.SourceSnapshots;
                }

                var diagnostics = ShouldLogDiagnostics();
                var batchId = diagnostics ? Interlocked.Increment(ref _diagnosticBatchSequence) : 0;
                var batchStarted = diagnostics ? _timeProvider.GetTimestamp() : 0;
                var snapshotsChanged = false;
                List<string> fullyReconciledSourceIds = [];

                foreach (var request in requests)
                {
                    using var backgroundCancellation = request.Background ? CancellationTokenSource.CreateLinkedTokenSource(_disposeToken) : null;
                    lock (_stateLock)
                    {
                        if (!_sourcesById.TryGetValue(request.Source.Id, out var active) || !ReferenceEquals(active, request.Source))
                        {
                            request.CompletePublication();
                            continue;
                        }

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
                    var sourceStarted = diagnostics ? _timeProvider.GetTimestamp() : 0;
                    var requestedIncremental = hadSnapshot && !request.RequiresFullRefresh && request.PathChanges.Count > 0;
                    if (diagnostics)
                    {
                        var queuedMs = request.QueuedAt is { } queuedAt ? _timeProvider.GetElapsedTime(queuedAt, sourceStarted).TotalMilliseconds : (double?)null;
                        LogDiagnosticSourceStarted(_logger, batchId, request.Source.Id, request.Background, requestedIncremental, request.PathChanges.Count, queuedMs);
                    }

                    try
                    {
                        IReadOnlyList<AppCatalogItem> refreshedItems;
                        var incremental = requestedIncremental;
                        if (incremental)
                        {
                            refreshedItems = await request.Source
                                .ApplyChangesAsync(currentItems, request.PathChanges, requestToken, request.Background)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            refreshedItems = await request.Source
                                .LoadAsync(requestToken, request.Background, request.PathChanges)
                                .ConfigureAwait(false);
                        }

                        var sourceLoadedAt = diagnostics ? _timeProvider.GetTimestamp() : 0;
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
                            UpdateRetriesUnderLock(request.Source, scan?.RetryPaths ?? [], scan?.ReusedRejectedPaths ?? [], retrySource, incremental ? request.PathChanges : null);
                        }

                        if (diagnostics)
                        {
                            var durationMs = _timeProvider.GetElapsedTime(sourceStarted, sourceLoadedAt).TotalMilliseconds;
                            LogDiagnosticSourceCompleted(_logger, batchId, request.Source.Id, durationMs, incremental, refreshedItems.Count, complete, scan?.FailedPaths?.Count, scan?.RetryPaths.Count ?? 0);
                        }

                        try
                        {
                            var publication = PublishSourceSnapshot(request.Source, refreshedItems, !incremental && complete, diagnostics, batchId, sourceLoadedAt);
                            snapshotsChanged |= publication.Changed;
                            if (publication.Reconciled)
                            {
                                fullyReconciledSourceIds.Add(request.Source.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            // A publication failure must not trigger another source scan.
                            LogCatalogRefreshFailed(_logger, ex);
                            CompleteInitialPublication(request.Source);
                        }
                    }
                    catch (OperationCanceledException) when (_disposeCancellation.IsCancellationRequested)
                    {
                        if (diagnostics)
                        {
                            var durationMs = _timeProvider.GetElapsedTime(sourceStarted).TotalMilliseconds;
                            LogDiagnosticSourceStopped(_logger, batchId, request.Source.Id, "canceled", durationMs);
                        }

                        return;
                    }
                    catch (OperationCanceledException) when (backgroundCancellation?.IsCancellationRequested == true)
                    {
                        if (diagnostics)
                        {
                            var durationMs = _timeProvider.GetElapsedTime(sourceStarted).TotalMilliseconds;
                            LogDiagnosticSourceStopped(_logger, batchId, request.Source.Id, "preempted", durationMs);
                        }

                        lock (_stateLock)
                        {
                            RequeueBackgroundUnderLock(request);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (diagnostics)
                        {
                            var durationMs = _timeProvider.GetElapsedTime(sourceStarted).TotalMilliseconds;
                            LogDiagnosticSourceStopped(_logger, batchId, request.Source.Id, "failed", durationMs);
                        }

                        LogSourceRefreshFailed(_logger, request.Source.Id, ex);
                        lock (_stateLock)
                        {
                            ScheduleRetryUnderLock(request.Source, string.Empty);
                            ArmRetryTimerUnderLock();
                        }

                        CompleteInitialPublication(request.Source);
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

                lock (_stateLock)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    snapshots = _publishedState.SourceSnapshots;
                }

                var cacheAttempted = snapshotsChanged || fullyReconciledSourceIds.Count > 0;
                var cacheMs = 0.0;
                if (cacheAttempted)
                {
                    var cacheStarted = diagnostics ? _timeProvider.GetTimestamp() : 0;
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
                    finally
                    {
                        if (diagnostics)
                        {
                            cacheMs = _timeProvider.GetElapsedTime(cacheStarted).TotalMilliseconds;
                        }
                    }
                }

                if (diagnostics)
                {
                    var durationMs = _timeProvider.GetElapsedTime(batchStarted).TotalMilliseconds;
                    LogDiagnosticBatchCompleted(_logger, batchId, durationMs, cacheAttempted, cacheMs);
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

                foreach (var request in requests)
                {
                    request.CompletePublication();
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
            foreach (var request in currentRequests)
            {
                request.CompletePublication();
            }

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

    private (bool Changed, bool Reconciled) PublishSourceSnapshot(
        IAppSource source,
        IReadOnlyList<AppCatalogItem> items,
        bool fullyReconciled,
        bool diagnostics,
        long batchId,
        long loadedAt)
    {
        IReadOnlyList<AppCatalogItemChange> changes = [];
        var snapshotsChanged = false;
        var publicationStarted = diagnostics ? _timeProvider.GetTimestamp() : 0;
        double? buildMs = null;
        AppCatalogSnapshot publicationSnapshot;
        lock (_stateLock)
        {
            if (_disposed || !_sourcesById.TryGetValue(source.Id, out var active) || !ReferenceEquals(active, source))
            {
                return (false, false);
            }

            if (!_publishedState.SourceSnapshots.TryGetValue(source.Id, out var currentItems)
                || !AppCatalogItem.HaveSamePersistedContent(currentItems, items))
            {
                var snapshots = new Dictionary<string, IReadOnlyList<AppCatalogItem>>(_publishedState.SourceSnapshots, StringComparer.Ordinal)
                {
                    [source.Id] = items,
                };
                changes = PublishSnapshotsUnderLock(snapshots, out buildMs);
                snapshotsChanged = true;
            }

            publicationSnapshot = _publishedState.Snapshot;
        }

        var publicationFinished = diagnostics ? _timeProvider.GetTimestamp() : 0;
        if (changes.Count > 0)
        {
            RaiseChanged(changes);
        }

        if (diagnostics)
        {
            var publicationMs = _timeProvider.GetElapsedTime(publicationStarted, publicationFinished).TotalMilliseconds;
            var observerMs = _timeProvider.GetElapsedTime(publicationFinished).TotalMilliseconds;
            var delayMs = _timeProvider.GetElapsedTime(loadedAt, publicationFinished).TotalMilliseconds;
            LogDiagnosticPublication(_logger, batchId, source.Id, changes.Count, publicationMs, buildMs, observerMs, publicationSnapshot.Items.Count, publicationSnapshot.HiddenItems.Count + publicationSnapshot.PatternHiddenItems.Count);
            LogDiagnosticPublicationDelay(_logger, batchId, source.Id, delayMs);
        }

        CompleteInitialPublication(source);
        return (snapshotsChanged, fullyReconciled);
    }

    private void CompleteInitialPublication(IAppSource source)
    {
        TaskCompletionSource? publication = null;
        lock (_stateLock)
        {
            if (_sourcesById.TryGetValue(source.Id, out var active) && ReferenceEquals(active, source))
            {
                _initialSourcePublications.Remove(source.Id, out publication);
            }
        }

        publication?.TrySetResult();
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
        IReadOnlyList<IAppSource> initializedSources,
        out double? buildMs)
    {
        buildMs = null;
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

            changes = PublishSnapshotsUnderLock(combined, out buildMs);
        }

        if (changes.Count > 0)
        {
            RaiseChanged(changes);
        }
    }

    /// <inheritdoc />
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
                || sourceIndex < 0)
            {
                return;
            }

            var hiddenIdentities = AppCatalogVisibility.UpdateHiddenIdentities(
                _visibilityStore.GetSnapshot(), catalogItem, hidden, publishedState.Snapshot.CommandAliases);
            if (!_visibilityStore.SetSnapshot(hiddenIdentities))
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
            ? new AppCatalogSnapshot(newSource, newDestination, publishedState.Snapshot.PatternHiddenItems, publishedState.Snapshot.CommandAliases)
            : new AppCatalogSnapshot(newDestination, newSource, publishedState.Snapshot.PatternHiddenItems, publishedState.Snapshot.CommandAliases);
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
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> snapshots,
        out double? buildMs)
    {
        var diagnostics = ShouldLogDiagnostics();
        var started = diagnostics ? _timeProvider.GetTimestamp() : 0;
        var publication = _publicationBuilder.Build(
            snapshots, _publishedState, _commandAliases.GetSnapshot(), _visibilityStore.GetSnapshot(), _visibilityRules);
        buildMs = diagnostics ? _timeProvider.GetElapsedTime(started).TotalMilliseconds : null;
        Volatile.Write(ref _publishedState, publication.State);
        _commandAliases.SetSnapshot(publication.State.Snapshot.CommandAliases);
        return publication.Changes;
    }

    private void OnVisibilitySettingsChanged(object sender, Settings args)
    {
        var updated = new AppCatalogVisibility(_settings.ExcludedAppNames, _settings.ExcludedAppPaths);
        lock (_stateLock)
        {
            if (_disposed || _visibilityRules.HasSamePatterns(updated))
            {
                return;
            }

            _visibilityRules = updated;
        }

        OnFilterChanged(this, EventArgs.Empty);
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

                changes = PublishSnapshotsUnderLock(_publishedState.SourceSnapshots, out _);
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
                    if (_pendingRefreshes.Remove(existing.Key, out var pending))
                    {
                        pending.CompletePublication();
                    }

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
                changes = PublishSnapshotsUnderLock(snapshots, out _);
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
        IReadOnlyList<string> reusedRejectedPaths,
        bool incomplete,
        IReadOnlyList<AppSourcePathChange>? checkedPaths)
    {
        if (_disposed || !_sourcesById.TryGetValue(source.Id, out var active) || !ReferenceEquals(active, source))
        {
            return;
        }

        var rejected = new HashSet<string>(retryPaths, StringComparer.OrdinalIgnoreCase);
        rejected.UnionWith(reusedRejectedPaths);
        if (_sourceRetries.TryGetValue(source.Id, out var state))
        {
            foreach (var path in state.Paths.Keys.ToArray())
            {
                var checkedPath = checkedPaths is null || checkedPaths.Any(change => string.Equals(change.Path, path, StringComparison.OrdinalIgnoreCase));
                if ((path.Length == 0 && !incomplete && checkedPaths is null) || (path.Length > 0 && checkedPath && !rejected.Contains(path)))
                {
                    state.Paths.Remove(path);
                }
                else if (rejected.Contains(path) && state.Paths[path].DueAtUtc == DateTimeOffset.MaxValue)
                {
                    // A full scan can lose a retry's dirty-path hint when the request reaches its limit.
                    ScheduleRetryUnderLock(source, path);
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
        if (path.Length > 0 && retry is null && state.Paths.Count >= SourceRefreshRequest.MaximumPathChanges)
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

                // Keep live retry deadlines; reconciliation renews only exhausted budgets.
                foreach (var state in _sourceRetries.Values)
                {
                    foreach (var path in state.Paths.Where(pair => pair.Value.Attempt >= RetryDelays.Length).Select(pair => pair.Key).ToArray())
                    {
                        state.Paths.Remove(path);
                    }
                }

                ArmRetryTimerUnderLock();
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
            request = new SourceRefreshRequest(source, background, ShouldLogDiagnostics() ? _timeProvider.GetTimestamp() : null);
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

    /// <inheritdoc />
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
        _settings.Settings.SettingsChanged -= OnVisibilitySettingsChanged;

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

        _commandAliases.Dispose();
        _disposeCancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record PendingRetry(int Attempt, DateTimeOffset DueAtUtc);

    private sealed class SourceRetryState
    {
        public IAppSource Source { get; }

        public Dictionary<string, PendingRetry> Paths { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Initializes a new instance of the <see cref="SourceRetryState"/> class. Creates retry tracking for one active source instance.</summary>
        public SourceRetryState(IAppSource source)
        {
            Source = source;
        }
    }
}
