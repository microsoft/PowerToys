// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace PowerOCR.Helpers;

public sealed class ThrottledActionInvoker : IThrottledActionInvoker, IDisposable
{
    private readonly Lock _invokerLock = new();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _timer;
    private Action? _actionToRun;

    public ThrottledActionInvoker(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        _timer = _dispatcherQueue.CreateTimer();
        _timer.IsRepeating = false;
        _timer.Tick += Timer_Tick;
    }

    public void ScheduleAction(Action action, int milliseconds)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            ScheduleOnDispatcherThread(action, milliseconds);
        }
        else
        {
            if (!_dispatcherQueue.TryEnqueue(() => ScheduleOnDispatcherThread(action, milliseconds)))
            {
                Logger.LogError("Failed to enqueue the Text Extractor settings debounce request.");
            }
        }
    }

    private void ScheduleOnDispatcherThread(Action action, int milliseconds)
    {
        lock (_invokerLock)
        {
            _timer.Stop();
            _actionToRun = action;
            _timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
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

    public void Dispose()
    {
        _timer.Stop();
    }
}
