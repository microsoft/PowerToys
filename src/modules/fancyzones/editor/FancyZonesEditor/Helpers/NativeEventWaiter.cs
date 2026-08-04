// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace FancyZonesEditor.Helpers
{
    /// <summary>
    /// Waits on a Windows named event in a background thread and marshals the callback back to
    /// the UI thread. Replaces <c>Common.UI.NativeEventWaiter</c>, which is bound to the WPF
    /// <c>Dispatcher</c>.
    /// </summary>
    public static class NativeEventWaiter
    {
        /// <summary>
        /// Waits for a Windows event in a background thread and invokes <paramref name="callback"/>
        /// on the thread owning <paramref name="dispatcherQueue"/> when it is signaled.
        /// </summary>
        /// <param name="eventName">Name of the Windows event to wait for.</param>
        /// <param name="callback">Callback to invoke when the event is signaled.</param>
        /// <param name="dispatcherQueue">Dispatcher queue of the UI thread.</param>
        /// <param name="cancellationToken">Token that ends the wait loop.</param>
        public static void WaitForEventLoop(string eventName, Action callback, DispatcherQueue dispatcherQueue, CancellationToken cancellationToken)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (WaitHandle.WaitAny(new WaitHandle[] { cancellationToken.WaitHandle, eventHandle }) == 1)
                        {
                            dispatcherQueue.TryEnqueue(() => callback());
                        }
                        else
                        {
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed waiting on event {eventName}", ex);
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
