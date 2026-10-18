// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.UI.Dispatching;

namespace ColorPicker.Helpers
{
    // MEF [Export] removed; registered in AppServices.Register.
    public sealed class ThrottledActionInvoker : IThrottledActionInvoker
    {
        private readonly DispatcherQueueTimer _timer;

        private Lock _invokerLock = new Lock();
        private Action _actionToRun;

        public ThrottledActionInvoker()
        {
            // Must be constructed on the UI thread so it binds to the app's DispatcherQueue.
            DispatcherQueue queue;
            try
            {
                queue = DispatcherQueue.GetForCurrentThread();
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException(
                    "ThrottledActionInvoker must be created on a thread with a DispatcherQueue (the UI thread).",
                    ex);
            }

            if (queue == null)
            {
                throw new InvalidOperationException("ThrottledActionInvoker must be created on a thread with a DispatcherQueue (the UI thread).");
            }

            _timer = queue.CreateTimer();
            _timer.IsRepeating = false; // one-shot debounce: DispatcherQueueTimer repeats by default.
            _timer.Tick += Timer_Tick;
        }

        public void ScheduleAction(Action action, int milliseconds)
        {
            lock (_invokerLock)
            {
                if (_timer.IsRunning)
                {
                    _timer.Stop();
                }

                _actionToRun = action;
                _timer.Interval = TimeSpan.FromMilliseconds(milliseconds);

                _timer.Start();
            }
        }

        private void Timer_Tick(DispatcherQueueTimer sender, object e)
        {
            lock (_invokerLock)
            {
                _timer.Stop();

                // Capture and clear the field before invoking so this process-lifetime singleton does
                // not pin the last-scheduled closure and its captured target alive
                // until the next ScheduleAction call.
                var action = _actionToRun;
                _actionToRun = null;
                action?.Invoke();
            }
        }
    }
}
