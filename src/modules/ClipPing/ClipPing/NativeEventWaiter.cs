// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using Microsoft.UI.Dispatching;

namespace ClipPing;

public static class NativeEventWaiter
{
    public static void WaitForEvents(params (string EventName, Action Callback)[] events)
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        var t = new Thread(() =>
        {
            var eventHandles = new WaitHandle[events.Length];

            for (int i = 0; i < events.Length; i++)
            {
                var (eventName, _) = events[i];
                eventHandles[i] = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            }

            while (true)
            {
                var index = WaitHandle.WaitAny(eventHandles);
                dispatcherQueue.TryEnqueue(() => events[index].Callback());
            }
        });

        t.IsBackground = true;
        t.Start();
    }
}
