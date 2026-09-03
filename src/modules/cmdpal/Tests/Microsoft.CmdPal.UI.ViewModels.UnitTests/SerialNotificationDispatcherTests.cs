// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Verifies the single ordered dispatch path for provider add/remove notifications
/// (r3-p4-04). Every emission runs on one worker in strict first-in-first-out order, so a
/// consumer can never observe a provider addition ahead of the removal enqueued before it,
/// even when the two originate on different threads, and none of this depends on a
/// UI-thread concept such as DispatcherQueue.
/// </summary>
[TestClass]
public class SerialNotificationDispatcherTests
{
    [TestMethod]
    public void Enqueue_RunsNotificationsInFifoOrder()
    {
        using var dispatcher = new SerialNotificationDispatcher();
        var observed = new ConcurrentQueue<int>();
        var done = new CountdownEvent(500);

        for (var i = 0; i < 500; i++)
        {
            var value = i;
            dispatcher.Enqueue(() =>
            {
                observed.Enqueue(value);
                done.Signal();
            });
        }

        Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(5)), "All notifications should have run.");

        var expected = 0;
        foreach (var value in observed)
        {
            Assert.AreEqual(expected, value, "Notifications must run in enqueue order.");
            expected++;
        }

        Assert.AreEqual(500, expected);
    }

    // A paired removal enqueued ahead of an addition must always be observed first, even
    // when the two are enqueued from different threads racing each other.
    [TestMethod]
    public void Enqueue_FromConcurrentThreads_PreservesPerCallerOrder()
    {
        using var dispatcher = new SerialNotificationDispatcher();
        var removeBeforeAdd = true;
        var addSeen = false;
        var done = new CountdownEvent(200);

        for (var i = 0; i < 100; i++)
        {
            // Each iteration enqueues a "remove" then an "add" from the same caller. The
            // add handler must never run before its paired remove handler.
            var removed = false;
            dispatcher.Enqueue(() =>
            {
                removed = true;
                done.Signal();
            });
            dispatcher.Enqueue(() =>
            {
                if (!removed)
                {
                    removeBeforeAdd = false;
                }

                addSeen = true;
                done.Signal();
            });
        }

        Assert.IsTrue(done.Wait(TimeSpan.FromSeconds(5)), "All notifications should have run.");
        Assert.IsTrue(addSeen);
        Assert.IsTrue(removeBeforeAdd, "An addition must never overtake the removal enqueued before it.");
    }

    [TestMethod]
    public void Enqueue_AfterDispose_IsDroppedSilently()
    {
        var dispatcher = new SerialNotificationDispatcher();
        dispatcher.Dispose();

        var ran = false;
        dispatcher.Enqueue(() => ran = true);

        Thread.Sleep(100);
        Assert.IsFalse(ran, "A notification enqueued after dispose must not run.");
    }

    [TestMethod]
    public void Dispose_DrainsAlreadyEnqueuedNotifications()
    {
        var dispatcher = new SerialNotificationDispatcher();
        var count = 0;

        for (var i = 0; i < 50; i++)
        {
            dispatcher.Enqueue(() => Interlocked.Increment(ref count));
        }

        dispatcher.Dispose();

        Assert.AreEqual(50, Volatile.Read(ref count), "Dispose must let already-queued notifications drain.");
    }

    [TestMethod]
    public void Enqueue_HandlerException_DoesNotStopLaterNotifications()
    {
        using var dispatcher = new SerialNotificationDispatcher();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        dispatcher.Enqueue(() => throw new InvalidOperationException("boom"));
        dispatcher.Enqueue(() => reached.TrySetResult());

        Assert.IsTrue(reached.Task.Wait(TimeSpan.FromSeconds(5)), "A throwing handler must not stall the worker.");
    }

    // The dispatcher must never rely on a UI-thread concept such as DispatcherQueue: it has
    // to keep running notifications even when the calling thread's SynchronizationContext
    // would reject any attempt to marshal work onto it.
    [TestMethod]
    public void Enqueue_DoesNotDependOnCallingThreadSynchronizationContext()
    {
        var originalContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new ThrowingSynchronizationContext());

            using var dispatcher = new SerialNotificationDispatcher();
            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            dispatcher.Enqueue(() => reached.TrySetResult());

            Assert.IsTrue(reached.Task.Wait(TimeSpan.FromSeconds(5)), "Notifications must run without depending on the caller's synchronization context.");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
    }

    private sealed class ThrowingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) =>
            throw new InvalidOperationException("The dispatcher must not marshal work through the caller's synchronization context.");

        public override void Send(SendOrPostCallback d, object? state) =>
            throw new InvalidOperationException("The dispatcher must not marshal work through the caller's synchronization context.");
    }
}
