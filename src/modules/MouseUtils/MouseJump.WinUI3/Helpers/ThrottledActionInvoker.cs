// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using Microsoft.UI.Dispatching;

namespace MouseJump.WinUI3.Helpers;

internal sealed class ThrottledActionInvoker
{
    private readonly Lock _invokerLock = new();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _timer;

    private Action? _actionToRun;

    public ThrottledActionInvoker()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException($"{nameof(ThrottledActionInvoker)} must be created on a thread with a DispatcherQueue.");
        _timer = _dispatcherQueue.CreateTimer();
        _timer.Tick += Timer_Tick;
    }

    public void ScheduleAction(Action action, int milliseconds)
    {
        // File watcher callbacks run on thread pool threads, but the timer must be used on its own thread.
        if (!_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => ScheduleAction(action, milliseconds));
            return;
        }

        lock (_invokerLock)
        {
            if (_timer.IsRunning)
            {
                _timer.Stop();
            }

            _actionToRun = action;
            _timer.Interval = new TimeSpan(0, 0, 0, 0, milliseconds);

            _timer.Start();
        }
    }

    private void Timer_Tick(DispatcherQueueTimer sender, object args)
    {
        lock (_invokerLock)
        {
            _timer.Stop();
            _actionToRun?.Invoke();
        }
    }
}
