// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.Utilities;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public class MtaWorkerTests
{
    [TestMethod]
    public async Task WorkAndCleanupRunOnTheSameMtaThread()
    {
        var cleanupThread = 0;
        var worker = new MtaWorker(() => cleanupThread = Environment.CurrentManagedThreadId);
        try
        {
            var firstThread = await worker.InvokeAsync(() =>
            {
                Assert.AreEqual(ApartmentState.MTA, Thread.CurrentThread.GetApartmentState());
                return Environment.CurrentManagedThreadId;
            });
            var secondThread = await worker.InvokeAsync(() => Environment.CurrentManagedThreadId);

            Assert.AreEqual(firstThread, secondThread);
            await worker.DisposeAsync();
            Assert.AreEqual(firstThread, cleanupThread);
        }
        finally
        {
            await worker.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task DisposalDoesNotReleaseResourcesDuringInFlightWork()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var cleanupCount = 0;
        var worker = new MtaWorker(() => Interlocked.Increment(ref cleanupCount));
        try
        {
            var first = worker.InvokeAsync(() =>
            {
                entered.Set();
                Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(10)));
                Assert.AreEqual(0, Volatile.Read(ref cleanupCount));
                return 1;
            });
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
            var second = worker.InvokeAsync(() => 2);

            worker.Dispose();
            var stopped = worker.DisposeAsync().AsTask();
            Assert.IsFalse(stopped.IsCompleted);
            Assert.AreEqual(0, Volatile.Read(ref cleanupCount));
            Assert.ThrowsExactly<ObjectDisposedException>(() => worker.InvokeAsync(() => 3));

            release.Set();
            Assert.AreEqual(1, await first);
            Assert.AreEqual(2, await second);
            await stopped.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.AreEqual(1, cleanupCount);
        }
        finally
        {
            release.Set();
            await worker.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task FailedWorkIsReportedAndDoesNotStopTheWorker()
    {
        await using var worker = new MtaWorker(() => { });
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => worker.InvokeAsync<int>(() => throw new InvalidOperationException("Measurement failed.")));
        Assert.AreEqual(42, await worker.InvokeAsync(() => 42));
    }

    [TestMethod]
    public async Task CleanupFailureIsReported()
    {
        var worker = new MtaWorker(() => throw new InvalidOperationException("Cleanup failed."));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => worker.DisposeAsync().AsTask());
    }
}
