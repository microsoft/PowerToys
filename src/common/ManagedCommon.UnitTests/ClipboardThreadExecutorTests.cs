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
        public async Task InvokeAsync_PropagatesUnexpectedException()
        {
            using var executor = new ClipboardThreadExecutor();

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => executor.InvokeAsync<int>(() => throw new InvalidOperationException("unexpected")));
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
