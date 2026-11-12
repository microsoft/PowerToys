// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace Common.UI
{
    public static class NativeEventWaiter
    {
        /// <summary>Posts <paramref name="callback"/> to the given context each time the named event is signaled, until canceled.</summary>
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
