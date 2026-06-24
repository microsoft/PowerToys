// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// An elastic pool of dedicated background threads for running blocking work
/// off the ThreadPool. Starts with <c>minThreads</c> always-alive threads and
/// expands up to <c>maxThreads</c> on demand. Threads above the minimum exit
/// automatically after <c>idleTimeout</c> with no work. Items are processed
/// FIFO; cancelled items are skipped at dequeue time.
/// </summary>
internal sealed partial class DedicatedThreadPool : IDisposable
{
    private const int DrainTimeoutMs = 3000;

    private readonly BlockingCollection<Action> _workQueue = new();
    private readonly int _minThreads;
    private readonly int _maxThreads;
    private readonly TimeSpan _idleTimeout;
    private readonly string _name;

    // Total live threads (Interlocked). Owned by the thread that wins the CAS.
    private int _threadCount;

    // Threads currently blocked in TryTake waiting for work (Interlocked).
    // Used as the expansion trigger: if zero, all threads are busy.
    private int _idleCount;

    // Ever-increasing counter for unique thread names across expand/shrink cycles.
    private int _nextThreadId;

    private InterlockedBoolean _disposed;

    public DedicatedThreadPool(int minThreads, int maxThreads, string name = "DedicatedWorker", TimeSpan? idleTimeout = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minThreads);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxThreads, minThreads);

        _minThreads = minThreads;
        _maxThreads = maxThreads;
        _name = name;
        _idleTimeout = idleTimeout ?? TimeSpan.FromSeconds(30);

        _threadCount = minThreads;
        for (var i = 0; i < minThreads; i++)
        {
            StartThread();
        }
    }

    private void StartThread()
    {
        var id = Interlocked.Increment(ref _nextThreadId);
        var thread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = $"{_name}-{id}",
            Priority = ThreadPriority.BelowNormal,
        };
        thread.Start();
    }

    private void WorkerLoop()
    {
        while (true)
        {
            Interlocked.Increment(ref _idleCount);

            bool got;
            Action? action;
            try
            {
                got = _workQueue.TryTake(out action, _idleTimeout);
            }
            catch (ObjectDisposedException)
            {
                // Pool was disposed while we were waiting.
                Interlocked.Decrement(ref _idleCount);
                Interlocked.Decrement(ref _threadCount);
                return;
            }

            Interlocked.Decrement(ref _idleCount);

            if (got)
            {
                try
                {
                    action!();
                }
                catch (Exception)
                {
                    // QueueAsync wraps work in its own try-catch, so this should
                    // never fire. Keep the thread alive defensively.
                }

                continue;
            }

            // TryTake timed out (no work for idleTimeout).
            if (_workQueue.IsCompleted)
            {
                break;
            }

            // Try to shrink: exit if we're above the minimum.
            // CAS ensures exactly one thread wins each decrement race.
            while (true)
            {
                var count = _threadCount;
                if (count <= _minThreads)
                {
                    break; // At minimum — stay alive.
                }

                if (Interlocked.CompareExchange(ref _threadCount, count - 1, count) == count)
                {
                    return; // Decremented successfully — this thread exits.
                }

                // Another thread changed _threadCount concurrently; retry.
            }
        }

        Interlocked.Decrement(ref _threadCount);
    }

    /// <summary>
    /// Queue a blocking work item. Returns a <see cref="Task"/> that
    /// completes when the work finishes on a dedicated thread.
    /// If <paramref name="cancellationToken"/> is already cancelled when
    /// the item reaches the front of the queue, it is skipped immediately.
    /// Spawns an extra thread (up to <c>maxThreads</c>) if all current
    /// threads are occupied.
    /// </summary>
    public Task QueueAsync(Action work, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _workQueue.Add(
                () =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                        return;
                    }

                    try
                    {
                        work();
                        tcs.TrySetResult();
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                },
                cancellationToken);

            // If no thread is idle, all are blocked in COM calls — try to expand.
            if (Volatile.Read(ref _idleCount) == 0)
            {
                TryExpand();
            }
        }
        catch (OperationCanceledException)
        {
            tcs.TrySetCanceled(cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            tcs.TrySetCanceled(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // CompleteAdding was called — pool is shutting down.
            tcs.TrySetCanceled(CancellationToken.None);
        }

        return tcs.Task;
    }

    /// <summary>
    /// Queue a blocking work item. Returns a <see cref="Task{T}"/> that
    /// completes when the work finishes on a dedicated thread.
    /// If <paramref name="cancellationToken"/> is already cancelled when
    /// the item reaches the front of the queue, it is skipped immediately.
    /// Spawns an extra thread (up to <c>maxThreads</c>) if all current
    /// threads are occupied.
    /// </summary>
    public Task<T> QueueAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            _workQueue.Add(
                () =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                        return;
                    }

                    try
                    {
                        tcs.TrySetResult(work());
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                },
                cancellationToken);

            // If no thread is idle, all are blocked in COM calls — try to expand.
            if (Volatile.Read(ref _idleCount) == 0)
            {
                TryExpand();
            }
        }
        catch (OperationCanceledException)
        {
            tcs.TrySetCanceled(cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            tcs.TrySetCanceled(CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // CompleteAdding was called — pool is shutting down.
            tcs.TrySetCanceled(CancellationToken.None);
        }

        return tcs.Task;
    }

    /// <summary>
    /// Attempt to spawn one additional thread, up to <c>maxThreads</c>.
    /// CAS on <c>_threadCount</c> ensures at most one thread wins per slot.
    /// </summary>
    private void TryExpand()
    {
        if (_disposed.Value)
        {
            return;
        }

        while (true)
        {
            var count = _threadCount;
            if (count >= _maxThreads)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _threadCount, count + 1, count) == count)
            {
                StartThread();
                return;
            }

            // Another concurrent expand won this slot; recheck the ceiling.
        }
    }

    public void Dispose()
    {
        if (!_disposed.Set())
        {
            return;
        }

        _workQueue.CompleteAdding();

        // Give worker threads a chance to drain remaining items and exit.
        // After CompleteAdding, idle threads see IsCompleted and exit
        // quickly, but threads blocked in long COM calls won't return
        // until their call finishes — don't wait forever.
        var deadline = Environment.TickCount64 + DrainTimeoutMs;
        var spin = default(SpinWait);
        while (Volatile.Read(ref _threadCount) > 0 && Environment.TickCount64 < deadline)
        {
            spin.SpinOnce();
        }

        // Dispose the queue even if threads are still alive. Threads
        // blocked in TryTake will get ObjectDisposedException and exit
        // via the catch in WorkerLoop. Threads busy in action!() will
        // finish their item, then hit ObjectDisposedException on the
        // next TryTake and exit.
        _workQueue.Dispose();
    }
}
