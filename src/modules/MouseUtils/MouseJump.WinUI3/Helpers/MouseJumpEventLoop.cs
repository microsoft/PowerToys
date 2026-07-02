// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

using Microsoft.UI.Dispatching;

namespace MouseJump.WinUI3.Helpers;

internal static class MouseJumpEventLoop
{
    /// <summary>
    /// Based on NativeEventWaiter.WaitForEventLoop.
    /// </summary>
    public static void RunEventHandler(string eventName, Action callback, DispatcherQueue dispatcherQueue, CancellationToken cancel)
    {
        Logger.LogDebug($"starting event handler for event '{eventName}'");
        var thread = new Thread(() =>
        {
            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            var waitHandles = new WaitHandle[] { cancel.WaitHandle, eventHandle };
            while (true)
            {
                Logger.LogDebug($"[{eventName}] - entering event handler loop");
                if (WaitHandle.WaitAny(waitHandles) == 1)
                {
                    try
                    {
                        Logger.LogDebug($"[{eventName}] - invoking callback");
                        var tcs = new TaskCompletionSource();
                        dispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                callback();
                                tcs.SetResult();
                            }
                            catch (Exception ex)
                            {
                                tcs.SetException(ex);
                            }
                        });
                        tcs.Task.GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogDebug($"[{eventName}] - error occurred");
                        Logger.LogError(ex.ToString());
                        throw;
                    }
                }
                else
                {
                    Logger.LogDebug($"[{eventName}] - exiting event handler loop");
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
    public static void RunAsyncEventHandler(string eventName, Func<Task> callback, DispatcherQueue dispatcherQueue, CancellationToken cancel)
    {
        Logger.LogDebug($"starting event handler for event '{eventName}'");
        var thread = new Thread(() =>
        {
            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            var waitHandles = new WaitHandle[] { cancel.WaitHandle, eventHandle };
            while (true)
            {
                Logger.LogDebug($"[{eventName}] - entering event handler loop");
                if (WaitHandle.WaitAny(waitHandles) == 1)
                {
                    try
                    {
                        Logger.LogDebug($"[{eventName}] - invoking callback");
                        var tcs = new TaskCompletionSource();
                        dispatcherQueue.TryEnqueue(() =>
                        {
                            callback().ContinueWith(
                                t =>
                                {
                                    if (t.IsFaulted)
                                    {
                                        tcs.SetException(t.Exception!.InnerExceptions);
                                    }
                                    else if (t.IsCanceled)
                                    {
                                        tcs.SetCanceled();
                                    }
                                    else
                                    {
                                        tcs.SetResult();
                                    }
                                },
                                TaskScheduler.Default);
                        });
                        tcs.Task.GetAwaiter().GetResult(); // block until callback completes
                    }
                    catch (Exception ex)
                    {
                        Logger.LogDebug($"[{eventName}] - error occurred");
                        Logger.LogError(ex.ToString());
                        throw;
                    }
                }
                else
                {
                    Logger.LogDebug($"[{eventName}] - exiting event handler loop");
                    return;
                }
            }
        });

        thread.Name = "MouseJumpEventLoopThread";
        thread.IsBackground = true;

        thread.Start();
    }
}
