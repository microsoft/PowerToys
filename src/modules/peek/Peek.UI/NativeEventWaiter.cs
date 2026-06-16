// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace Peek.UI.Native
{
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
                    try
                    {
                        if (eventHandle.WaitOne())
                        {
                            dispatcherQueue.TryEnqueue(() => callback());
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"NativeEventWaiter error for {eventName}: {ex.Message}");
                        break;
                    }
                }
            });

            t.IsBackground = true;
            t.Start();
        }
    }
}
