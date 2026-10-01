// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Helpers;

[TestClass]
public class NativeEventWaiterTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task Dispose_WhileWaiting_ClosesTheNamedHandle()
    {
        var eventName = NewEventName();
        using var dispatcher = new QueuedDispatcher();
        using var registration = NativeEventWaiter.Register(eventName, () => Assert.Fail("No event was signaled."), dispatcher.Enqueue);

        await Task.Run(registration.Dispose).WaitAsync(TestTimeout);

        AssertEventClosed(eventName);
    }

    [TestMethod]
    public void ActiveRegistration_DispatchesRepeatedEventsOnlyWhenTheQueueRuns()
    {
        var eventName = NewEventName();
        using var dispatcher = new QueuedDispatcher();
        var calls = 0;
        using var registration = NativeEventWaiter.Register(eventName, () => calls++, dispatcher.Enqueue);

        for (var index = 0; index < 3; index++)
        {
            Signal(eventName);
            var callback = dispatcher.Take();
            Assert.AreEqual(index, calls);
            callback();
            Assert.AreEqual(index + 1, calls);
        }
    }

    [TestMethod]
    public void Dispose_AfterDispatchWasQueued_SuppressesTheOldGeneration()
    {
        var eventName = NewEventName();
        using var dispatcher = new QueuedDispatcher();
        var oldCalls = 0;
        var currentCalls = 0;
        using var previous = NativeEventWaiter.Register(eventName, () => oldCalls++, dispatcher.Enqueue);
        Signal(eventName);
        var oldCallback = dispatcher.Take();
        previous.Dispose();

        using var current = NativeEventWaiter.Register(eventName, () => currentCalls++, dispatcher.Enqueue);
        Signal(eventName);
        var currentCallback = dispatcher.Take();
        oldCallback();
        currentCallback();

        Assert.AreEqual(0, oldCalls);
        Assert.AreEqual(1, currentCalls);
    }

    [TestMethod]
    public async Task Dispose_FromQueuedCallback_DoesNotWaitForTheDispatcher()
    {
        var eventName = NewEventName();
        using var dispatcher = new QueuedDispatcher();
        IDisposable registration = null;
        registration = NativeEventWaiter.Register(eventName, () => registration.Dispose(), dispatcher.Enqueue);
        using (registration)
        {
            Signal(eventName);
            await Task.Run(dispatcher.Take()).WaitAsync(TestTimeout);
            AssertEventClosed(eventName);
        }
    }

    [TestMethod]
    public async Task Dispose_FromDispatchingWorker_DoesNotJoinItself()
    {
        var eventName = NewEventName();
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable registration = null;
        registration = NativeEventWaiter.Register(eventName, () => Assert.Fail("Dispatcher should not invoke callbacks inline."), _ =>
        {
            registration.Dispose();
            disposed.SetResult();
            return true;
        });
        using (registration)
        {
            Signal(eventName);
            await disposed.Task.WaitAsync(TestTimeout);
            registration.Dispose();
            AssertEventClosed(eventName);
        }
    }

    [TestMethod]
    public async Task Dispose_FromCallback_UnblocksAWorkerDispatchingInline()
    {
        var eventName = NewEventName();
        using var dispatcher = new QueuedDispatcher();
        using var callbackStarted = new ManualResetEventSlim();
        using var secondDispatchStarted = new ManualResetEventSlim();
        var dispatches = 0;
        var calls = 0;
        IDisposable registration = null;
        registration = NativeEventWaiter.Register(
            eventName,
            () =>
            {
                Interlocked.Increment(ref calls);
                callbackStarted.Set();
                Assert.IsTrue(secondDispatchStarted.Wait(TestTimeout));
                registration.Dispose();
            },
            action =>
            {
                if (Interlocked.Increment(ref dispatches) == 1)
                {
                    return dispatcher.Enqueue(action);
                }

                secondDispatchStarted.Set();
                action();
                return true;
            });
        using (registration)
        {
            Signal(eventName);
            var callback = Task.Run(dispatcher.Take());
            Assert.IsTrue(callbackStarted.Wait(TestTimeout));
            Signal(eventName);
            await callback.WaitAsync(TestTimeout);
            Assert.AreEqual(1, calls);
            AssertEventClosed(eventName);
        }
    }

    [TestMethod]
    public async Task Dispose_ConcurrentCalls_AreIdempotent()
    {
        var eventName = NewEventName();
        using var dispatcher = new QueuedDispatcher();
        using var registration = NativeEventWaiter.Register(eventName, () => { }, dispatcher.Enqueue);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(registration.Dispose))).WaitAsync(TestTimeout);

        AssertEventClosed(eventName);
    }

    [TestMethod]
    public void RepeatedRegistrations_DoNotKeepNamedHandlesAlive()
    {
        using var dispatcher = new QueuedDispatcher();
        for (var index = 0; index < 25; index++)
        {
            var eventName = NewEventName();
            using var registration = NativeEventWaiter.Register(eventName, () => { }, dispatcher.Enqueue);
            registration.Dispose();
            AssertEventClosed(eventName);
        }
    }

    [TestMethod]
    public void Dispose_QueuedCallbackDoesNotRetainItsTarget()
    {
        var eventName = NewEventName();
        using var dispatcher = new QueuedDispatcher();
        using var registration = RegisterTarget(eventName, dispatcher, out var target);
        Signal(eventName);
        var queuedCallback = dispatcher.Take();
        registration.Dispose();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.IsFalse(target.IsAlive, "A queued callback retained the disposed registration's target.");
        queuedCallback();
        GC.KeepAlive(registration);
    }

    [TestMethod]
    public async Task RejectedDispatch_StopsRegistrationAndReleasesHandles()
    {
        var eventName = NewEventName();
        var rejected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = NativeEventWaiter.Register(eventName, () => Assert.Fail("Dispatch was rejected."), _ =>
        {
            rejected.SetResult();
            return false;
        });

        Signal(eventName);
        await rejected.Task.WaitAsync(TestTimeout);
        await Task.Run(registration.Dispose).WaitAsync(TestTimeout);
        AssertEventClosed(eventName);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IDisposable RegisterTarget(string eventName, QueuedDispatcher dispatcher, out WeakReference reference)
    {
        var target = new CallbackTarget();
        reference = new WeakReference(target);
        return NativeEventWaiter.Register(eventName, target.Invoke, dispatcher.Enqueue);
    }

    private static string NewEventName() => $@"Local\PowerToys-Settings-NativeWaiterTests-{Guid.NewGuid():N}";

    private static void Signal(string eventName)
    {
        using var handle = EventWaitHandle.OpenExisting(eventName);
        handle.Set();
    }

    private static void AssertEventClosed(string eventName)
    {
        var exists = EventWaitHandle.TryOpenExisting(eventName, out var handle);
        handle?.Dispose();
        Assert.IsFalse(exists, "Disposal must finish the worker and close its named event handle.");
    }

    private sealed class CallbackTarget
    {
        public void Invoke() => Assert.Fail("The disposed target should never run.");
    }

    private sealed class QueuedDispatcher : IDisposable
    {
        private readonly BlockingCollection<Action> _callbacks = new();

        public bool Enqueue(Action callback)
        {
            _callbacks.Add(callback);
            return true;
        }

        public Action Take()
        {
            Assert.IsTrue(_callbacks.TryTake(out var callback, TestTimeout), "The worker did not enqueue a callback.");
            return callback;
        }

        public void Dispose() => _callbacks.Dispose();
    }
}
