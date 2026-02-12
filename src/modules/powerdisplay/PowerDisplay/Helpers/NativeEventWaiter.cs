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
            Logger.LogTrace($"[NativeEventWaiter] Setting up event loop for event: {eventName}");
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            if (dispatcherQueue == null)
            {
                Logger.LogError($"[NativeEventWaiter] DispatcherQueue is null for event: {eventName}");
                return;
            }

            Logger.LogTrace($"[NativeEventWaiter] DispatcherQueue obtained for event: {eventName}");

            var t = new Thread(() =>
            {
                Logger.LogInfo($"[NativeEventWaiter] Background thread started for event: {eventName}");
                try
                {
                    Logger.LogTrace($"[NativeEventWaiter] Creating EventWaitHandle for event: {eventName}");
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                    Logger.LogInfo($"[NativeEventWaiter] EventWaitHandle created successfully for event: {eventName}");

                    int waitCount = 0;
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // Use 500ms timeout for polling
                        if (eventHandle.WaitOne(500))
                        {
                            waitCount++;
                            Logger.LogInfo($"[NativeEventWaiter] Event SIGNALED: {eventName} (signal count: {waitCount})");
                            bool enqueued = dispatcherQueue.TryEnqueue(() =>
                            {
                                Logger.LogTrace($"[NativeEventWaiter] Executing callback on UI thread for event: {eventName}");
                                try
                                {
                                    callback();
                                    Logger.LogTrace($"[NativeEventWaiter] Callback completed for event: {eventName}");
                                }
                                catch (Exception callbackEx)
                                {
                                    Logger.LogError($"[NativeEventWaiter] Callback exception for event {eventName}: {callbackEx.Message}");
                                }
                            });

                            if (!enqueued)
                            {
                                Logger.LogError($"[NativeEventWaiter] Failed to enqueue callback to UI thread for event: {eventName}");
                            }
                        }
                    }

                    Logger.LogInfo($"[NativeEventWaiter] Event loop ending for event: {eventName} (cancellation requested)");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[NativeEventWaiter] Exception in event loop for {eventName}: {ex.Message}\n{ex.StackTrace}");
                }
            });

            t.IsBackground = true;
            t.Name = $"NativeEventWaiter_{eventName}";
            t.Start();
            Logger.LogTrace($"[NativeEventWaiter] Background thread started with name: {t.Name}");
        }
    }
}
