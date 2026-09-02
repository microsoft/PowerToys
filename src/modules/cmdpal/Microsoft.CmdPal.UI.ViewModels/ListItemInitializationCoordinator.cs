// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;

namespace Microsoft.CmdPal.UI.ViewModels;

internal sealed class ListItemInitializationCoordinator
{
    private const int NotStarted = 0;
    private const int Running = 1;
    private const int Completed = 2;

    private readonly ListItemViewModel[] _items;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ListItemInitializationDemandNode? _incomingRequests;
    private ListItemInitializationDemandNode? _priorityRequests;
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
        // publishing a node never takes a worker-owned lock. The single consumer
        // reverses each detached batch to preserve publication order.
        var entry = new ListItemInitializationDemandNode(demand);
        ListItemInitializationDemandNode? head;
        do
        {
            head = Volatile.Read(ref _incomingRequests);
            entry.Next = head;
        }
        while (Interlocked.CompareExchange(ref _incomingRequests, entry, head) != head);

        if (Volatile.Read(ref _accepting) == 0)
        {
            Interlocked.Exchange(ref _incomingRequests, null);
            return false;
        }

        return true;
    }

    internal void Run(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _runState, Running, NotStarted) != NotStarted)
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
            _priorityRequests = null;

            // This worker ran, so this worker publishes its own return.
            Volatile.Write(ref _runState, Completed);
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
        if (Interlocked.CompareExchange(ref _runState, Completed, NotStarted) == NotStarted)
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
        Interlocked.Exchange(ref _incomingRequests, null);
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
            if (_priorityRequests is null)
            {
                var incoming = Interlocked.Exchange(ref _incomingRequests, null);
                while (incoming is not null)
                {
                    var next = incoming.Next;
                    incoming.Next = _priorityRequests;
                    _priorityRequests = incoming;
                    incoming = next;
                }
            }

            if (_priorityRequests is not { } entry)
            {
                item = null!;
                return false;
            }

            _priorityRequests = entry.Next;
            var demand = entry.Demand;
            if (!IsDemandServiceable(demand))
            {
                continue;
            }

            item = demand.Item;
            return true;
        }
    }
}
