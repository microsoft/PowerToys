// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Storage.Streams;
using Windows.System;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests;

[TestClass]
public sealed class ClipboardHistoryWorkerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task RunAsync_AcrossManagedAndWinRtAwaits_ReusesPumpedStaThread()
    {
        await using var worker = new ClipboardHistoryWorker();
        var threadId = 0;
        await worker.RunAsync(async () =>
        {
            threadId = Environment.CurrentManagedThreadId;
            Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            Assert.IsNotNull(DispatcherQueue.GetForCurrentThread());
            await Task.Yield();
            Assert.AreEqual(threadId, Environment.CurrentManagedThreadId);
            using var stream = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(stream);
            writer.WriteString("clipboard");
            await writer.StoreAsync();
            Assert.AreEqual(threadId, Environment.CurrentManagedThreadId);
            Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
        }).WaitAsync(Timeout);

        await worker.RunAsync(() =>
        {
            Assert.AreEqual(threadId, Environment.CurrentManagedThreadId);
            return Task.CompletedTask;
        }).WaitAsync(Timeout);
    }

    [TestMethod]
    public async Task DisposeAsync_PendingContinuation_DrainsBeforeStoppingQueue()
    {
        await using var worker = new ClipboardHistoryWorker();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DispatcherQueue queue = null;
        var resumed = false;
        var work = worker.RunAsync(async () =>
        {
            queue = DispatcherQueue.GetForCurrentThread();
            started.SetResult();
            await release.Task;
            resumed = true;
        });

        await started.Task.WaitAsync(Timeout);
        var shutdown = worker.DisposeAsync().AsTask();
        try
        {
            Assert.IsFalse(shutdown.IsCompleted);
            Assert.ThrowsExactly<ObjectDisposedException>(() => worker.RunAsync(() => Task.CompletedTask));
        }
        finally
        {
            release.TrySetResult();
        }

        await work.WaitAsync(Timeout);
        await shutdown.WaitAsync(Timeout);
        Assert.IsTrue(resumed);
        Assert.IsFalse(queue.TryEnqueue(() => { }));
        await worker.DisposeAsync();
    }

    [TestMethod]
    public async Task RunAsync_Fault_PropagatesAndLeavesWorkerUsable()
    {
        await using var worker = new ClipboardHistoryWorker();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => worker.RunAsync(
            () => throw new InvalidOperationException("Expected failure.")).WaitAsync(Timeout));

        var ran = false;
        await worker.RunAsync(() =>
        {
            ran = true;
            return Task.CompletedTask;
        }).WaitAsync(Timeout);

        Assert.IsTrue(ran);
    }
}
