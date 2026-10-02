// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace Common.UI
{
    public static class NativeEventWaiter
    {
        /// <summary>
        /// Starts a thread that waits on the auto-reset named event <paramref name="eventName"/> and, each time it is
        /// signaled, posts <paramref name="callback"/> to <paramref name="synchronizationContext"/>, until
        /// <paramref name="cancel"/> is canceled. Pass the UI thread's context to run the callback on the UI thread,
        /// e.g. a DispatcherSynchronizationContext (WPF), WindowsFormsSynchronizationContext (WinForms) or
        /// DispatcherQueueSynchronizationContext (WinUI).
        /// </summary>
        public static void WaitForEventLoop(string eventName, Action callback, SynchronizationContext synchronizationContext, CancellationToken cancel)
        {
            ArgumentNullException.ThrowIfNull(callback);
            ArgumentNullException.ThrowIfNull(synchronizationContext);

            new Thread(() =>
            {
                var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                while (true)
                {
                    if (WaitHandle.WaitAny(new WaitHandle[] { cancel.WaitHandle, eventHandle }) == 1)
                    {
                        synchronizationContext.Post(_ => callback(), null);
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
