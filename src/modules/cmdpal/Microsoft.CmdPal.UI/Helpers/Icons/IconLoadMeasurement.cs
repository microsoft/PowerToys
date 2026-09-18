// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed class IconLoadMeasurement
{
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

    internal IconLoadDiagnosticsSession Session { get; }

    internal long Id { get; }

    internal IconLoadInputKind InputKind { get; }

    internal IconLoadMeasurement(IconLoadDiagnosticsSession session, long id, IconLoadInputKind inputKind)
    {
        Session = session;
        Id = id;
        InputKind = inputKind;
    }

    public void Enqueued(IconLoadPriority priority)
    {
        _queuePriority = (int)priority;
        _enqueuedAt = Stopwatch.GetTimestamp();
        try
        {
            Session.RecordLoadEnqueued(Id, priority);
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

    public long BeginDispatcherWait() => Stopwatch.GetTimestamp();

    public long DispatcherStarted(long enqueuedAt)
    {
        var now = Stopwatch.GetTimestamp();
        Session.RecordDispatcherWait(Id, InputKind, now - enqueuedAt);
        return now;
    }

    public void DispatcherCompleted(long startedAt)
    {
        Session.RecordDispatcherWork(Id, InputKind, Stopwatch.GetTimestamp() - startedAt);
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
            if (enqueuedAt != 0 && Volatile.Read(ref _started) != 0)
            {
                Session.RecordLoadCompleted(Id, InputKind, IconLoadResultKind.Failed, Stopwatch.GetTimestamp() - enqueuedAt);
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
