// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering.UnitTests;

[TestClass]
public sealed class LatestWinsUpdateQueueTests
{
    [TestMethod]
    public async Task ActiveUpdateFinishesAndNewestPendingUpdateRuns()
    {
        var processed = new List<int>();
        var firstStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new LatestWinsUpdateQueue<int>(async update =>
        {
            if (update == 1)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task;
            }

            processed.Add(update);
        });

        var first = queue.EnqueueAsync(1);
        await firstStarted.Task;
        var superseded = queue.EnqueueAsync(2);
        var newest = queue.EnqueueAsync(3);

        await superseded;
        Assert.IsFalse(first.IsCompleted);
        Assert.IsFalse(newest.IsCompleted);

        releaseFirst.TrySetResult();
        await Task.WhenAll(first, newest);

        Assert.HasCount(2, processed);
        Assert.AreEqual(1, processed[0]);
        Assert.AreEqual(3, processed[1]);
    }

    [TestMethod]
    public async Task ClearPendingDoesNotCancelActiveUpdate()
    {
        var processed = new List<int>();
        var firstStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new LatestWinsUpdateQueue<int>(async update =>
        {
            firstStarted.TrySetResult();
            await releaseFirst.Task;
            processed.Add(update);
        });

        var first = queue.EnqueueAsync(1);
        await firstStarted.Task;
        var pending = queue.EnqueueAsync(2);
        queue.ClearPending();

        await pending;
        Assert.IsFalse(first.IsCompleted);

        releaseFirst.TrySetResult();
        await first;

        Assert.HasCount(1, processed);
        Assert.AreEqual(1, processed[0]);
    }

    [TestMethod]
    public async Task ProcessorFailureDoesNotStopNewestPendingUpdate()
    {
        var processed = new List<int>();
        var firstStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new LatestWinsUpdateQueue<int>(async update =>
        {
            if (update == 1)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task;
                throw new InvalidOperationException("Expected test failure.");
            }

            processed.Add(update);
        });

        var first = queue.EnqueueAsync(1);
        await firstStarted.Task;
        var pending = queue.EnqueueAsync(2);
        releaseFirst.TrySetResult();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await first);
        await pending;

        Assert.HasCount(1, processed);
        Assert.AreEqual(2, processed[0]);
    }
}
