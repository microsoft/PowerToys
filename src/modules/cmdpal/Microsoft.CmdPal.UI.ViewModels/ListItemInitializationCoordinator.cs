// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed class ListItemInitializationCoordinator
{
    private readonly ListItemViewModel[] _items;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Stack<ListItemInitializationDemand> _priorityRequests = new();
    private ListItemInitializationDemandStack _incomingRequests; // can't be readonly
    private int _accepting = 1;
    private int _runState;

    internal ListItemInitializationCoordinator(ListItemViewModel[] items)
    {
        _items = items;
        foreach (var item in items)
        {
            item.AttachInitializationCoordinator(this);
        }
    }

    // Completion means the initializer has returned, not just that Stop was called.
    // A selection fallback must not start another initializer while this one is exiting.
    internal Task Completion => _completion.Task;

    internal bool TryEnqueue(ListItemInitializationDemand demand)
    {
        if (Volatile.Read(ref _accepting) == 0 || !IsDemandServiceable(demand))
        {
            return false;
        }

        // Producers include the UI thread. Unlike ConcurrentQueue segment growth,
        // publishing a node never takes a worker-owned lock.
        // Prune here as well as in the item so a blocked consumer cannot retain recycled requests forever.
        _incomingRequests.Push(demand);

        if (Volatile.Read(ref _accepting) == 0)
        {
            _incomingRequests.Clear();
            return false;
        }

        return true;
    }

    internal void Run(CancellationToken cancellationToken)
    {
        // CAS _runState
        // Exactly one Run call may claim the worker.
        // A failed claim also covers Stop completing a worker that never started.
        if (Interlocked.CompareExchange(ref _runState, RunState.Running, RunState.NotStarted) != RunState.NotStarted)
        {
            return;
        }

        try
        {
            var speculativeIndex = 0;
            while (Volatile.Read(ref _accepting) != 0 && !cancellationToken.IsCancellationRequested)
            {
                if (TryTakePriorityRequest(out var item))
                {
                    InitializeItem(item);
                    continue;
                }

                while (speculativeIndex < _items.Length && !IsItemServiceable(_items[speculativeIndex]))
                {
                    speculativeIndex++;
                }

                if (speculativeIndex >= _items.Length)
                {
                    return;
                }

                item = _items[speculativeIndex++];
                InitializeItem(item);
            }
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to coordinate list item initialization", ex);
        }
        finally
        {
            StopAccepting();

            // Consumer-owned, so only this thread may clear it.
            _priorityRequests.Clear();

            // This worker ran, so this worker publishes its own return.
            Volatile.Write(ref _runState, RunState.Completed);
            SignalCompleted();
        }
    }

    /// <summary>
    /// Stops accepting work. This is not "the worker has finished": a running
    /// executor keeps owning <see cref="Completion"/> and publishes it from
    /// <see cref="Run"/>. Only a worker that never started is completed here.
    /// </summary>
    internal void Stop()
    {
        StopAccepting();

        // CAS _runState
        // Complete here only if Run never claimed the worker.
        // A running worker must settle Completion after its initializer returns.
        if (Interlocked.CompareExchange(ref _runState, RunState.Completed, RunState.NotStarted) == RunState.NotStarted)
        {
            SignalCompleted();
        }
    }

    private bool IsItemServiceable(ListItemViewModel item) =>
        item.IsAttachedTo(this) && !item.IsInitializationComplete;

    private bool IsDemandServiceable(ListItemInitializationDemand demand) =>
        demand.IsActive && IsItemServiceable(demand.Item);

    private void StopAccepting()
    {
        Interlocked.Exchange(ref _accepting, 0);
        _incomingRequests.Clear();
    }

    // Publishes "the executor has returned". Callers must own that transition:
    // Run's final cleanup, or Stop when no worker ever claimed _runState.
    private void SignalCompleted() => _completion.TrySetResult();

    private static void InitializeItem(ListItemViewModel item)
    {
        try
        {
            item.InitializePropertiesOnce();
        }
        catch (Exception ex)
        {
            // SafeInitializeProperties handles ordinary extension failures. Contain
            // an exception from its error cleanup to this item as well.
            CoreLogger.LogError("Failed to initialize a list item", ex);
        }
    }

    private bool TryTakePriorityRequest(out ListItemViewModel item)
    {
        while (true)
        {
            if (_priorityRequests.Count == 0)
            {
                var incoming = _incomingRequests.TakeAll();
                while (incoming is not null)
                {
                    // Copy demand into a consumer-owned stack to restore FIFO order.
                    // Producers may still be pruning the detached links, so those
                    // links must keep their original direction.
                    if (IsDemandServiceable(incoming.Demand))
                    {
                        _priorityRequests.Push(incoming.Demand);
                    }

                    incoming = incoming.Next;
                }
            }

            if (!_priorityRequests.TryPop(out var demand))
            {
                item = null!;
                return false;
            }

            if (!IsDemandServiceable(demand))
            {
                continue;
            }

            item = demand.Item;
            return true;
        }
    }

    /// <summary>
    /// Integer states used to atomically coordinate ownership of the single worker.
    /// </summary>
    private static class RunState
    {
        /// <summary>
        /// No worker has claimed execution.
        /// </summary>
        public const int NotStarted = 0;

        /// <summary>
        /// A worker owns execution and is responsible for publishing completion.
        /// </summary>
        public const int Running = 1;

        /// <summary>
        /// Execution has finished, or was stopped before a worker claimed it.
        /// </summary>
        public const int Completed = 2;
    }
}
