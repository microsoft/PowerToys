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
            var operation = await InvokeLoadMoreAsync(result, requestedItemCount, cancellationToken).ConfigureAwait(false);
            var publisher = new SnapshotPublisher(
                this,
                source,
                result.Query,
                result.QueryId,
                maximumItemCount,
                cancellationToken);
            operation.Progress = (_, snapshot) => publisher.PublishProgress(snapshot);
            var nextResult = await operation.AsTask(cancellationToken).ConfigureAwait(false);
            publisher.PublishFinal(nextResult);
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
            if (handler is null)
            {
                return;
            }

            var requestedItemCount = source.EffectiveMaximumVisibleItemCount;
            var args = new FallbackQueryArgs(
                query,
                queryId,
                requestedItemCount,
                global::Windows.System.UserProfile.GlobalizationPreferences.Languages.ToArray());
            var operation = await InvokeQueryAsync(handler, args, cancellationToken).ConfigureAwait(false);
            var publisher = new SnapshotPublisher(
                this,
                source,
                query,
                queryId,
                requestedItemCount,
                cancellationToken);
            operation.Progress = (_, snapshot) => publisher.PublishProgress(snapshot);
            var result = await operation.AsTask(cancellationToken).ConfigureAwait(false);
            publisher.PublishFinal(result);
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

    private static Task<IAsyncOperationWithProgress<IFallbackCommandResult, IFallbackCommandResult>> InvokeQueryAsync(
        IFallbackHandler2 handler,
        IFallbackQueryArgs args,
        CancellationToken cancellationToken)
        => StartCancelableAsync(() => handler.QueryAsync(args), cancellationToken);

    private static Task<IAsyncOperationWithProgress<IFallbackCommandResult, IFallbackCommandResult>> InvokeLoadMoreAsync(
        IFallbackCommandResult result,
        uint requestedItemCount,
        CancellationToken cancellationToken)
        => StartCancelableAsync(() => result.LoadMoreItemsAsync(requestedItemCount), cancellationToken);

    /// <summary>
    /// Starts a call into an extension and stops waiting for it when the query is cancelled.
    /// </summary>
    /// <remarks>
    /// The call itself can block, so it runs on a pool thread. A cancelled query stops
    /// waiting at once, but the call keeps running. The continuation cancels whatever the
    /// call returns later, so the extension does not keep working for a query the user
    /// already replaced.
    /// </remarks>
    private static async Task<IAsyncOperationWithProgress<IFallbackCommandResult, IFallbackCommandResult>> StartCancelableAsync(
        Func<IAsyncOperationWithProgress<IFallbackCommandResult, IFallbackCommandResult>> start,
        CancellationToken cancellationToken)
    {
        var callTask = Task.Run(start, CancellationToken.None);
        try
        {
            return await callTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _ = callTask.ContinueWith(
                static task =>
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        task.Result?.Cancel();
                    }
                    else
                    {
                        // Observe the fault so it does not surface as unhandled.
                        _ = task.Exception;
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            throw;
        }
    }

    public void Dispose()
    {
        // Stop new queries. The caller cancels the query token before it disposes us,
        // so work that is already running unwinds on its own.
        _disposed = true;
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
