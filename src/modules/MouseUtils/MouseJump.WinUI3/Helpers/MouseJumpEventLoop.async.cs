// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

using Microsoft.UI.Dispatching;

namespace MouseJump.WinUI3.Helpers;

internal static partial class MouseJumpEventLoop
{
    /// <summary>
    /// An in-progress invocation of a <see cref="RunAsyncEventHandler"/> callback, and the
    /// cancellation source that can stop it.
    /// </summary>
    private sealed record Invocation(CancellationTokenSource Cancellation, Task Task);

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

        // propagates the outcome of the original callback onto the task we returned to the caller
        static void CompleteFromCallbackTask(Task callbackTask, TaskCompletionSource tcs, CancellationToken cancellationToken)
        {
            if (callbackTask.IsFaulted)
            {
                // unwrap the AggregateException so the caller sees the original exception(s)
                var aggregateException = callbackTask.Exception;
                tcs.SetException(aggregateException.InnerExceptions);
            }
            else if (callbackTask.IsCanceled)
            {
                tcs.SetCanceled(cancellationToken);
            }
            else
            {
                tcs.SetResult();
            }
        }

        // runs on the ui thread - starts the callback, then arranges for its outcome to be copied
        // across when it completes. this returns as soon as the callback reaches its first await,
        // so the ui thread isn't blocked while the callback runs
        static void StartCallback(Func<CancellationToken, Task> callback, TaskCompletionSource tcs, CancellationToken cancellationToken)
        {
            Task callbackTask;
            try
            {
                callbackTask = callback(cancellationToken);
            }
            catch (Exception ex)
            {
                // the callback threw before it even returned a task (an async method would
                // capture the exception in its task instead, so this is just a safety net)
                tcs.SetException(ex);
                return;
            }

            // copy the outcome on the thread pool - it doesn't need the ui thread
            _ = callbackTask.ContinueWith(
                t => CompleteFromCallbackTask(t, tcs, cancellationToken),
                TaskScheduler.Default);
        }

        // queues the callback on the dispatcher (ui thread) and returns a task that completes when
        // the callback's own task does. DispatcherQueue.TryEnqueue can only tell us whether the
        // work was *queued*, not when it finishes, so we complete this one by hand instead -
        // either when the callback itself completes, or straight away if it can't even be started
        static Task InvokeOnDispatcherAsync(
            Func<CancellationToken, Task> callback, DispatcherQueue dispatcherQueue, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!dispatcherQueue.TryEnqueue(
                () => StartCallback(callback, tcs, cancellationToken)))
            {
                // the dispatcher is shutting down, so StartCallback will never run
                tcs.SetException(new InvalidOperationException("DispatcherQueue is not accepting work items."));
            }

            return tcs.Task;
        }

        // starts a new invocation of the callback with its own cancellation token, linked to cancel
        // so that shutting down the loop also cancels it
        static Invocation StartInvocation(
            string eventName, Func<CancellationToken, Task> callback, DispatcherQueue dispatcherQueue, CancellationToken cancel)
        {
            Logger.LogDebug($"[{eventName}] - invoking callback");
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            var task = InvokeOnDispatcherAsync(callback, dispatcherQueue, cancellation.Token);
            return new Invocation(cancellation, task);
        }

        // waits for an invocation to complete - cancelling it first if it's been superseded by a
        // newer one - treating that cancellation as expected and rethrowing anything else
        static void EndInvocation(string eventName, Invocation invocation, bool superseded)
        {
            if (superseded)
            {
                Logger.LogDebug($"[{eventName}] - cancelling superseded callback");
                invocation.Cancellation.Cancel();
            }

            try
            {
                invocation.Task.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (invocation.Cancellation.IsCancellationRequested)
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
            finally
            {
                invocation.Cancellation.Dispose();
            }
        }

        // runs on the event loop thread - starts a new invocation of the callback every time the
        // specified event is signalled, superseding any invocation still in progress, until cancel
        // is signalled
        static void RunLoop(
            string eventName, Func<CancellationToken, Task> callback, DispatcherQueue dispatcherQueue, CancellationToken cancel)
        {
            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

            // the invocation currently in progress, if any
            Invocation? invocation = null;

            try
            {
                while (true)
                {
                    Logger.LogDebug($"[{eventName}] - entering event handler loop");

                    // also wake up when the current invocation completes, so a failure is
                    // observed (and rethrown) straight away rather than only when the event
                    // is next signalled
                    var waitHandles = invocation is null
                        ? new WaitHandle[] { cancel.WaitHandle, eventHandle }
                        : new WaitHandle[] { cancel.WaitHandle, eventHandle, ((IAsyncResult)invocation.Task).AsyncWaitHandle };
                    var signalled = WaitHandle.WaitAny(waitHandles);
                    if (signalled == 0)
                    {
                        Logger.LogDebug($"[{eventName}] - exiting event handler loop");
                        return;
                    }

                    // either the event was signalled again (so the current invocation has been
                    // superseded) or the current invocation has completed on its own
                    var eventSignalled = signalled == 1;
                    if (invocation is not null)
                    {
                        EndInvocation(eventName, invocation, superseded: eventSignalled);
                        invocation = null;
                    }

                    if (eventSignalled)
                    {
                        invocation = StartInvocation(eventName, callback, dispatcherQueue, cancel);
                    }
                }
            }
            finally
            {
                // stop any invocation still in progress when the loop exits. deliberately not
                // disposed, since the callback may still be running on the ui thread and
                // observing its token
                invocation?.Cancellation.Cancel();
            }
        }

        var thread = new Thread(
            () => RunLoop(eventName, callback, dispatcherQueue, cancel));
        thread.Name = "MouseJumpEventLoopThread";
        thread.IsBackground = true;

        thread.Start();
    }
}
