// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class IconLoadQueueTests
{
    [TestMethod]
    [Timeout(5_000)]
    public async Task DemandedWorkRunsBeforeSpeculativeHighPriorityWork()
    {
        var queue = new IconLoadQueue(workerCount: 1);
        var speculativeDemand = IconLoadDemand.CreateDemanded();
        speculativeDemand.RemoveRequester();
        var demanded = IconLoadDemand.CreateDemanded();
        var speculativeWork = new TestOperation();
        var demandedWork = new TestOperation();

        Assert.IsTrue(queue.TryEnqueue(
            speculativeWork,
            IconLoadPriority.High,
            speculativeDemand,
            out var speculativePriority));
        Assert.AreEqual(IconLoadPriority.High, speculativePriority);
        Assert.IsTrue(queue.TryEnqueue(
            demandedWork,
            IconLoadPriority.Low,
            demanded,
            out var demandedPriority));
        Assert.AreEqual(IconLoadPriority.Low, demandedPriority);

        Assert.AreSame(demandedWork, await queue.DequeueAsync());
        Assert.AreSame(speculativeWork, await queue.DequeueAsync());
        queue.Complete();
        await queue.Completion;
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task DemandLossMovesQueuedWorkBehindLiveRequests()
    {
        var queue = new IconLoadQueue(workerCount: 1);
        var firstRequest = new IconRequestDemand();
        var firstDemand = new IconLoadDemand();
        firstDemand.Attach(firstRequest);
        var secondDemand = IconLoadDemand.CreateDemanded();
        var firstWork = new TestOperation();
        var secondWork = new TestOperation();

        Assert.IsTrue(queue.TryEnqueue(firstWork, IconLoadPriority.Low, firstDemand, out _));
        Assert.IsTrue(queue.TryEnqueue(secondWork, IconLoadPriority.Low, secondDemand, out _));

        firstRequest.Release();

        Assert.AreSame(secondWork, await queue.DequeueAsync());
        Assert.AreSame(firstWork, await queue.DequeueAsync());
        queue.Complete();
        await queue.Completion;
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ReturnedDemandPromotesQueuedWork()
    {
        var queue = new IconLoadQueue(workerCount: 1);
        var promotedDemand = new IconLoadDemand();
        var speculativeDemand = new IconLoadDemand();
        var promotedWork = new TestOperation();
        var speculativeWork = new TestOperation();

        Assert.IsTrue(queue.TryEnqueue(promotedWork, IconLoadPriority.Low, promotedDemand, out _));
        Assert.IsTrue(queue.TryEnqueue(speculativeWork, IconLoadPriority.Low, speculativeDemand, out _));

        var returnedRequest = new IconRequestDemand();
        promotedDemand.Attach(returnedRequest);

        Assert.AreSame(promotedWork, await queue.DequeueAsync());
        Assert.AreSame(speculativeWork, await queue.DequeueAsync());
        queue.Complete();
        await queue.Completion;
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task HighPriorityRemainsFirstWithinDemandClass()
    {
        var queue = new IconLoadQueue(workerCount: 1);
        var lowDemand = IconLoadDemand.CreateDemanded();
        var highDemand = IconLoadDemand.CreateDemanded();
        var lowWork = new TestOperation();
        var highWork = new TestOperation();

        Assert.IsTrue(queue.TryEnqueue(lowWork, IconLoadPriority.Low, lowDemand, out _));
        Assert.IsTrue(queue.TryEnqueue(highWork, IconLoadPriority.High, highDemand, out _));

        Assert.AreSame(highWork, await queue.DequeueAsync());
        Assert.AreSame(lowWork, await queue.DequeueAsync());
        queue.Complete();
        await queue.Completion;
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task CompletionDrainsQueuedWorkBeforeStoppingWorkers()
    {
        var queue = new IconLoadQueue(workerCount: 2);
        var firstWork = new TestOperation();
        var secondWork = new TestOperation();

        Assert.IsTrue(queue.TryEnqueue(
            firstWork,
            IconLoadPriority.Low,
            IconLoadDemand.CreateDemanded(),
            out _));
        Assert.IsTrue(queue.TryEnqueue(
            secondWork,
            IconLoadPriority.Low,
            IconLoadDemand.CreateDemanded(),
            out _));

        queue.Complete();

        Assert.AreSame(firstWork, await queue.DequeueAsync());
        Assert.AreSame(secondWork, await queue.DequeueAsync());
        Assert.IsNull(await queue.DequeueAsync());
        await queue.Completion;
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task DequeueRejectsMoreConcurrentConsumersThanConfigured()
    {
        var queue = new IconLoadQueue(workerCount: 1);
        var firstDequeue = queue.DequeueAsync().AsTask();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await queue.DequeueAsync());

        queue.Complete();
        Assert.IsNull(await firstDequeue);
        await queue.Completion;
    }

    [TestMethod]
    [DataRow(2)]
    [DataRow(4)]
    [Timeout(5_000)]
    public async Task SpeculativeWorkLeavesOneWorkerAvailableForDemand(int workerCount)
    {
        var queue = new IconLoadQueue(workerCount);
        var releaseSpeculativeWork = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var speculativeCapacityFilled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var demandedWorkStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var speculativeStarts = 0;
        var workers = new Task[workerCount];

        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = RunWorkerAsync(queue);
        }

        try
        {
            for (var i = 0; i < workerCount; i++)
            {
                var speculativeDemand = IconLoadDemand.CreateDemanded();
                speculativeDemand.RemoveRequester();
                Assert.IsTrue(queue.TryEnqueue(
                    new TestOperation(async () =>
                    {
                        if (Interlocked.Increment(ref speculativeStarts) == workerCount - 1)
                        {
                            speculativeCapacityFilled.TrySetResult(true);
                        }

                        await releaseSpeculativeWork.Task;
                    }),
                    IconLoadPriority.Low,
                    speculativeDemand,
                    out _));
            }

            await speculativeCapacityFilled.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.IsTrue(queue.TryEnqueue(
                new TestOperation(() =>
                {
                    demandedWorkStarted.TrySetResult(true);
                    return Task.CompletedTask;
                }),
                IconLoadPriority.Low,
                IconLoadDemand.CreateDemanded(),
                out _));

            await demandedWorkStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(workerCount - 1, Volatile.Read(ref speculativeStarts));
        }
        finally
        {
            queue.Complete();
            releaseSpeculativeWork.TrySetResult(true);
            await Task.WhenAll(workers);
            await queue.Completion;
        }

        Assert.AreEqual(workerCount, speculativeStarts);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task SingleWorkerStillProcessesSpeculativeWork()
    {
        var queue = new IconLoadQueue(workerCount: 1);
        var work = new TestOperation();
        var speculativeDemand = IconLoadDemand.CreateDemanded();
        speculativeDemand.RemoveRequester();

        var dequeue = queue.DequeueAsync().AsTask();
        Assert.IsTrue(queue.TryEnqueue(work, IconLoadPriority.Low, speculativeDemand, out _));

        Assert.AreSame(work, await dequeue);
        queue.Complete();
        Assert.IsNull(await queue.DequeueAsync());
        await queue.Completion;
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task DemandChurnDuringDequeueRunsEveryWorkExactlyOnce()
    {
        const int WorkerCount = 4;
        const int ItemCount = 256;
        var queue = new IconLoadQueue(WorkerCount);
        var demands = new IconLoadDemand[ItemCount];
        var executionCounts = new int[ItemCount];
        var startedCount = 0;
        var firstBatchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstBatch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        for (var i = 0; i < ItemCount; i++)
        {
            var index = i;
            demands[index] = IconLoadDemand.CreateDemanded();
            Assert.IsTrue(queue.TryEnqueue(
                new TestOperation(async () =>
                {
                    var started = Interlocked.Increment(ref startedCount);
                    if (started <= WorkerCount)
                    {
                        if (started == WorkerCount)
                        {
                            firstBatchStarted.TrySetResult(true);
                        }

                        await releaseFirstBatch.Task;
                    }

                    Interlocked.Increment(ref executionCounts[index]);
                }),
                IconLoadPriority.Low,
                demands[index],
                out _));
        }

        var workers = new Task[WorkerCount];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = RunWorkerAsync(queue);
        }

        await firstBatchStarted.Task;
        await Task.Run(() =>
        {
            Parallel.For(0, ItemCount, i =>
            {
                for (var cycle = 0; cycle < 20; cycle++)
                {
                    demands[i].RemoveRequester();
                    demands[i].AddRequester();
                }
            });
        });

        queue.Complete();
        releaseFirstBatch.TrySetResult(true);
        await Task.WhenAll(workers);
        await queue.Completion;

        for (var i = 0; i < executionCounts.Length; i++)
        {
            Assert.AreEqual(1, executionCounts[i], $"Work item {i} ran an unexpected number of times.");
        }
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task CompletionRacingEnqueueDoesNotLoseAcceptedWork()
    {
        const int RoundCount = 10;
        const int WorkerCount = 4;
        const int ProducerCount = 8;
        const int AttemptsPerProducer = 128;

        for (var round = 0; round < RoundCount; round++)
        {
            var queue = new IconLoadQueue(WorkerCount);
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var attempts = 0;
            var accepted = 0;
            var executed = 0;

            var workers = new Task[WorkerCount];
            for (var i = 0; i < workers.Length; i++)
            {
                workers[i] = RunWorkerAsync(queue);
            }

            var producers = new Task[ProducerCount];
            for (var i = 0; i < producers.Length; i++)
            {
                producers[i] = Task.Run(async () =>
                {
                    await start.Task;
                    for (var attempt = 0; attempt < AttemptsPerProducer; attempt++)
                    {
                        if (queue.TryEnqueue(
                            new TestOperation(() =>
                            {
                                Interlocked.Increment(ref executed);
                                return Task.CompletedTask;
                            }),
                            IconLoadPriority.Low,
                            IconLoadDemand.CreateDemanded(),
                            out _))
                        {
                            Interlocked.Increment(ref accepted);
                        }

                        Interlocked.Increment(ref attempts);
                        Thread.Yield();
                    }
                });
            }

            var completion = Task.Run(async () =>
            {
                await start.Task;
                SpinWait.SpinUntil(() => Volatile.Read(ref attempts) >= 32, TimeSpan.FromSeconds(1));
                queue.Complete();
            });

            start.TrySetResult(true);
            await Task.WhenAll(producers);
            await completion;
            await Task.WhenAll(workers);
            await queue.Completion;

            Assert.AreEqual(accepted, executed, $"Accepted work was lost in round {round}.");
        }
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task CoordinatorFaultFailsQueuedOperationExactlyOnce()
    {
        var queue = new IconLoadQueue(workerCount: 1);
        var demand = IconLoadDemand.CreateDemanded();
        var operation = new TestOperation();
        var failure = new InvalidOperationException("Injected coordinator failure.");

        Assert.IsTrue(queue.TryEnqueue(operation, IconLoadPriority.High, demand, out _));
        queue.FailForTesting(failure);

        Assert.AreSame(failure, await operation.Failure.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.AreEqual(1, operation.FailureCount);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await queue.Completion);

        var retryQueue = new IconLoadQueue(workerCount: 1);
        var retry = new TestOperation();
        Assert.IsTrue(retryQueue.TryEnqueue(retry, IconLoadPriority.High, demand, out _));
        Assert.AreSame(retry, await retryQueue.DequeueAsync());
        retryQueue.Complete();
        Assert.IsNull(await retryQueue.DequeueAsync());
        await retryQueue.Completion;
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task CoordinatorFaultWaitsForAcceptedPublisherAndFailsItsOperation()
    {
        var queue = new IconLoadQueue(workerCount: 1);
        using var enqueueEntered = new ManualResetEventSlim();
        using var resumeEnqueue = new ManualResetEventSlim();
        var operation = new TestOperation(
            enqueued: (_, _) =>
            {
                enqueueEntered.Set();
                resumeEnqueue.Wait();
            });
        var failure = new InvalidOperationException("Injected coordinator failure.");

        var publisher = Task.Run(() =>
            queue.TryEnqueue(operation, IconLoadPriority.Low, IconLoadDemand.CreateDemanded(), out _));
        Assert.IsTrue(enqueueEntered.Wait(TimeSpan.FromSeconds(2)));

        queue.FailForTesting(failure);
        Assert.IsTrue(
            SpinWait.SpinUntil(() => queue.CoordinatorFailedForTesting, TimeSpan.FromSeconds(2)),
            "The coordinator did not observe the injected failure.");
        Assert.IsFalse(queue.Completion.IsCompleted, "The coordinator must wait for the accepted publisher.");

        resumeEnqueue.Set();
        Assert.IsTrue(await publisher);
        Assert.AreSame(failure, await operation.Failure.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.AreEqual(1, operation.FailureCount);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await queue.Completion);
    }

    private static async Task RunWorkerAsync(IconLoadQueue queue)
    {
        while (await queue.DequeueAsync() is { } operation)
        {
            await operation.ExecuteAsync();
        }
    }

    private sealed class TestOperation : IconLoadQueue.Operation
    {
        private readonly Func<Task> _execute;
        private readonly Action<IconLoadPriority, int>? _enqueued;
        private readonly TaskCompletionSource<Exception> _failure = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _failureCount;

        public TestOperation(
            Func<Task>? execute = null,
            Action<IconLoadPriority, int>? enqueued = null)
        {
            _execute = execute ?? (() => Task.CompletedTask);
            _enqueued = enqueued;
        }

        public Task<Exception> Failure => _failure.Task;

        public int FailureCount => Volatile.Read(ref _failureCount);

        public override void Enqueued(IconLoadPriority priority, int workerCount) => _enqueued?.Invoke(priority, workerCount);

        public override Task ExecuteAsync() => _execute();

        public override void Fail(Exception failure)
        {
            Interlocked.Increment(ref _failureCount);
            _failure.TrySetResult(failure);
        }
    }
}
