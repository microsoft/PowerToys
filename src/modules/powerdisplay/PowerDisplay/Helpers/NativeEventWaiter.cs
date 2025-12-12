// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Helper class for waiting on Windows Named Events (Awake pattern)
    /// Based on Peek.UI implementation
    /// </summary>
    public static class NativeEventWaiter
    {
        /// <summary>
        /// Wait for a Windows Event in a background thread and invoke callback on UI thread when signaled
        /// </summary>
        /// <param name="eventName">Name of the Windows Event to wait for</param>
        /// <param name="callback">Callback to invoke when event is signaled</param>
        /// <param name="cancellationToken">Token to cancel the wait loop</param>
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
                        // Use infinite wait like Peek.UI for more reliable event reception
                        if (eventHandle.WaitOne(500))
                        {
                            dispatcherQueue.TryEnqueue(() => callback());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[NativeEventWaiter] Exception in event loop for {eventName}: {ex.Message}");
                }
            });

            t.IsBackground = true;
            t.Start();
        }
    }
}
