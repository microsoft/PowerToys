// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering.UnitTests;

[TestClass]
public sealed class LatestWinsUpdateQueueTests
{
    [TestMethod]
    public async Task ActiveUpdateCompletesAndOnlyNewestPendingUpdateRuns()
    {
        var processed = new List<int>();
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new LatestWinsUpdateQueue<int>(async update =>
        {
            if (update == 1)
            {
                firstStarted.TrySetResult(true);
                await releaseFirst.Task;
            }

            processed.Add(update);
        });

        queue.Enqueue(1);
        await firstStarted.Task;
        queue.Enqueue(2);
        queue.Enqueue(3);
        releaseFirst.TrySetResult(true);
        await queue.WhenIdleAsync();

        Assert.AreEqual(2, processed.Count);
        Assert.AreEqual(1, processed[0]);
        Assert.AreEqual(3, processed[1]);
    }

    [TestMethod]
    public async Task ClearPendingDoesNotInterruptActiveUpdate()
    {
        var processed = new List<int>();
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new LatestWinsUpdateQueue<int>(async update =>
        {
            firstStarted.TrySetResult(true);
            await releaseFirst.Task;
            processed.Add(update);
        });

        queue.Enqueue(1);
        await firstStarted.Task;
        queue.Enqueue(2);
        queue.ClearPending();
        releaseFirst.TrySetResult(true);
        await queue.WhenIdleAsync();

        Assert.AreEqual(1, processed.Count);
        Assert.AreEqual(1, processed[0]);
    }

    [TestMethod]
    public async Task ProcessorFailureDoesNotPreventNewestPendingUpdate()
    {
        var processed = new List<int>();
        var errors = new List<Exception>();
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue = new LatestWinsUpdateQueue<int>(
            async update =>
            {
                if (update == 1)
                {
                    firstStarted.TrySetResult(true);
                    await releaseFirst.Task;
                    throw new InvalidOperationException("Expected test failure.");
                }

                processed.Add(update);
            },
            errors.Add);

        queue.Enqueue(1);
        await firstStarted.Task;
        queue.Enqueue(2);
        releaseFirst.TrySetResult(true);
        await queue.WhenIdleAsync();

        Assert.AreEqual(1, errors.Count);
        Assert.AreEqual(1, processed.Count);
        Assert.AreEqual(2, processed[0]);
    }
}
