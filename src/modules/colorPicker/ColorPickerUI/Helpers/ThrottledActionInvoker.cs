// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Windows.Threading;

namespace ColorPicker.Helpers
{
    [Export(typeof(IThrottledActionInvoker))]
    public sealed class ThrottledActionInvoker : IThrottledActionInvoker
    {
        private object _invokerLock = new object();
        private Action _actionToRun;

        private DispatcherTimer _timer;

        public ThrottledActionInvoker()
        {
            _timer = new DispatcherTimer();
            _timer.Tick += Timer_Tick;
        }

        public void ScheduleAction(Action action, int milliseconds)
        {
            lock (_invokerLock)
            {
                if (_timer.IsEnabled)
                {
                    _timer.Stop();
                }

                _actionToRun = action;
                _timer.Interval = new TimeSpan(0, 0, 0, 0, milliseconds);

                _timer.Start();
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            lock (_invokerLock)
            {
                _timer.Stop();
                _actionToRun.Invoke();
            }
        }
    }
}
