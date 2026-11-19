// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

using Microsoft.UI.Dispatching;

namespace MouseJump.WinUI3.Helpers;

internal static partial class MouseJumpEventLoop
{
    /// <summary>
    /// Based on NativeEventWaiter.WaitForEventLoop.
    /// </summary>
    public static void RunEventHandler(string eventName, Action callback, DispatcherQueue dispatcherQueue, CancellationToken cancel)
    {
        Logger.LogDebug($"starting event handler for event '{eventName}'");

        // runs on the ui thread - invokes the callback and records its outcome on tcs
        static void InvokeCallback(Action callback, TaskCompletionSource tcs)
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
        }

        // queues the callback on the dispatcher (ui thread) and blocks until it finishes.
        // DispatcherQueue.TryEnqueue can only tell us whether the work was *queued*, not
        // when it finishes, so we complete this one by hand instead - either by
        // InvokeCallback, or straight away if it can't even be started
        static void InvokeOnDispatcherAndWait(string eventName, Action callback, DispatcherQueue dispatcherQueue)
        {
            try
            {
                Logger.LogDebug($"[{eventName}] - invoking callback");
                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                if (!dispatcherQueue.TryEnqueue(() => InvokeCallback(callback, tcs)))
                {
                    // the dispatcher is shutting down, so InvokeCallback will never run
                    tcs.SetException(new InvalidOperationException("DispatcherQueue is not accepting work items."));
                }

                tcs.Task.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // if something went wrong the application could be in an unstable state.
                // the main MouseJump module will restart the background process the next
                // time the hotkey is invoked, so just rethrow the exception here.
                Logger.LogDebug($"[{eventName}] - error occurred");
                Logger.LogError(ex.ToString());
                throw;
            }
        }

        // runs on the event loop thread - invokes the callback every time the specified
        // event is signalled, until cancel is signalled
        static void RunLoop(string eventName, Action callback, DispatcherQueue dispatcherQueue, CancellationToken cancel)
        {
            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            var waitHandles = new WaitHandle[] { cancel.WaitHandle, eventHandle };
            while (true)
            {
                Logger.LogDebug($"[{eventName}] - entering event handler loop");
                if (WaitHandle.WaitAny(waitHandles) != 1)
                {
                    Logger.LogDebug($"[{eventName}] - exiting event handler loop");
                    return;
                }

                InvokeOnDispatcherAndWait(eventName, callback, dispatcherQueue);
            }
        }

        var thread = new Thread(
            () => RunLoop(eventName, callback, dispatcherQueue, cancel));
        thread.Name = "MouseJumpEventLoopThread";
        thread.IsBackground = true;

        thread.Start();
    }
}
