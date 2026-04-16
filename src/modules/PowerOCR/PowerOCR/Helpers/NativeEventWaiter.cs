// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace PowerOCR.Helpers
{
    public static class NativeEventWaiter
    {
        public static void WaitForEventLoop(string eventName, Action callback, CancellationToken cancellationToken)
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            var t = new Thread(() =>
            {
                try
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (eventHandle.WaitOne(500))
                        {
                            dispatcherQueue?.TryEnqueue(() =>
                            {
                                try
                                {
                                    callback();
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"NativeEventWaiter callback exception for {eventName}: {ex.Message}");
                                }
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"NativeEventWaiter exception for {eventName}: {ex.Message}");
                }
            });

            t.IsBackground = true;
            t.Name = $"NativeEventWaiter_{eventName}";
            t.Start();
        }
    }
}
