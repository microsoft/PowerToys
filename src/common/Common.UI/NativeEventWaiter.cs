// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using Dispatcher = System.Windows.Threading.Dispatcher;

namespace Common.UI
{
    public static class NativeEventWaiter
    {
        public static void WaitForEventLoop(string eventName, Action callback, Dispatcher dispatcher, CancellationToken cancel)
        {
            new Thread(() =>
            {
                var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                while (true)
                {
                    if (WaitHandle.WaitAny(new WaitHandle[] { cancel.WaitHandle, eventHandle }) == 1)
                    {
                        dispatcher.BeginInvoke(callback);
                    }
                    else
                    {
                        return;
                    }
                }
            }).Start();
        }
    }
}
