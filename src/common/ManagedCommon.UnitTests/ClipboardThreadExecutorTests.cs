// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ManagedCommon.UnitTests
{
    [TestClass]
    public sealed class ClipboardThreadExecutorTests
    {
        private static readonly int[] ExpectedOrder = { 1, 2 };

        [TestMethod]
        public async Task InvokeAsync_RunsOnStaThread()
        {
            using var executor = new ClipboardThreadExecutor();

            ApartmentState apartmentState = await executor.InvokeAsync(
                () => Thread.CurrentThread.GetApartmentState());

            Assert.AreEqual(ApartmentState.STA, apartmentState);
        }

        [TestMethod]
        public async Task InvokeAsync_AwaitTaskYield_ResumesOnSameStaThread()
        {
            using var executor = new ClipboardThreadExecutor();

            (
                int BeforeThreadId,
                int AfterThreadId,
                ApartmentState BeforeApartment,
                ApartmentState AfterApartment,
                bool HadContextBefore,
                bool HadContextAfter) result = await executor.InvokeAsync(async () =>
                {
                    int beforeThreadId = Environment.CurrentManagedThreadId;
                    ApartmentState beforeApartment = Thread.CurrentThread.GetApartmentState();
                    bool hadContextBefore = SynchronizationContext.Current is not null;

                    await Task.Yield();

                    return (
                        beforeThreadId,
                        Environment.CurrentManagedThreadId,
                        beforeApartment,
                        Thread.CurrentThread.GetApartmentState(),
                        hadContextBefore,
                        SynchronizationContext.Current is not null);
                });

            Assert.AreEqual(result.BeforeThreadId, result.AfterThreadId);
            Assert.AreEqual(ApartmentState.STA, result.BeforeApartment);
            Assert.AreEqual(ApartmentState.STA, result.AfterApartment);
            Assert.IsTrue(result.HadContextBefore);
            Assert.IsTrue(result.HadContextAfter);
        }

        [TestMethod]
        public async Task InvokeAsync_SerializesQueuedWork()
        {
            using var executor = new ClipboardThreadExecutor();
            using var firstStarted = new ManualResetEventSlim();
            using var releaseFirst = new ManualResetEventSlim();
            var order = new List<int>();

            Task<int> first = executor.InvokeAsync(() =>
            {
                firstStarted.Set();
                if (!releaseFirst.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("The first queued operation was not released.");
                }

                order.Add(1);
                return 1;
            });

            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(1)));

            Task<int> second = executor.InvokeAsync(() =>
            {
                order.Add(2);
                return 2;
            });

            Assert.IsFalse(second.Wait(TimeSpan.FromMilliseconds(50)));
            releaseFirst.Set();

            await Task.WhenAll(first, second);
            CollectionAssert.AreEqual(ExpectedOrder, order);
        }

        [TestMethod]
        public async Task InvokeAsync_AsyncWorkDoesNotStartNextItemWhileAwaiting()
        {
            using var executor = new ClipboardThreadExecutor();
            using var firstStarted = new ManualResetEventSlim();
            var releaseFirst = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            int secondStarted = 0;

            Task<int> first = executor.InvokeAsync(async () =>
            {
                firstStarted.Set();
                await releaseFirst.Task;
                return 1;
            });

            Assert.IsTrue(firstStarted.Wait(TimeSpan.FromSeconds(1)));

            Task<int> second = executor.InvokeAsync(async () =>
            {
                Interlocked.Exchange(ref secondStarted, 1);
                await Task.Yield();
                return 2;
            });

            Assert.IsFalse(SpinWait.SpinUntil(
                () => Volatile.Read(ref secondStarted) != 0,
                TimeSpan.FromMilliseconds(100)));
            Assert.IsFalse(second.IsCompleted);

            releaseFirst.SetResult(null);

            int[] results = await Task.WhenAll(first, second);
            CollectionAssert.AreEqual(ExpectedOrder, results);
        }

        [TestMethod]
        public async Task InvokeAsync_PropagatesUnexpectedException()
        {
            using var executor = new ClipboardThreadExecutor();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => executor.InvokeAsync(new Func<int>(() => throw new InvalidOperationException("unexpected"))));
        }

        [TestMethod]
        public async Task InvokeAsync_DisposeFromExecutorAction_CompletesReturnedTask()
        {
            using var executor = new ClipboardThreadExecutor();
            using var unhandledExceptionSeen = new ManualResetEventSlim();
            Exception? unhandledException = null;

            void UnhandledExceptionHandler(object? sender, UnhandledExceptionEventArgs e)
            {
                unhandledException = e.ExceptionObject as Exception;
                unhandledExceptionSeen.Set();
            }

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
            try
            {
                Task<int> task = executor.InvokeAsync(() =>
                {
                    executor.Dispose();
                    return 42;
                });

                int result = await task;

                Assert.AreEqual(42, result);
                Assert.IsFalse(unhandledExceptionSeen.Wait(TimeSpan.FromMilliseconds(250)));
                Assert.IsNull(unhandledException);
            }
            finally
            {
                AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionHandler;
            }
        }
    }
}
