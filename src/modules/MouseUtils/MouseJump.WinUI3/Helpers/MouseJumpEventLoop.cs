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
                        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        if (!dispatcherQueue.TryEnqueue(() =>
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
                        }))
                        {
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
    /// <remarks>
    /// Each invocation of <paramref name="callback"/> gets its own cancellation token (linked to
    /// <paramref name="cancel"/>), and the loop keeps listening for the event while it runs. If
    /// the event is signalled again before the callback completes (e.g. the user presses the
    /// hotkey again during a slow activation) the in-progress invocation is cancelled and
    /// awaited before the next one starts, so invocations never overlap, and a superseded
    /// invocation ending in an <see cref="OperationCanceledException"/> is treated as expected
    /// rather than as a failure.
    /// </remarks>
    public static void RunAsyncEventHandler(
        string eventName, Func<CancellationToken, Task> callback, DispatcherQueue dispatcherQueue, CancellationToken cancel)
    {
        Logger.LogDebug($"starting event handler for event '{eventName}'");
        var thread = new Thread(() =>
        {
            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

            // the invocation currently in progress, if any
            CancellationTokenSource? invocationCancellation = null;
            Task? invocationTask = null;

            try
            {
                while (true)
                {
                    Logger.LogDebug($"[{eventName}] - entering event handler loop");

                    // also wake up when the current invocation completes, so a failure is
                    // observed (and rethrown) straight away rather than only when the event
                    // is next signalled
                    var waitHandles = invocationTask is null
                        ? new WaitHandle[] { cancel.WaitHandle, eventHandle }
                        : new WaitHandle[] { cancel.WaitHandle, eventHandle, ((IAsyncResult)invocationTask).AsyncWaitHandle };
                    var signalled = WaitHandle.WaitAny(waitHandles);
                    if (signalled == 0)
                    {
                        Logger.LogDebug($"[{eventName}] - exiting event handler loop");
                        return;
                    }

                    if (invocationTask is not null)
                    {
                        if (signalled == 1)
                        {
                            Logger.LogDebug($"[{eventName}] - cancelling superseded callback");
                            invocationCancellation!.Cancel();
                        }

                        MouseJumpEventLoop.ObserveInvocation(eventName, invocationTask, invocationCancellation!.Token);
                        invocationCancellation.Dispose();
                        invocationCancellation = null;
                        invocationTask = null;
                    }

                    if (signalled == 1)
                    {
                        Logger.LogDebug($"[{eventName}] - invoking callback");
                        invocationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancel);
                        invocationTask = MouseJumpEventLoop.InvokeOnDispatcherAsync(callback, dispatcherQueue, invocationCancellation.Token);
                    }
                }
            }
            finally
            {
                // stop any invocation still in progress when the loop exits. deliberately not
                // disposed, since the callback may still be running on the ui thread and
                // observing its token
                invocationCancellation?.Cancel();
            }
        });

        thread.Name = "MouseJumpEventLoopThread";
        thread.IsBackground = true;

        thread.Start();
    }

    /// <summary>
    /// Queues <paramref name="callback"/> on <paramref name="dispatcherQueue"/> and returns a
    /// task that completes when the callback's own task does.
    /// </summary>
    private static Task InvokeOnDispatcherAsync(
        Func<CancellationToken, Task> callback, DispatcherQueue dispatcherQueue, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                _ = callback(cancellationToken).ContinueWith(
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
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }))
        {
            tcs.SetException(new InvalidOperationException("DispatcherQueue is not accepting work items."));
        }

        return tcs.Task;
    }

    /// <summary>
    /// Blocks until <paramref name="invocationTask"/> completes, ignoring a cancellation caused by
    /// <paramref name="cancellationToken"/> and rethrowing anything else.
    /// </summary>
    private static void ObserveInvocation(string eventName, Task invocationTask, CancellationToken cancellationToken)
    {
        try
        {
            invocationTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // superseded by a newer invocation (or the loop is shutting down) - expected
            Logger.LogDebug($"[{eventName}] - callback was cancelled");
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
}
