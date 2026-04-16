// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PowerOCR.Helpers;

public sealed class ThrottledActionInvoker : IThrottledActionInvoker
{
    private Lock _invokerLock = new Lock();
    private Action? _actionToRun;

    private DispatcherQueueTimer? _timer;

    public ThrottledActionInvoker()
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue != null)
        {
            _timer = dispatcherQueue.CreateTimer();
            _timer.Tick += Timer_Tick;
            _timer.IsRepeating = false;
        }
    }

    public void ScheduleAction(Action action, int milliseconds)
    {
        lock (_invokerLock)
        {
            if (_timer == null)
            {
                return;
            }

            if (_timer.IsRunning)
            {
                _timer.Stop();
            }

            _actionToRun = action;
            _timer.Interval = TimeSpan.FromMilliseconds(milliseconds);

            _timer.Start();
        }
    }

    private void Timer_Tick(DispatcherQueueTimer sender, object args)
    {
        lock (_invokerLock)
        {
            _timer?.Stop();
            _actionToRun?.Invoke();
        }
    }
}
