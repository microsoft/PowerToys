// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

using Common.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PreviewHandlerCommonUnitTests
{
    [TestClass]
    public class NativeEventWaiterTests
    {
        private static readonly TimeSpan CallbackTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan NoCallbackTimeout = TimeSpan.FromMilliseconds(500);

        [TestMethod]
        public void WaitForEventLoopShouldRunCallbackThroughSynchronizationContext()
        {
            var eventName = CreateUniqueEventName();
            var context = new QueueingSynchronizationContext();
            var callbackThreadId = 0;
            using var cancellation = new CancellationTokenSource();
            using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

            try
            {
                NativeEventWaiter.WaitForEventLoop(eventName, () => callbackThreadId = Environment.CurrentManagedThreadId, context, cancellation.Token);
                signal.Set();

                // The waiter thread must only post the callback; it runs when the context's thread pumps it.
                Assert.IsTrue(context.TryRunNext(CallbackTimeout), "The callback was not posted to the synchronization context.");
                Assert.AreEqual(Environment.CurrentManagedThreadId, callbackThreadId);
            }
            finally
            {
                cancellation.Cancel();
            }
        }

        [TestMethod]
        public void WaitForEventLoopShouldPostOncePerSignal()
        {
            var eventName = CreateUniqueEventName();
            var context = new QueueingSynchronizationContext();
            var callbackCount = 0;
            using var cancellation = new CancellationTokenSource();
            using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

            try
            {
                NativeEventWaiter.WaitForEventLoop(eventName, () => callbackCount++, context, cancellation.Token);

                for (var i = 1; i <= 3; i++)
                {
                    signal.Set();
                    Assert.IsTrue(context.TryRunNext(CallbackTimeout), $"Signal {i} was not posted.");
                    Assert.AreEqual(i, callbackCount);
                }

                Assert.IsFalse(context.TryRunNext(NoCallbackTimeout), "The callback was posted without a signal.");
            }
            finally
            {
                cancellation.Cancel();
            }
        }

        [TestMethod]
        public void WaitForEventLoopShouldStopWaitingWhenCanceled()
        {
            var eventName = CreateUniqueEventName();
            var context = new QueueingSynchronizationContext();
            using var cancellation = new CancellationTokenSource();
            using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);

            try
            {
                NativeEventWaiter.WaitForEventLoop(eventName, () => { }, context, cancellation.Token);
                signal.Set();
                Assert.IsTrue(context.TryRunNext(CallbackTimeout), "The waiter didn't start.");

                cancellation.Cancel();
                signal.Set();

                Assert.IsFalse(context.TryRunNext(NoCallbackTimeout), "The callback was posted after cancellation.");

                // Nobody consumed the auto-reset signal, so the waiter is no longer waiting on the event.
                Assert.IsTrue(signal.WaitOne(0), "The event was consumed after cancellation.");
            }
            finally
            {
                cancellation.Cancel();
            }
        }

        [TestMethod]
        public void WaitForEventLoopShouldRejectNullArguments()
        {
            var eventName = CreateUniqueEventName();

            Assert.ThrowsExactly<ArgumentNullException>(() => NativeEventWaiter.WaitForEventLoop(eventName, () => { }, null, CancellationToken.None));
            Assert.ThrowsExactly<ArgumentNullException>(() => NativeEventWaiter.WaitForEventLoop(eventName, null, new QueueingSynchronizationContext(), CancellationToken.None));
        }

        private static string CreateUniqueEventName() => @"Local\PowerToys_NativeEventWaiterTests_" + Guid.NewGuid().ToString("N");

        // Queues posted callbacks and runs them on the thread calling TryRunNext, like a UI thread pumping messages.
        private sealed class QueueingSynchronizationContext : SynchronizationContext
        {
            private readonly ConcurrentQueue<(SendOrPostCallback Callback, object State)> _posted = new();

            public override void Post(SendOrPostCallback d, object state) => _posted.Enqueue((d, state));

            public bool TryRunNext(TimeSpan timeout)
            {
                var stopwatch = Stopwatch.StartNew();
                (SendOrPostCallback Callback, object State) item;
                while (!_posted.TryDequeue(out item))
                {
                    if (stopwatch.Elapsed > timeout)
                    {
                        return false;
                    }

                    Thread.Sleep(10);
                }

                item.Callback(item.State);
                return true;
            }
        }
    }
}
