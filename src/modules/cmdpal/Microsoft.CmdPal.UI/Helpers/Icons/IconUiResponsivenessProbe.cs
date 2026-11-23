// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Samples normal-priority dispatcher responsiveness without allowing probes to queue up.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The owning diagnostic session calls Stop, which cancels the loop and disposes the source after it exits. Avoid implementing a WinRT interface on this internal NativeAOT type.")]
internal sealed class IconUiResponsivenessProbe
{
    private const long NoPendingProbe = long.MinValue;

    private static readonly TimeSpan ProbeInterval = TimeSpan.FromMilliseconds(50);

    private readonly Func<DispatcherQueuePriority, DispatcherQueueHandler, bool> _tryEnqueue;
    private readonly DispatcherQueueHandler _probeCallback;
    private readonly IconLoadDiagnosticsSession _session;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _runTask;
    private int _active = 1;
    private long _pendingSince = NoPendingProbe;

    public IconUiResponsivenessProbe(
        DispatcherQueue dispatcherQueue,
        IconLoadDiagnosticsSession session)
        : this(dispatcherQueue.TryEnqueue, session, startTimer: true)
    {
    }

    internal IconUiResponsivenessProbe(
        Func<DispatcherQueuePriority, DispatcherQueueHandler, bool> tryEnqueue,
        IconLoadDiagnosticsSession session)
        : this(tryEnqueue, session, startTimer: false)
    {
    }

    private IconUiResponsivenessProbe(
        Func<DispatcherQueuePriority, DispatcherQueueHandler, bool> tryEnqueue,
        IconLoadDiagnosticsSession session,
        bool startTimer)
    {
        _tryEnqueue = tryEnqueue;
        _probeCallback = ProbeCallback;
        _session = session;
        _runTask = startTimer ? RunAsync() : Task.CompletedTask;
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _active, 0) == 0)
        {
            return;
        }

        try
        {
            _cancellation.Cancel();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to stop icon UI responsiveness probe", ex);
        }

        _ = _runTask.ContinueWith(
            static (_, state) => ((CancellationTokenSource)state!).Dispose(),
            _cancellation,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task RunAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(ProbeInterval);
            while (await timer.WaitForNextTickAsync(_cancellation.Token).ConfigureAwait(false))
            {
                OnTimerTick();
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError("Icon UI responsiveness probe failed", ex);
        }
    }

    internal void OnTimerTick()
    {
        if (Volatile.Read(ref _active) == 0)
        {
            return;
        }

        var enqueuedAt = Stopwatch.GetTimestamp();
        if (Interlocked.CompareExchange(ref _pendingSince, enqueuedAt, NoPendingProbe) != NoPendingProbe)
        {
            _session.RecordUiProbeSkipped();
            return;
        }

        _session.RecordUiProbeEnqueued();
        if (!_tryEnqueue(
                DispatcherQueuePriority.Normal,
                _probeCallback))
        {
            var rejectedAt = Interlocked.Exchange(ref _pendingSince, NoPendingProbe);
            Debug.Assert(rejectedAt == enqueuedAt, "A rejected probe cannot have run its callback.");
            _session.RecordUiProbeRejected();
        }
    }

    private void ProbeCallback()
    {
        var completedAt = Stopwatch.GetTimestamp();
        var enqueuedAt = Interlocked.Exchange(ref _pendingSince, NoPendingProbe);
        if (Volatile.Read(ref _active) != 0 && enqueuedAt != NoPendingProbe)
        {
            _session.RecordUiProbeCompleted(completedAt - enqueuedAt);
        }
    }
}
