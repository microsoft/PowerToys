// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*
 #define CMDPAL_FF_MAINPAGE_TIME_FALLBACK_UPDATES
*/

using System.Collections.Concurrent;
using System.Diagnostics;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Commands;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Manages adaptive dispatch of fallback update work on a dedicated thread pool.
/// Tracks per-command inflight calls, pending-retry slots, and enforces a per-batch
/// sibling-spawn cap to prevent runaway thread expansion.
/// </summary>
internal sealed class FallbackUpdateManager : IDisposable
{
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
    private const int MaxInflightPerFallback = 4;

    // Per-batch cap on sibling workers
    private static readonly int MaxWorkersPerBatch = Math.Max(2, Environment.ProcessorCount / 2);

    private readonly ConcurrentDictionary<string, InflightCounter> _inflightFallbacks = new();

    // Dedicated background threads for fallback COM/RPC calls so they never block the
    // ThreadPool. Stuck extensions consume a dedicated thread, not a pool thread.
    // Max is intentionally above ProcessorCount: blocked threads consume no CPU, so
    // core count is not the right ceiling. Pool expands on demand and shrinks when idle.
    private readonly DedicatedThreadPool _fallbackThreadPool = new(minThreads: InitialFallbackWorkers, maxThreads: MaximumFallbackWorkersMaxThreads, name: "Fallbacks");

    private readonly Action _onFallbackChanged;

#if CMDPAL_FF_MAINPAGE_TIME_FALLBACK_UPDATES
    private ulong _updateBatchCounter;
#endif

    internal FallbackUpdateManager(Action onFallbackChanged)
    {
        _onFallbackChanged = onFallbackChanged;
    }

    internal void BeginUpdate(string query, IReadOnlyList<TopLevelViewModel> commands, CancellationToken cancellationToken)
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
        // - Start 2 workers; each claims commands via a shared atomic index (FIFO, no double-work).
        // - If a command is slow (> FallbackItemSlowTimeout), the worker spawns a sibling so
        //     remaining fast commands aren't blocked waiting in the worker's loop.
        // - _onFallbackChanged is called on the dedicated thread when a result changes
        var sharedIndex = 0;
        var totalCommands = commands.Count;
        var startingWorkers = Math.Min(InitialFallbackWorkers, totalCommands);
        var activeWorkerCount = startingWorkers;

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
                        // Per-batch cap — restore the constraint from ParallelHelper
                        var current = Volatile.Read(ref activeWorkerCount);
                        if (current < MaxWorkersPerBatch
                            && Interlocked.CompareExchange(ref activeWorkerCount, current + 1, current) == current)
                        {
                            _ = _fallbackThreadPool.QueueAsync(Worker, cancellationToken);
                        }
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
                    _onFallbackChanged();
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

        for (var i = 0; i < startingWorkers; i++)
        {
            _ = _fallbackThreadPool.QueueAsync(Worker, cancellationToken);
        }

        return;

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
                _onFallbackChanged();
            }
        }
    }

    public void Dispose()
    {
        _fallbackThreadPool.Dispose();
        _inflightFallbacks.Clear();
    }

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
