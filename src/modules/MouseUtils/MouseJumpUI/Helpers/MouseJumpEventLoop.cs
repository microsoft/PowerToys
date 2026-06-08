// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

using ManagedCommon;

namespace MouseJumpUI.Helpers;

internal static class MouseJumpEventLoop
{
    /// <summary>
    /// Based on NativeEventWaiter.WaitForEventLoop.
    /// </summary>
    public static void RunEventHandler(string eventName, Action callback, Dispatcher dispatcher, CancellationToken cancel)
    {
        var thread = new Thread(() =>
        {
            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            var waitHandles = new WaitHandle[] { cancel.WaitHandle, eventHandle };
            while (true)
            {
                if (WaitHandle.WaitAny(waitHandles) == 1)
                {
                    try
                    {
                        dispatcher.Invoke(callback);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                        throw;
                    }
                }
                else
                {
                    return;
                }
            }
        });

        thread.Name = "MouseJumpEventLoopThread";
        thread.IsBackground = true;

        thread.Start();
    }

    /// <summary>
         /// Based on NativeEventWaiter.WaitForEventLoop,
         /// but takes an async callback.
         /// </summary>
    public static void RunEventHandler(string eventName, Func<Task> callback, Dispatcher dispatcher, CancellationToken cancel)
    {
        var thread = new Thread(() =>
        {
            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            var waitHandles = new WaitHandle[] { cancel.WaitHandle, eventHandle };
            while (true)
            {
                if (WaitHandle.WaitAny(waitHandles) == 1)
                {
                    try
                    {
                        dispatcher.InvokeAsync(callback) // DispatcherOperation<Task>
                            .Task // Task<Task>
                            .Unwrap() // Task (the actual async work)
                            .GetAwaiter()
                            .GetResult(); // block until callback completes
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex.ToString());
                        throw;
                    }
                }
                else
                {
                    return;
                }
            }
        });

        thread.Name = "MouseJumpEventLoopThread";
        thread.IsBackground = true;

        thread.Start();
    }
}
