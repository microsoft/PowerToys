// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public sealed class SupersedingAsyncGateTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task ExecuteAsync_QueuedCallsCoalesceOnWorkerScheduler()
    {
        var scheduler = new QueuedScheduler();
        var calls = 0;
        using var gate = new SupersedingAsyncGate(
            _ =>
            {
                Assert.AreSame(scheduler, TaskScheduler.Current);
                calls++;
                return Task.CompletedTask;
            },
            scheduler);

        var first = gate.ExecuteAsync();
        var second = gate.ExecuteAsync();
        var latest = gate.ExecuteAsync();

        Assert.AreEqual(0, calls);
        Assert.AreEqual(1, scheduler.Count);
        scheduler.ExecuteAll();
        await latest.WaitAsync(Timeout);

        await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));
        await Assert.ThrowsAsync<OperationCanceledException>(() => second.WaitAsync(Timeout));
        Assert.IsTrue(first.IsCanceled);
        Assert.IsTrue(second.IsCanceled);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_RunningCallsStaySerializedAndSkipSupersededWork()
    {
        var scheduler = new QueuedScheduler();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var runningToken = CancellationToken.None;
        using var gate = new SupersedingAsyncGate(
            token =>
            {
                calls++;
                if (calls == 1)
                {
                    runningToken = token;
                    return release.Task;
                }

                return Task.CompletedTask;
            },
            scheduler);

        try
        {
            var first = gate.ExecuteAsync();
            scheduler.ExecuteAll();
            var second = gate.ExecuteAsync();
            var latest = gate.ExecuteAsync();

            Assert.IsTrue(runningToken.IsCancellationRequested);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, scheduler.Count);
            Assert.IsFalse(latest.IsCompleted);
            release.SetResult();
            await latest.WaitAsync(Timeout);

            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));
            await Assert.ThrowsAsync<OperationCanceledException>(() => second.WaitAsync(Timeout));
            Assert.AreEqual(2, calls);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ReentrantRequestSurvivesPreviousCompletion()
    {
        var scheduler = new QueuedScheduler();
        var calls = 0;
        Task? latest = null;
        SupersedingAsyncGate? gate = null;
        gate = new(
            _ =>
            {
                if (++calls == 1)
                {
                    latest = gate!.ExecuteAsync(CancellationToken.None);
                }

                return Task.CompletedTask;
            },
            scheduler);

        using (gate)
        {
            var first = gate.ExecuteAsync();
            scheduler.ExecuteAll();

            Assert.IsNotNull(latest);
            await latest.WaitAsync(Timeout);
            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));
            Assert.AreEqual(2, calls);
            Assert.AreEqual(0, scheduler.Count);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_CancellationCallbackCanSupersedeTheCancelingRequest()
    {
        var scheduler = new QueuedScheduler();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runningToken = CancellationToken.None;
        var calls = 0;
        using var gate = new SupersedingAsyncGate(
            token =>
            {
                if (++calls == 1)
                {
                    runningToken = token;
                    return release.Task;
                }

                return Task.CompletedTask;
            },
            scheduler);

        try
        {
            var first = gate.ExecuteAsync();
            scheduler.ExecuteAll();
            Task? latest = null;
            using var registration = runningToken.Register(() => latest = gate.ExecuteAsync());

            var superseded = gate.ExecuteAsync();
            Assert.IsNotNull(latest);
            release.SetResult();
            await latest.WaitAsync(Timeout);

            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));
            await Assert.ThrowsAsync<OperationCanceledException>(() => superseded.WaitAsync(Timeout));
            Assert.AreEqual(2, calls);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [TestMethod]
    public async Task Dispose_FromCancellationCallbackCannotRevivePendingWork()
    {
        var scheduler = new QueuedScheduler();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runningToken = CancellationToken.None;
        var calls = 0;
        using var gate = new SupersedingAsyncGate(
            token =>
            {
                calls++;
                runningToken = token;
                return release.Task;
            },
            scheduler);

        try
        {
            var first = gate.ExecuteAsync();
            scheduler.ExecuteAll();
            using var registration = runningToken.Register(gate.Dispose);

            var rejected = gate.ExecuteAsync();
            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => rejected.WaitAsync(Timeout));
            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));
            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => gate.ExecuteAsync());
            Assert.AreEqual(1, calls);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_CompletedWorkerCanRestart()
    {
        var scheduler = new QueuedScheduler();
        var calls = 0;
        using var gate = new SupersedingAsyncGate(
            _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            scheduler);

        for (var i = 1; i <= 3; i++)
        {
            var execution = gate.ExecuteAsync();
            Assert.AreEqual(1, scheduler.Count);
            scheduler.ExecuteAll();
            await execution.WaitAsync(Timeout);
            Assert.AreEqual(i, calls);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_FailureReachesCallerAndAllowsNextRequest(bool throwSynchronously)
    {
        var scheduler = new QueuedScheduler();
        var failure = new InvalidOperationException("The operation failed.");
        var calls = 0;
        using var gate = new SupersedingAsyncGate(
            _ =>
            {
                if (++calls == 1)
                {
                    return throwSynchronously ? throw failure : Task.FromException(failure);
                }

                return Task.CompletedTask;
            },
            scheduler);

        var first = gate.ExecuteAsync();
        scheduler.ExecuteAll();
        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => first.WaitAsync(Timeout));
        Assert.AreSame(failure, actual);

        var next = gate.ExecuteAsync();
        scheduler.ExecuteAll();
        await next.WaitAsync(Timeout);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_CanceledQueuedRequestDoesNotRun()
    {
        var scheduler = new QueuedScheduler();
        var calls = 0;
        using var cancellation = new CancellationTokenSource();
        using var gate = new SupersedingAsyncGate(
            _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            scheduler);

        var canceled = gate.ExecuteAsync(cancellation.Token);
        cancellation.Cancel();
        scheduler.ExecuteAll();

        await Assert.ThrowsAsync<OperationCanceledException>(() => canceled.WaitAsync(Timeout));
        Assert.AreEqual(0, calls);

        var next = gate.ExecuteAsync();
        scheduler.ExecuteAll();
        await next.WaitAsync(Timeout);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_PreCanceledRequestDoesNotSupersedePendingWork()
    {
        var scheduler = new QueuedScheduler();
        var calls = 0;
        using var gate = new SupersedingAsyncGate(
            _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            scheduler);

        var pending = gate.ExecuteAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => gate.ExecuteAsync(new CancellationToken(true)));
        scheduler.ExecuteAll();
        await pending.WaitAsync(Timeout);

        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_ExternalCancellationReachesRunningWork()
    {
        var scheduler = new QueuedScheduler();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runningToken = CancellationToken.None;
        var calls = 0;
        using var cancellation = new CancellationTokenSource();
        using var gate = new SupersedingAsyncGate(
            token =>
            {
                calls++;
                if (calls == 1)
                {
                    runningToken = token;
                    return release.Task;
                }

                Assert.IsFalse(token.IsCancellationRequested);
                return Task.CompletedTask;
            },
            scheduler);

        try
        {
            var first = gate.ExecuteAsync(cancellation.Token);
            scheduler.ExecuteAll();
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));
            Assert.IsTrue(runningToken.IsCancellationRequested);

            var next = gate.ExecuteAsync();
            Assert.AreEqual(1, calls);
            release.SetResult();
            await next.WaitAsync(Timeout);
            Assert.AreEqual(2, calls);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ExternalCallbackCanReenterDuringSourceDisposal()
    {
        var scheduler = new QueuedScheduler();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runningToken = CancellationToken.None;
        var calls = 0;
        using var cancellation = new CancellationTokenSource();
        using var gate = new SupersedingAsyncGate(
            token =>
            {
                if (++calls == 1)
                {
                    runningToken = token;
                    return release.Task;
                }

                return Task.CompletedTask;
            },
            scheduler);

        // Observe cleanup directly so this race does not depend on thread timing.
        var sourceField = typeof(SupersedingAsyncGate).GetField("_currentCancellationSource", BindingFlags.Instance | BindingFlags.NonPublic);
        var lockField = typeof(SupersedingAsyncGate).GetField("_lock", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(sourceField);
        Assert.IsNotNull(lockField);
        Assert.IsInstanceOfType<Lock>(lockField.GetValue(gate), out var gateLock);

        var first = gate.ExecuteAsync(cancellation.Token);
        scheduler.ExecuteAll();
        Task? latest = null;
        using var registration = runningToken.Register(() =>
        {
            callbackEntered.SetResult();
            Assert.IsTrue(
                SpinWait.SpinUntil(() => sourceField.GetValue(gate) is null, Timeout),
                "Cleanup must retire the source while its cancellation callback is still running.");
            Assert.IsTrue(
                gateLock.TryEnter(Timeout),
                "Source disposal must not hold the lock needed by the cancellation callback.");
            try
            {
                latest = gate.ExecuteAsync();
            }
            finally
            {
                gateLock.Exit();
            }
        });
        var cancelTask = Task.Run(cancellation.Cancel);

        try
        {
            await callbackEntered.Task.WaitAsync(Timeout);
            release.SetResult();
            await cancelTask.WaitAsync(Timeout + Timeout);
            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));

            Assert.IsNotNull(latest);
            await latest.WaitAsync(Timeout);
            Assert.AreEqual(2, calls);
        }
        finally
        {
            release.TrySetResult();
            await cancelTask.WaitAsync(Timeout + Timeout);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_OldExternalCancellationDoesNotCancelLatestRequest()
    {
        var scheduler = new QueuedScheduler();
        using var cancellation = new CancellationTokenSource();
        using var gate = new SupersedingAsyncGate(
            token =>
            {
                Assert.IsFalse(token.IsCancellationRequested);
                return Task.CompletedTask;
            },
            scheduler);

        var first = gate.ExecuteAsync(cancellation.Token);
        var latest = gate.ExecuteAsync();
        cancellation.Cancel();
        scheduler.ExecuteAll();

        await latest.WaitAsync(Timeout);
        await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(Timeout));
    }

    [TestMethod]
    public async Task Dispose_RejectsQueuedAndFutureWork()
    {
        var scheduler = new QueuedScheduler();
        var calls = 0;
        using var gate = new SupersedingAsyncGate(
            _ =>
            {
                calls++;
                return Task.CompletedTask;
            },
            scheduler);

        var pending = gate.ExecuteAsync();
        gate.Dispose();
        scheduler.ExecuteAll();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => pending.WaitAsync(Timeout));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => gate.ExecuteAsync());
        gate.Dispose();
        Assert.AreEqual(0, calls);
        Assert.AreEqual(0, scheduler.Count);
    }

    [TestMethod]
    public async Task Dispose_KeepsRunningTokenUsableUntilActionReturns()
    {
        var scheduler = new QueuedScheduler();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var gate = new SupersedingAsyncGate(
            async token =>
            {
                await release.Task.ConfigureAwait(false);
                try
                {
                    Assert.IsTrue(token.IsCancellationRequested);
                    using var registration = token.Register(() => { });
                    Assert.IsNotNull(token.WaitHandle);
                    finished.SetResult();
                }
                catch (Exception ex)
                {
                    finished.SetException(ex);
                    throw;
                }
            },
            scheduler);

        try
        {
            var pending = gate.ExecuteAsync();
            scheduler.ExecuteAll();
            gate.Dispose();
            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => pending.WaitAsync(Timeout));

            release.SetResult();
            await finished.Task.WaitAsync(Timeout);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_SchedulerFailureDoesNotLeaveGateRunning()
    {
        var scheduler = new QueuedScheduler { RejectNextTask = true };
        using var gate = new SupersedingAsyncGate(_ => Task.CompletedTask, scheduler);

        await Assert.ThrowsExactlyAsync<TaskSchedulerException>(() => gate.ExecuteAsync());
        var next = gate.ExecuteAsync();
        scheduler.ExecuteAll();
        await next.WaitAsync(Timeout);
    }

    private sealed class QueuedScheduler : TaskScheduler
    {
        private readonly Queue<Task> _tasks = [];

        internal int Count => _tasks.Count;

        internal bool RejectNextTask { get; set; }

        protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

        protected override void QueueTask(Task task)
        {
            if (RejectNextTask)
            {
                RejectNextTask = false;
                throw new InvalidOperationException("The scheduler rejected the task.");
            }

            _tasks.Enqueue(task);
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        internal void ExecuteAll()
        {
            while (_tasks.TryDequeue(out var task))
            {
                Assert.IsTrue(TryExecuteTask(task));
                task.GetAwaiter().GetResult();
            }
        }
    }
}
