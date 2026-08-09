// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using Microsoft.UI.Dispatching;

namespace MeasureToolUI
{
    internal static class NativeEventWaiter
    {
        internal static Thread WaitForEventLoop(
            string eventName,
            Action callback,
            DispatcherQueue dispatcherQueue,
            CancellationToken cancellationToken)
        {
            var thread = new Thread(() =>
            {
                using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
                var waitHandles = new[] { cancellationToken.WaitHandle, eventHandle };
                while (WaitHandle.WaitAny(waitHandles) == 1)
                {
                    dispatcherQueue.TryEnqueue(() => callback());
                }
            })
            {
                IsBackground = true,
                Name = $"ScreenRuler_{eventName}",
            };
            thread.Start();
            return thread;
        }
    }
}
