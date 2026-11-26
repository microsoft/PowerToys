// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Runtime.InteropServices.WindowsRuntime;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed partial class FallbackResultQueryManager : IDisposable
{
    internal const uint InitialRequestedItemCount = 5;

    // Fallback queries cross the process boundary and can be slow. This cap keeps a slow
    // extension from holding every thread that other sources need.
    private const int MaximumConcurrentQueries = 8;

    private readonly SemaphoreSlim _querySlots = new(MaximumConcurrentQueries);
    private readonly DedicatedThreadPool _workers = new(minThreads: 2, maxThreads: MaximumConcurrentQueries, name: "FallbackResults");
    private readonly ConcurrentDictionary<string, byte> _loadingSources = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sourceLocks = new(StringComparer.Ordinal);
    private readonly Action<TopLevelViewModel, IFallbackCommandResult, bool, uint, CancellationToken> _publishSnapshot;
    private readonly Action<IFallbackCommandResult> _discardSnapshot;
    private volatile bool _disposed;

    internal FallbackResultQueryManager(
        Action<TopLevelViewModel, IFallbackCommandResult, bool, uint, CancellationToken> publishSnapshot,
        Action<IFallbackCommandResult> discardSnapshot)
    {
        _publishSnapshot = publishSnapshot;
        _discardSnapshot = discardSnapshot;
    }

    internal void BeginUpdate(
        string query,
        string queryId,
        IReadOnlyList<TopLevelViewModel> sources,
        CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        foreach (var source in sources)
        {
            _ = Task.Run(() => RunSourceAsync(source, query, queryId, cancellationToken), CancellationToken.None);
        }
    }

    internal void LoadMore(
        TopLevelViewModel source,
        FallbackSnapshotLease resultLease,
        int acceptedItemCount,
        CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return;
        }

        var operationLease = resultLease.Acquire();
        if (operationLease is null)
        {
            return;
        }

        if (_loadingSources.TryAdd(source.FallbackKey, 0))
        {
            _ = Task.Run(
                () => RunLoadMoreAsync(source, resultLease.Snapshot, operationLease, acceptedItemCount, cancellationToken),
                CancellationToken.None);
        }
        else
        {
            operationLease.Dispose();
        }
    }

    private async Task RunLoadMoreAsync(
        TopLevelViewModel source,
        IFallbackCommandResult result,
        IDisposable operationLease,
        int acceptedItemCount,
        CancellationToken cancellationToken)
    {
        var sourceLock = _sourceLocks.GetOrAdd(source.FallbackKey, static _ => new SemaphoreSlim(1, 1));
        var sourceLockAcquired = false;
        var querySlotAcquired = false;
        try
        {
            await sourceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            sourceLockAcquired = true;
            await _querySlots.WaitAsync(cancellationToken).ConfigureAwait(false);
            querySlotAcquired = true;

            var requestedItemCount = source.EffectiveMaximumVisibleItemCount;
            var maximumItemCount = (uint)acceptedItemCount + requestedItemCount;
            await _workers.QueueAsync(
                () =>
                {
                    var publisher = new SnapshotPublisher(this, source, result.Query, result.QueryId, maximumItemCount, cancellationToken);
                    RunOperation(result.LoadMoreItemsAsync(requestedItemCount), publisher, cancellationToken);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError($"Fallback result source '{source.Id}' failed to load more items.", ex);
        }
        finally
        {
            if (querySlotAcquired)
            {
                _querySlots.Release();
            }

            if (sourceLockAcquired)
            {
                sourceLock.Release();
            }

            operationLease.Dispose();
            _loadingSources.TryRemove(source.FallbackKey, out _);
        }
    }

    private async Task RunSourceAsync(
        TopLevelViewModel source,
        string query,
        string queryId,
        CancellationToken cancellationToken)
    {
        var sourceLock = _sourceLocks.GetOrAdd(source.FallbackKey, static _ => new SemaphoreSlim(1, 1));
        var sourceLockAcquired = false;
        var querySlotAcquired = false;
        try
        {
            var minimumLength = source.EffectiveMinimumQueryLength;
            if ((uint)query.Length < minimumLength)
            {
                return;
            }

            var delay = Math.Min(source.EffectiveQueryDelayMilliseconds, FallbackSettings.MaximumQueryDelayMilliseconds);
            if (delay > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken).ConfigureAwait(false);
            }

            await sourceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            sourceLockAcquired = true;
            await _querySlots.WaitAsync(cancellationToken).ConfigureAwait(false);
            querySlotAcquired = true;

            var handler = source.FallbackQueryHandler;
            if (!handler.HasValue)
            {
                return;
            }

            var requestedItemCount = source.EffectiveMaximumVisibleItemCount;
            var args = new FallbackQueryArgs(
                query,
                queryId,
                requestedItemCount,
                global::Windows.System.UserProfile.GlobalizationPreferences.Languages.ToArray());
            var publisher = new SnapshotPublisher(
                this,
                source,
                query,
                queryId,
                requestedItemCount,
                cancellationToken);
            await _workers.QueueAsync(
                () => RunOperation(handler.Unsafe!.QueryAsync(args), publisher, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError($"Fallback result source '{source.Id}' failed.", ex);
        }
        finally
        {
            if (querySlotAcquired)
            {
                _querySlots.Release();
            }

            if (sourceLockAcquired)
            {
                sourceLock.Release();
            }
        }
    }

    private void PublishIfCurrent(
        TopLevelViewModel source,
        IFallbackCommandResult? snapshot,
        string query,
        string queryId,
        bool isFinal,
        uint maximumItemCount,
        CancellationToken cancellationToken)
    {
        if (snapshot is null
            || cancellationToken.IsCancellationRequested
            || !string.Equals(snapshot.Query, query, StringComparison.Ordinal)
            || !string.Equals(snapshot.QueryId, queryId, StringComparison.Ordinal))
        {
            if (snapshot is not null)
            {
                _discardSnapshot(snapshot);
            }

            return;
        }

        _publishSnapshot(source, snapshot, isFinal, maximumItemCount, cancellationToken);
    }

    // Keep the worker, source slot and (for load-more) snapshot lease until the
    // extension returns, even if a newer query cancels this one in the meantime.
    private static void RunOperation(
        IAsyncOperationWithProgress<IFallbackCommandResult, IFallbackCommandResult> operation,
        SnapshotPublisher publisher,
        CancellationToken cancellationToken)
    {
        operation.Progress = (_, snapshot) => publisher.PublishProgress(snapshot);
        var result = operation.AsTask(cancellationToken).GetAwaiter().GetResult();
        publisher.PublishFinal(result);
    }

    public void Dispose()
    {
        // Stop new queries. The caller cancels the query token before it disposes us,
        // so work that is already running unwinds on its own.
        _disposed = true;
        _workers.Dispose();
        _loadingSources.Clear();

        // Do not dispose the semaphores. A query that is still unwinding releases the
        // one it holds, and a disposed SemaphoreSlim throws on Release. They hold no
        // unmanaged handle, because this class never reads AvailableWaitHandle, so the
        // garbage collector reclaims them after the last query lets go.
        _sourceLocks.Clear();
    }

    private sealed class SnapshotPublisher
    {
        private readonly FallbackResultQueryManager _manager;
        private readonly TopLevelViewModel _source;
        private readonly string _query;
        private readonly string _queryId;
        private readonly uint _maximumItemCount;
        private readonly CancellationToken _cancellationToken;
        private readonly Lock _lock = new();
        private bool _finalPublished;

        internal SnapshotPublisher(
            FallbackResultQueryManager manager,
            TopLevelViewModel source,
            string query,
            string queryId,
            uint maximumItemCount,
            CancellationToken cancellationToken)
        {
            _manager = manager;
            _source = source;
            _query = query;
            _queryId = queryId;
            _maximumItemCount = maximumItemCount;
            _cancellationToken = cancellationToken;
        }

        internal void PublishProgress(IFallbackCommandResult snapshot)
        {
            lock (_lock)
            {
                if (_finalPublished)
                {
                    _manager._discardSnapshot(snapshot);
                    return;
                }

                _manager.PublishIfCurrent(
                    _source,
                    snapshot,
                    _query,
                    _queryId,
                    false,
                    _maximumItemCount,
                    _cancellationToken);
            }
        }

        internal void PublishFinal(IFallbackCommandResult snapshot)
        {
            lock (_lock)
            {
                if (_finalPublished)
                {
                    _manager._discardSnapshot(snapshot);
                    return;
                }

                _finalPublished = true;
                _manager.PublishIfCurrent(
                    _source,
                    snapshot,
                    _query,
                    _queryId,
                    true,
                    _maximumItemCount,
                    _cancellationToken);
            }
        }
    }
}
