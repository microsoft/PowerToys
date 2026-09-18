// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class IconLoadMeasurement
{
    private const int DispatcherWaitingState = 1;
    private const int DispatcherCallbackState = 2;
    private const int DispatcherCompletedState = 3;

    private enum EnqueueState
    {
        Pending,
        Enqueued,
        Rejected,
        Faulted,
    }

    private readonly long _createdAt = Stopwatch.GetTimestamp();
    private long _enqueuedAt;
    private int _queuePriority;
    private int _enqueueState;
    private int _started;
    private int _completed;
    private int _resultKind;
    private TaskCompletionSource<bool>? _enqueueWaiter;
    private int _dispatcherState;
    private int _dispatcherMaterializationKind;

    internal IconLoadDiagnosticsSession Session { get; }

    internal long Id { get; }

    internal IconLoadInputKind InputKind { get; }

    internal IconLoadMeasurement(IconLoadDiagnosticsSession session, long id, IconLoadInputKind inputKind)
    {
        Session = session;
        Id = id;
        InputKind = inputKind;
    }

    public void Enqueued(IconLoadPriority priority, int workerCount = 1)
    {
        _queuePriority = (int)priority;
        _enqueuedAt = Stopwatch.GetTimestamp();
        try
        {
            Session.RecordLoadEnqueued(Id, priority, Math.Max(1, workerCount));
            var published = PublishEnqueueState(EnqueueState.Enqueued);
            Debug.Assert(published, "A load can only be enqueued once.");
        }
        catch
        {
            PublishEnqueueState(EnqueueState.Faulted);
            throw;
        }
    }

    public void RegisterTask(Task<IconSource?> task)
    {
        Session.RegisterLoad(task, this);
    }

    public void Rejected()
    {
        if (!PublishEnqueueState(EnqueueState.Rejected))
        {
            return;
        }

        if (Interlocked.Exchange(ref _completed, 1) == 0)
        {
            Session.RecordLoadRejected(Id);
        }
    }

    public async ValueTask<bool> WorkerStartingAsync(int workerCount = 1)
    {
        if (!await WaitForEnqueueAsync().ConfigureAwait(false))
        {
            return false;
        }

        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return false;
        }

        var now = Stopwatch.GetTimestamp();
        Session.RecordWorkerStarted(Id, InputKind, (IconLoadPriority)_queuePriority, now - _enqueuedAt, workerCount);
        return true;
    }

    public long BeginBackgroundPreparation() => Stopwatch.GetTimestamp();

    public void CompleteBackgroundPreparation(long startedAt)
    {
        Session.RecordBackgroundPreparation(Id, InputKind, Stopwatch.GetTimestamp() - startedAt);
    }

    public long BeginDispatcherWait(
        IconDispatcherMaterializationKind materializationKind = IconDispatcherMaterializationKind.Unknown)
    {
        var now = Stopwatch.GetTimestamp();
        Volatile.Write(ref _dispatcherMaterializationKind, (int)materializationKind);
        if (Interlocked.CompareExchange(ref _dispatcherState, DispatcherWaitingState, 0) == 0)
        {
            Session.RecordDispatcherEnqueued(Id, InputKind, materializationKind, Session.IsLoadDemanded(Id));
        }

        return now;
    }

    public long DispatcherStarted(long enqueuedAt)
    {
        var now = Stopwatch.GetTimestamp();
        if (Interlocked.CompareExchange(
                ref _dispatcherState,
                DispatcherCallbackState,
                DispatcherWaitingState) == DispatcherWaitingState)
        {
            Session.RecordDispatcherWait(
                Id,
                InputKind,
                (IconDispatcherMaterializationKind)Volatile.Read(ref _dispatcherMaterializationKind),
                Session.IsLoadDemanded(Id),
                enqueuedAt,
                now - enqueuedAt);
        }

        // Start callback-wall and UI-slice timing after recording the queue-wait
        // sample so diagnostics bookkeeping is not attributed to materialization.
        return Stopwatch.GetTimestamp();
    }

    public long DispatcherUiSliceCompleted(long startedAt, IconDispatcherUiSliceKind sliceKind)
    {
        var now = Stopwatch.GetTimestamp();
        Session.RecordDispatcherUiSlice(
            Id,
            InputKind,
            (IconDispatcherMaterializationKind)Volatile.Read(ref _dispatcherMaterializationKind),
            sliceKind,
            Session.IsLoadDemanded(Id),
            startedAt,
            now - startedAt);
        return Stopwatch.GetTimestamp();
    }

    public long DispatcherAsyncSuspensionCompleted(long startedAt)
    {
        var now = Stopwatch.GetTimestamp();
        Session.RecordDispatcherAsyncSuspension(
            Id,
            InputKind,
            (IconDispatcherMaterializationKind)Volatile.Read(ref _dispatcherMaterializationKind),
            Session.IsLoadDemanded(Id),
            startedAt,
            now - startedAt);
        return Stopwatch.GetTimestamp();
    }

    public void DispatcherCompleted(long startedAt)
    {
        var now = Stopwatch.GetTimestamp();
        if (Interlocked.CompareExchange(
                ref _dispatcherState,
                DispatcherCompletedState,
                DispatcherCallbackState) == DispatcherCallbackState)
        {
            Session.RecordDispatcherWork(
                Id,
                InputKind,
                (IconDispatcherMaterializationKind)Volatile.Read(ref _dispatcherMaterializationKind),
                Session.IsLoadDemanded(Id),
                startedAt,
                now - startedAt);
        }
    }

    public void DispatcherWaitFailed(long enqueuedAt)
    {
        var now = Stopwatch.GetTimestamp();
        if (Interlocked.CompareExchange(
                ref _dispatcherState,
                DispatcherCompletedState,
                DispatcherWaitingState) == DispatcherWaitingState)
        {
            Session.RecordDispatcherWaitFailed(
                Id,
                InputKind,
                (IconDispatcherMaterializationKind)Volatile.Read(ref _dispatcherMaterializationKind),
                Session.IsLoadDemanded(Id),
                enqueuedAt,
                now - enqueuedAt);
        }
    }

    public void SetResult(IconSource? result)
    {
        _resultKind = (int)IconLoadDiagnostics.ClassifyResult(result);
    }

    public void CompleteDirectGlyph(IconSource? result)
    {
        SetResult(result);
        if (Interlocked.Exchange(ref _completed, 1) == 0)
        {
            Session.RecordDirectGlyphCompleted(
                Id,
                InputKind,
                (IconLoadResultKind)_resultKind,
                Stopwatch.GetTimestamp() - _createdAt);
        }
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 0)
        {
            var enqueuedAt = Volatile.Read(ref _enqueuedAt);
            if (enqueuedAt != 0 && Volatile.Read(ref _started) != 0)
            {
                Session.RecordLoadCompleted(Id, InputKind, (IconLoadResultKind)_resultKind, Stopwatch.GetTimestamp() - enqueuedAt);
            }
        }
    }

    public void Fail()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 0)
        {
            var enqueuedAt = Volatile.Read(ref _enqueuedAt);
            if (enqueuedAt == 0)
            {
                return;
            }

            if (Volatile.Read(ref _started) != 0)
            {
                Session.RecordLoadCompleted(Id, InputKind, IconLoadResultKind.Failed, Stopwatch.GetTimestamp() - enqueuedAt);
            }
            else
            {
                Session.RecordLoadAbandoned(Id, (IconLoadPriority)_queuePriority);
            }
        }
    }

    private ValueTask<bool> WaitForEnqueueAsync()
    {
        var state = (EnqueueState)Volatile.Read(ref _enqueueState);
        if (state != EnqueueState.Pending)
        {
            return new ValueTask<bool>(state == EnqueueState.Enqueued);
        }

        var waiter = Volatile.Read(ref _enqueueWaiter);
        if (waiter is null)
        {
            var newWaiter = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            waiter = Interlocked.CompareExchange(ref _enqueueWaiter, newWaiter, null) ?? newWaiter;
        }

        state = (EnqueueState)Volatile.Read(ref _enqueueState);
        if (state != EnqueueState.Pending)
        {
            waiter.TrySetResult(state == EnqueueState.Enqueued);
        }

        return new ValueTask<bool>(waiter.Task);
    }

    private bool PublishEnqueueState(EnqueueState state)
    {
        var previousState = (EnqueueState)Interlocked.CompareExchange(
            ref _enqueueState,
            (int)state,
            (int)EnqueueState.Pending);
        var publishedState = previousState == EnqueueState.Pending ? state : previousState;
        Volatile.Read(ref _enqueueWaiter)?.TrySetResult(publishedState == EnqueueState.Enqueued);
        return previousState == EnqueueState.Pending;
    }
}
