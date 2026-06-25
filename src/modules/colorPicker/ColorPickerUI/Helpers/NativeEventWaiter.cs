// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace ColorPicker.Helpers
{
    /// <summary>
    /// WinUI 3 replacement for <c>Common.UI.NativeEventWaiter</c>. Waits on a Windows
    /// named <see cref="EventWaitHandle"/> on a background thread and marshals the
    /// callback onto the captured UI-thread <see cref="DispatcherQueue"/> via
    /// <see cref="DispatcherQueue.TryEnqueue(DispatcherQueueHandler)"/>.
    /// </summary>
    /// <remarks>
    /// The Common.UI original uses <c>System.Windows.Threading.Dispatcher.BeginInvoke</c>,
    /// which has no WinUI 3 equivalent. This copy keeps the original
    /// <see cref="WaitHandle.WaitAny(WaitHandle[])"/> shape so the worker wakes the instant
    /// <paramref name="cancel"/> is signalled (ColorPicker's ExitToken is cancelled on runner
    /// exit / shutdown), rather than polling.
    /// </remarks>
    public static class NativeEventWaiter
    {
        /// <summary>
        /// Spawns a background worker that invokes <paramref name="callback"/> on the current
        /// thread's <see cref="DispatcherQueue"/> whenever the named event fires, and exits when
        /// <paramref name="cancel"/> is signalled. MUST be called from a thread that owns a
        /// <see cref="DispatcherQueue"/> (the WinUI UI thread); otherwise the registration is skipped.
        /// </summary>
        public static void WaitForEventLoop(string eventName, Action callback, CancellationToken cancel)
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            if (dispatcherQueue == null)
            {
                Logger.LogError($"[NativeEventWaiter] No DispatcherQueue on the calling thread for event: {eventName}. Call from the UI thread.");
                return;
            }

            var thread = new Thread(() =>
            {
                try
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                    var handles = new WaitHandle[] { cancel.WaitHandle, eventHandle };
                    while (true)
                    {
                        // Index 0 == cancel (exit immediately); index 1 == event signalled.
                        if (WaitHandle.WaitAny(handles) == 1)
                        {
                            dispatcherQueue.TryEnqueue(() =>
                            {
                                try
                                {
                                    callback();
                                }
                                catch (Exception callbackEx)
                                {
                                    Logger.LogError($"[NativeEventWaiter] Callback failed for event {eventName}: {callbackEx.Message}");
                                }
                            });
                        }
                        else
                        {
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[NativeEventWaiter] Event loop failed for {eventName}: {ex.Message}");
                }
            })
            {
                IsBackground = true,
                Name = $"NativeEventWaiter_{eventName}",
            };

            thread.Start();
        }
    }
}
