// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using Microsoft.UI.Dispatching;

namespace Microsoft.CmdPal.Common.Helpers;

public static class NativeEventWaiter
{
    public static void WaitForEventLoop(string eventName, Action callback)
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        var t = new Thread(() =>
        {
            var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            while (true)
            {
                if (eventHandle.WaitOne())
                {
                    dispatcherQueue.TryEnqueue(() => callback());
                }
            }
        });

        t.IsBackground = true;
        t.Start();
    }
}
