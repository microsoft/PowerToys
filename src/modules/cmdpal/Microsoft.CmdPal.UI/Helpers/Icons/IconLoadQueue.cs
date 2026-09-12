// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Orders icon work by live UI demand, then by the existing high/low priority.
/// </summary>
internal sealed class IconLoadQueue
{
    private const int HighPriorityCapacity = 32;

    // UI requesters only publish commands. A dedicated background coordinator is
    // the sole owner of the linked lists, so the WinUI thread never waits on scheduler state.
    private readonly ConcurrentQueue<QueueCommand> _commands = new();
    private readonly SingleConsumerSignal _commandSignal = new();
    private readonly Channel<Operation> _readyWork;
    private readonly LinkedList<WorkItem> _demandedHigh = new();
    private readonly LinkedList<WorkItem> _demandedLow = new();
    private readonly LinkedList<WorkItem> _speculativeHigh = new();
    private readonly LinkedList<WorkItem> _speculativeLow = new();
    private readonly Queue<IconLoadDiagnostics.SchedulerCommandMeasurement?> _availableWorkerMeasurements = new();
    private readonly TaskCompletionSource<bool> _schedulerCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _schedulerThread;
    private readonly int _workerSlotsReservedForDemand;
    private readonly int _workerCount;

    private TaskCompletionSource<bool>? _faultedEnqueuePublishersDrained;
    private int _accepting = 1;
    private int _activeDequeuers;
    private int _activeEnqueuePublishers;
    private int _completionCommandPublished;
    private int _coordinatorFailed;
    private int _highPriorityCount;

    // Accessed only by the background coordinator.
    private int _availableWorkerSlots;
    private bool _completionRequested;
    private IconLoadDiagnostics.DemandedIdleCapacityMeasurement? _demandedIdleCapacityMeasurement;
    private IconLoadDiagnostics.SpeculativeDispatchDeferralMeasurement? _speculativeDispatchDeferralMeasurement;

    public IconLoadQueue(int workerCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(workerCount, 1);
        _workerCount = workerCount;

        // Keep one consumer waiting for live demand while only speculative work is
        // queued. At two workers this deliberately halves speculative concurrency:
        // reserving no slot would let non-preemptible speculative loads occupy all
        // capacity. A single-worker queue must still make progress.
        _workerSlotsReservedForDemand = workerCount > 1 ? 1 : 0;
        _readyWork = Channel.CreateUnbounded<Operation>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true,
            AllowSynchronousContinuations = false,
        });
        _schedulerThread = new Thread(ProcessCommands)
        {
            IsBackground = true,
            Name = "CmdPal.IconLoadCoordinator",
        };
        _schedulerThread.Start();
    }

    public Task Completion => _schedulerCompletion.Task;

    public bool TryEnqueue(
        Operation operation,
        IconLoadPriority requestedPriority,
        IconLoadDemand demand,
        out IconLoadPriority actualPriority)
    {
        actualPriority = requestedPriority;
        Interlocked.Increment(ref _activeEnqueuePublishers);

        var highPriorityReserved = false;
        WorkItem? item = null;
        try
        {
            if (Volatile.Read(ref _accepting) == 0)
            {
                return false;
            }

            if (requestedPriority == IconLoadPriority.High)
            {
                highPriorityReserved = TryReserveHighPriority();
                if (!highPriorityReserved)
                {
                    actualPriority = IconLoadPriority.Low;
                }
            }

            item = new WorkItem(this, operation, demand, actualPriority);
            demand.Subscribe(item);
            operation.Enqueued(actualPriority, _workerCount);
            Publish(QueueCommand.Enqueue(item));
            return true;
        }
        catch
        {
            if (item is not null)
            {
                item.ReleaseQueueOwnership();
            }
            else if (highPriorityReserved)
            {
                ReleaseHighPriority();
            }

            throw;
        }
        finally
        {
            EnqueuePublisherFinished();
        }
    }

    /// <summary>
    /// Waits for the next work item assigned to one background consumer.
    /// </summary>
    /// <remarks>
    /// The number of simultaneous callers must not exceed the worker count supplied
    /// to the constructor. Each consumer may have only one outstanding call.
    /// </remarks>
    public async ValueTask<Operation?> DequeueAsync()
    {
        var activeDequeuers = Interlocked.Increment(ref _activeDequeuers);
        if (activeDequeuers > _workerCount)
        {
            Interlocked.Decrement(ref _activeDequeuers);
            throw new InvalidOperationException("The icon queue has more active consumers than configured workers.");
        }

        try
        {
            Publish(QueueCommand.WorkerReady());

            try
            {
                return await _readyWork.Reader.ReadAsync().ConfigureAwait(false);
            }
            catch (ChannelClosedException ex) when (ex.InnerException is null)
            {
                return null;
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeDequeuers);
        }
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref _accepting, 0) == 0)
        {
            return;
        }

        TryPublishCompletion();
    }

    private void EnqueuePublisherFinished()
    {
        var activePublishers = Interlocked.Decrement(ref _activeEnqueuePublishers);
        Debug.Assert(activePublishers >= 0, "An enqueue publisher must be registered before it finishes.");
        if (activePublishers == 0 && Volatile.Read(ref _accepting) == 0)
        {
            if (Volatile.Read(ref _coordinatorFailed) != 0)
            {
                Volatile.Read(ref _faultedEnqueuePublishersDrained)?.TrySetResult(true);
            }
            else
            {
                TryPublishCompletion();
            }
        }
    }

    private void TryPublishCompletion()
    {
        if (Volatile.Read(ref _activeEnqueuePublishers) == 0
            && Interlocked.CompareExchange(ref _completionCommandPublished, 1, 0) == 0)
        {
            // Every accepted producer publishes its Enqueue command before decrementing
            // the active count, so Complete cannot overtake accepted work.
            Publish(QueueCommand.Complete());
        }
    }

    private bool TryReserveHighPriority()
    {
        while (true)
        {
            var count = Volatile.Read(ref _highPriorityCount);
            if (count >= HighPriorityCapacity)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _highPriorityCount, count + 1, count) == count)
            {
                return true;
            }
        }
    }

    private void ReleaseHighPriority()
    {
        var count = Interlocked.Decrement(ref _highPriorityCount);
        Debug.Assert(count >= 0, "A high-priority reservation must be released exactly once.");
    }

    private void Publish(QueueCommand command)
    {
        _commands.Enqueue(command);
        _commandSignal.Pulse(command.Measurement);
    }

    internal void FailForTesting(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        Publish(QueueCommand.Fault(failure));
    }

    internal bool CoordinatorFailedForTesting => Volatile.Read(ref _coordinatorFailed) != 0;

    private void ProcessCommands()
    {
        Exception? failure = null;
        QueueCommand? commandBeingProcessed = null;
        try
        {
            while (true)
            {
                var wakeMeasurement = _commandSignal.Wait();
                var measureBatch = wakeMeasurement is not null;
                var drainStartedAt = 0L;
                if (measureBatch)
                {
                    var wokeAt = Stopwatch.GetTimestamp();
                    wakeMeasurement!.Woke(wokeAt);
                    drainStartedAt = Stopwatch.GetTimestamp();
                }

                var commandCount = 0;
                while (_commands.TryDequeue(out var command))
                {
                    commandBeingProcessed = command;
                    if (measureBatch)
                    {
                        commandCount++;
                    }

                    command.Measurement?.Processed();
                    Apply(command);
                    if (command.Measurement is not null
                        || _demandedIdleCapacityMeasurement is not null
                        || _speculativeDispatchDeferralMeasurement is not null)
                    {
                        UpdateDemandedIdleCapacityMeasurement();
                        UpdateSpeculativeDispatchDeferralMeasurement();
                    }

                    commandBeingProcessed = null;
                }

                var drainTicks = measureBatch ? Stopwatch.GetTimestamp() - drainStartedAt : 0;
                var dispatchedWorkItemCount = DispatchAvailableWorkers();
                if (_demandedIdleCapacityMeasurement is not null)
                {
                    UpdateDemandedIdleCapacityMeasurement();
                }

                // Dispatch can consume the last non-reserved slot and begin a
                // deferral interval without another command changing state.
                UpdateSpeculativeDispatchDeferralMeasurement();

                if (measureBatch)
                {
                    wakeMeasurement!.BatchCompleted(
                        commandCount,
                        dispatchedWorkItemCount,
                        drainTicks,
                        Stopwatch.GetTimestamp() - drainStartedAt);
                }

                if (_completionRequested && !HasQueuedWork())
                {
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            failure = ex;
            WaitForEnqueuePublishersAfterFailure();
        }
        finally
        {
            try
            {
                CompleteDemandedIdleCapacityMeasurement();
                CompleteSpeculativeDispatchDeferralMeasurement();
            }
            catch (Exception ex)
            {
                failure = failure is null ? ex : new AggregateException(failure, ex);
                WaitForEnqueuePublishersAfterFailure();
            }

            try
            {
                if (failure is null)
                {
                    ReleaseQueuedItems();
                }
                else
                {
                    failure = FailAcceptedWork(failure, commandBeingProcessed);
                }
            }
            catch (Exception ex)
            {
                failure = failure is null ? ex : new AggregateException(failure, ex);
                WaitForEnqueuePublishersAfterFailure();
            }

            _commandSignal.Close();
            _readyWork.Writer.TryComplete(failure);
            if (failure is null)
            {
                _schedulerCompletion.TrySetResult(true);
            }
            else
            {
                // An exception escaping a raw background thread terminates the process.
                // Preserve the previous Task-based fault contract instead.
                _schedulerCompletion.TrySetException(failure);
            }
        }
    }

    private void WaitForEnqueuePublishersAfterFailure()
    {
        var publisherWaiter = Volatile.Read(ref _faultedEnqueuePublishersDrained);
        if (publisherWaiter is null)
        {
            var newWaiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            publisherWaiter = Interlocked.CompareExchange(
                ref _faultedEnqueuePublishersDrained,
                newWaiter,
                null) ?? newWaiter;
        }

        // Only the dedicated coordinator waits. Producers, including the WinUI STA,
        // retain their existing lock-free publication path and merely signal the waiter.
        Volatile.Write(ref _coordinatorFailed, 1);
        Interlocked.Exchange(ref _accepting, 0);
        if (Volatile.Read(ref _activeEnqueuePublishers) == 0)
        {
            publisherWaiter.TrySetResult(true);
        }

        publisherWaiter.Task.GetAwaiter().GetResult();
    }

    private void Apply(QueueCommand command)
    {
        if (command.Failure is not null)
        {
            throw command.Failure;
        }

        switch (command.Kind)
        {
            case QueueCommandKind.Enqueue:
                Enqueue(command.Item!);
                break;
            case QueueCommandKind.DemandChanged:
                OnDemandChanged(command.Item!);
                break;
            case QueueCommandKind.WorkerReady:
                _availableWorkerSlots++;
                _availableWorkerMeasurements.Enqueue(command.Measurement);
                Debug.Assert(_availableWorkerSlots <= _workerCount, "Each worker can publish only one available slot.");
                break;
            case QueueCommandKind.Complete:
                _completionRequested = true;
                break;
            default:
                throw new UnreachableException();
        }
    }

    private void Enqueue(WorkItem item)
    {
        Debug.Assert(!_completionRequested, "Completion cannot overtake an accepted enqueue.");
        item.IsDemanded = item.Demand.IsDemanded;
        item.Node = GetQueue(item.IsDemanded, item.Priority).AddLast(item);
    }

    private void OnDemandChanged(WorkItem item)
    {
        // Clear the coalescing flag before sampling demand. A transition racing
        // after this write can then publish the follow-up invalidation needed to
        // correct the sampled state; moving this below the read can lose it.
        item.DemandChangeDequeued();
        if (item.Node is not { List: not null } node)
        {
            return;
        }

        // Notifications are invalidations and may overtake one another. Read the
        // current aggregate state instead of trusting a state captured by the caller.
        var isDemanded = item.Demand.IsDemanded;
        if (item.IsDemanded == isDemanded)
        {
            return;
        }

        node.List.Remove(node);
        item.IsDemanded = isDemanded;

        // Ordered by the start of the current demand period: losing every requester
        // relinquishes the position, so an item cannot starve while continuously demanded.
        GetQueue(isDemanded, item.Priority).AddLast(node);
    }

    private int DispatchAvailableWorkers()
    {
        var dispatchedWorkItemCount = 0;
        while (CanDispatchNextWorkItem() && RemoveNext() is { } item)
        {
            try
            {
                _availableWorkerSlots--;
                dispatchedWorkItemCount++;
                var workerMeasurement = _availableWorkerMeasurements.Dequeue();
                item.ReleaseQueueOwnership();

                workerMeasurement?.WorkerDispatched(item.IsDemanded);
                if (!_readyWork.Writer.TryWrite(item.Operation))
                {
                    throw new InvalidOperationException("The icon worker channel was closed unexpectedly.");
                }

                item.MarkDispatched();
            }
            catch (Exception ex)
            {
                item.Fail(ex);
                throw;
            }
        }

        return dispatchedWorkItemCount;
    }

    private bool CanDispatchNextWorkItem()
    {
        if (_availableWorkerSlots == 0)
        {
            return false;
        }

        if (_demandedHigh.Count != 0 || _demandedLow.Count != 0)
        {
            return true;
        }

        // Speculative work uses at most workerCount - 1 consumers. Because a
        // consumer publishes WorkerReady only after its previous load completes,
        // retaining this slot also bounds the number of active speculative loads.
        return _availableWorkerSlots > _workerSlotsReservedForDemand;
    }

    private void UpdateDemandedIdleCapacityMeasurement()
    {
        var hasDemandedWorkWithIdleCapacity = _availableWorkerSlots > 0
            && (_demandedHigh.Count != 0 || _demandedLow.Count != 0);
        if (!hasDemandedWorkWithIdleCapacity)
        {
            CompleteDemandedIdleCapacityMeasurement();
            return;
        }

        if (_demandedIdleCapacityMeasurement is null
            || !_demandedIdleCapacityMeasurement.IsForActiveSession)
        {
            CompleteDemandedIdleCapacityMeasurement();
            _demandedIdleCapacityMeasurement = IconLoadDiagnostics.BeginDemandedIdleCapacity(
                _demandedHigh.Count + _demandedLow.Count,
                _availableWorkerSlots);
            return;
        }

        _demandedIdleCapacityMeasurement.Observe(
            _demandedHigh.Count + _demandedLow.Count,
            _availableWorkerSlots);
    }

    private void CompleteDemandedIdleCapacityMeasurement()
    {
        _demandedIdleCapacityMeasurement?.Complete();
        _demandedIdleCapacityMeasurement = null;
    }

    private void UpdateSpeculativeDispatchDeferralMeasurement()
    {
        var speculativeQueueDepth = _speculativeHigh.Count + _speculativeLow.Count;
        var reserveIsDeferringWork = _workerSlotsReservedForDemand > 0
            && _availableWorkerSlots > 0
            && _availableWorkerSlots <= _workerSlotsReservedForDemand
            && _demandedHigh.Count == 0
            && _demandedLow.Count == 0
            && speculativeQueueDepth > 0;
        if (!reserveIsDeferringWork)
        {
            CompleteSpeculativeDispatchDeferralMeasurement();
            return;
        }

        if (_speculativeDispatchDeferralMeasurement is null
            || !_speculativeDispatchDeferralMeasurement.IsForActiveSession)
        {
            CompleteSpeculativeDispatchDeferralMeasurement();
            _speculativeDispatchDeferralMeasurement = IconLoadDiagnostics.BeginSpeculativeDispatchDeferral(
                speculativeQueueDepth,
                _workerCount,
                _availableWorkerSlots);
            return;
        }

        _speculativeDispatchDeferralMeasurement.Observe(
            speculativeQueueDepth,
            _availableWorkerSlots);
    }

    private void CompleteSpeculativeDispatchDeferralMeasurement()
    {
        _speculativeDispatchDeferralMeasurement?.Complete();
        _speculativeDispatchDeferralMeasurement = null;
    }

    // Strict order by design: speculative work may starve while demanded work keeps
    // arriving, since no live request is waiting on it.
    private WorkItem? RemoveNext()
    {
        return RemoveFirst(_demandedHigh)
            ?? RemoveFirst(_demandedLow)
            ?? RemoveFirst(_speculativeHigh)
            ?? RemoveFirst(_speculativeLow);
    }

    private bool HasQueuedWork()
    {
        return _demandedHigh.Count != 0
            || _demandedLow.Count != 0
            || _speculativeHigh.Count != 0
            || _speculativeLow.Count != 0;
    }

    private LinkedList<WorkItem> GetQueue(bool isDemanded, IconLoadPriority priority)
    {
        return (isDemanded, priority) switch
        {
            (true, IconLoadPriority.High) => _demandedHigh,
            (true, IconLoadPriority.Low) => _demandedLow,
            (false, IconLoadPriority.High) => _speculativeHigh,
            _ => _speculativeLow,
        };
    }

    private void ReleaseQueuedItems()
    {
        ReleaseQueuedItems(_demandedHigh);
        ReleaseQueuedItems(_demandedLow);
        ReleaseQueuedItems(_speculativeHigh);
        ReleaseQueuedItems(_speculativeLow);
    }

    private void ReleaseQueuedItems(LinkedList<WorkItem> queue)
    {
        while (RemoveFirst(queue) is { } item)
        {
            item.ReleaseQueueOwnership();
        }
    }

    private Exception FailAcceptedWork(Exception failure, QueueCommand? commandBeingProcessed)
    {
        Exception? cleanupFailure = null;
        if (commandBeingProcessed is { Kind: QueueCommandKind.Enqueue, Item: { } activeItem })
        {
            TryFail(activeItem);
        }

        while (_commands.TryDequeue(out var command))
        {
            if (command is { Kind: QueueCommandKind.Enqueue, Item: { } item })
            {
                TryFail(item);
            }
        }

        FailQueuedItems(_demandedHigh);
        FailQueuedItems(_demandedLow);
        FailQueuedItems(_speculativeHigh);
        FailQueuedItems(_speculativeLow);
        return cleanupFailure is null ? failure : new AggregateException(failure, cleanupFailure);

        void FailQueuedItems(LinkedList<WorkItem> queue)
        {
            while (RemoveFirst(queue) is { } item)
            {
                TryFail(item);
            }
        }

        void TryFail(WorkItem item)
        {
            try
            {
                item.Fail(failure);
            }
            catch (Exception ex)
            {
                cleanupFailure = cleanupFailure is null ? ex : new AggregateException(cleanupFailure, ex);
            }
        }
    }

    private static WorkItem? RemoveFirst(LinkedList<WorkItem> queue)
    {
        if (queue.First is not { } node)
        {
            return null;
        }

        queue.RemoveFirst();
        node.Value.Node = null;
        return node.Value;
    }

    internal enum QueueCommandKind
    {
        Enqueue,
        DemandChanged,
        WorkerReady,
        Complete,
    }

    private readonly record struct QueueCommand(
        QueueCommandKind Kind,
        WorkItem? Item,
        IconLoadDiagnostics.SchedulerCommandMeasurement? Measurement,
        Exception? Failure)
    {
        public static QueueCommand Enqueue(WorkItem item) =>
            new(
                QueueCommandKind.Enqueue,
                item,
                IconLoadDiagnostics.BeginSchedulerCommand(QueueCommandKind.Enqueue),
                null);

        public static QueueCommand DemandChanged(WorkItem item) =>
            new(
                QueueCommandKind.DemandChanged,
                item,
                IconLoadDiagnostics.BeginSchedulerCommand(QueueCommandKind.DemandChanged),
                null);

        public static QueueCommand WorkerReady() =>
            new(
                QueueCommandKind.WorkerReady,
                null,
                IconLoadDiagnostics.BeginSchedulerCommand(QueueCommandKind.WorkerReady),
                null);

        public static QueueCommand Complete() =>
            new(
                QueueCommandKind.Complete,
                null,
                IconLoadDiagnostics.BeginSchedulerCommand(QueueCommandKind.Complete),
                null);

        public static QueueCommand Fault(Exception failure) =>
            new(
                QueueCommandKind.Complete,
                null,
                null,
                failure);
    }

    private sealed class WorkItem : IconLoadDemand.IObserver
    {
        private readonly IconLoadQueue _owner;
        private int _demandChangeQueued;
        private int _queueOwnershipReleased;
        private int _terminal;

        public WorkItem(IconLoadQueue owner, Operation operation, IconLoadDemand demand, IconLoadPriority priority)
        {
            _owner = owner;
            Operation = operation;
            Demand = demand;
            Priority = priority;
        }

        public Operation Operation { get; }

        public IconLoadDemand Demand { get; }

        public IconLoadPriority Priority { get; }

        public LinkedListNode<WorkItem>? Node { get; set; }

        public bool IsDemanded { get; set; }

        public void ReleaseQueueOwnership()
        {
            if (Interlocked.Exchange(ref _queueOwnershipReleased, 1) != 0)
            {
                return;
            }

            Demand.Unsubscribe(this);
            if (Priority == IconLoadPriority.High)
            {
                _owner.ReleaseHighPriority();
            }
        }

        public void MarkDispatched()
        {
            var wasTerminal = Interlocked.Exchange(ref _terminal, 1);
            Debug.Assert(wasTerminal == 0, "An icon load operation can be dispatched only once.");
        }

        public void Fail(Exception failure)
        {
            ReleaseQueueOwnership();
            if (Interlocked.Exchange(ref _terminal, 1) == 0)
            {
                Operation.Fail(failure);
            }
        }

        public void OnDemandChanged()
        {
            if (Interlocked.CompareExchange(ref _demandChangeQueued, 1, 0) == 0)
            {
                _owner.Publish(QueueCommand.DemandChanged(this));
            }
        }

        public void DemandChangeDequeued() => Volatile.Write(ref _demandChangeQueued, 0);
    }

    internal abstract class Operation
    {
        public virtual void Enqueued(IconLoadPriority priority, int workerCount)
        {
        }

        public abstract Task ExecuteAsync();

        public abstract void Fail(Exception failure);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1001:Types that own disposable fields should be disposable",
        Justification = "The coordinator closes the handle before completing; late lock-free publishers are ignored.")]
    private sealed class SingleConsumerSignal
    {
        private readonly AutoResetEvent _waitHandle = new(initialState: false);
        private IconLoadDiagnostics.SchedulerCommandMeasurement? _triggerMeasurement;
        private int _closed;
        private int _signaled;

        public void Pulse(IconLoadDiagnostics.SchedulerCommandMeasurement? measurement)
        {
            if (Volatile.Read(ref _closed) != 0)
            {
                return;
            }

            // Only the first publisher enters the kernel. Later publications coalesce
            // behind the already-signaled pass and remain visible in _commands.
            if (Interlocked.CompareExchange(ref _signaled, 1, 0) != 0)
            {
                return;
            }

            Volatile.Write(ref _triggerMeasurement, measurement);
            try
            {
                _waitHandle.Set();
            }
            catch (ObjectDisposedException) when (Volatile.Read(ref _closed) != 0)
            {
                // A worker or stale demand notification raced coordinator shutdown.
            }
        }

        public IconLoadDiagnostics.SchedulerWakeMeasurement? Wait()
        {
            _waitHandle.WaitOne();
            var triggerMeasurement = Interlocked.Exchange(ref _triggerMeasurement, null);

            // Reset before draining. A new publication either signals the following
            // pass or races early enough to be consumed by the pass about to begin.
            Volatile.Write(ref _signaled, 0);
            return triggerMeasurement?.CreateWakeMeasurement();
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0)
            {
                _waitHandle.Dispose();
            }
        }
    }
}
