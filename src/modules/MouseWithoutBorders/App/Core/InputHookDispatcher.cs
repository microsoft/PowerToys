// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MouseWithoutBorders.Core;

/// <summary>
/// Runs work requested from inside the WH_MOUSE_LL / WH_KEYBOARD_LL callbacks on one dedicated
/// thread, in FIFO order.
/// </summary>
/// <remarks>
/// Windows only gives a low-level hook callback LowLevelHooksTimeout to return. Past that,
/// all system input stalls and Windows may silently remove the hook. The hook callbacks keep the
/// cheap, synchronous routing state (which machine is active, whether to swallow the event) and
/// post anything that can block (socket sends, pacing sleeps, cross-thread waits) here instead.
/// A single consumer thread keeps the posted work in the same order the hook produced it.
/// SendInput calls made from the hook (e.g. releasing keys on a machine switch) intentionally stay on the hook
/// thread: InputSimulation flags them via InputHook.RealData so the hook can skip its own injected events, and
/// that flag is only reliable when the injection happens on the hook thread. SendInput does not block.
/// </remarks>
internal sealed class InputHookDispatcher : IDisposable
{
    private static readonly Lazy<InputHookDispatcher> DefaultInstance = new(() => new InputHookDispatcher("MWB input hook dispatcher"));

    [ThreadStatic]
    private static bool inHookCallback;

    private readonly BlockingCollection<Action> queue = new(new ConcurrentQueue<Action>());

    // Guards openSlot and keeps queue order consistent with it. Only held for a few instructions, never while running work.
    private readonly Lock postLock = new();
    private CoalescingSlot openSlot;
    private long coalescedCount;

    internal InputHookDispatcher(string threadName)
    {
        var thread = new System.Threading.Thread(Run)
        {
            Name = threadName,
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
        Thread.UpdateThreads(thread);
        thread.Start();
    }

    internal static InputHookDispatcher Default => DefaultInstance.Value;

    /// <summary>
    /// Gets a value indicating whether the current thread is executing a low-level hook callback.
    /// </summary>
    internal static bool IsInHookCallback => inHookCallback;

    /// <summary>
    /// Gets the number of coalescible work items that were replaced by a newer one before they ran.
    /// </summary>
    internal long CoalescedCount => Interlocked.Read(ref coalescedCount);

    /// <summary>
    /// Marks the current thread as running a low-level hook callback until the returned scope is disposed.
    /// </summary>
    internal static HookCallbackScope EnterHookCallback()
    {
        bool previous = inHookCallback;
        inHookCallback = true;
        return new HookCallbackScope(previous);
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the dispatcher thread when called from a hook callback; otherwise runs it inline.
    /// </summary>
    internal static void RunOffHook(Action action)
    {
        if (IsInHookCallback)
        {
            _ = Default.Post(action);
        }
        else
        {
            action();
        }
    }

    /// <summary>
    /// Waits <paramref name="milliseconds"/> without blocking a hook callback: from a hook callback, the
    /// wait is queued so it still spaces out later queued work; otherwise it sleeps inline.
    /// </summary>
    internal static void Pause(int milliseconds)
    {
        RunOffHook(() => Thread.Sleep(milliseconds));
    }

    /// <summary>
    /// Queues <paramref name="action"/> to run on the dispatcher thread.
    /// </summary>
    /// <param name="action">The work to run.</param>
    /// <param name="coalesce">
    /// True for work that is fully superseded by the next work of the same kind (e.g. absolute mouse moves).
    /// If the previously queued item is also coalescible and has not started yet, it is replaced by this one, so
    /// a slow consumer only ever has the latest position pending. Coalescing never reorders across non-coalescible
    /// work: a non-coalescible post closes the open slot.
    /// </param>
    /// <returns>True if queued or merged; false if the dispatcher is shut down.</returns>
    internal bool Post(Action action, bool coalesce = false)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (postLock)
        {
            if (coalesce && openSlot != null)
            {
                openSlot.Latest = action;
                _ = Interlocked.Increment(ref coalescedCount);
                return true;
            }

            CoalescingSlot slot = coalesce ? new CoalescingSlot(action) : null;

            try
            {
                queue.Add(slot != null ? () => RunSlot(slot) : action);
            }
            catch (InvalidOperationException)
            {
                // Adding was completed: the dispatcher is shutting down.
                openSlot = null;
                return false;
            }

            openSlot = slot;
            return true;
        }
    }

    public void Dispose()
    {
        queue.CompleteAdding();
    }

    private void RunSlot(CoalescingSlot slot)
    {
        Action latest;

        lock (postLock)
        {
            if (openSlot == slot)
            {
                openSlot = null;
            }

            latest = slot.Latest;
        }

        latest();
    }

    private void Run()
    {
        // SuppressFlow fixes an issue on service mode, where the helper process can't get enough permissions to be started again.
        // More details can be found on: https://github.com/microsoft/PowerToys/pull/36892
        using var asyncFlowControl = ExecutionContext.SuppressFlow();

        foreach (Action action in queue.GetConsumingEnumerable())
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }
    }

    private sealed class CoalescingSlot
    {
        internal CoalescingSlot(Action latest)
        {
            Latest = latest;
        }

        internal Action Latest { get; set; }
    }

    internal readonly struct HookCallbackScope : IDisposable
    {
        private readonly bool previous;

        internal HookCallbackScope(bool previous)
        {
            this.previous = previous;
        }

        public void Dispose()
        {
            inHookCallback = previous;
        }
    }
}
