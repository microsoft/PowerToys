// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.UI.Dispatching;

namespace AdvancedPaste.Helpers
{
    public static class NativeEventWaiter
    {
        public static void WaitForEventLoop(string eventName, Action callback)
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            new Thread(() =>
            {
                var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                while (true)
                {
                    if (eventHandle.WaitOne())
                    {
                        dispatcherQueue.TryEnqueue(() => callback());
                    }
                }
            }).Start();
        }
    }
}
