// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;
using Moq;
using static Microsoft.CmdPal.Common.UnitTests.Helpers.CoalescingAsyncRunnerTestHelper;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public sealed class CoalescingAsyncRunnerTests
{
    private static readonly bool[] BusyCycle = [true, false];
    private static readonly bool[] TwoBusyCycles = [true, false, true, false];

    [TestMethod]
    public async Task Dispose_Idle_IsTerminalAndIdempotent()
    {
        var calls = 0;
        using var runner = new CoalescingAsyncRunner(() =>
        {
            Interlocked.Increment(ref calls);
            return Task.CompletedTask;
        });
        Assert.IsFalse(runner.IsBusy);
        Assert.IsTrue(runner.WhenIdleAsync().IsCompletedSuccessfully);
        runner.Dispose();
        runner.Dispose();
        await runner.DisposeAsync();
        Assert.IsTrue(runner.RequestAsync().IsCanceled);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task RequestAsync_CompletionContinuation_CanWaitForNextRun()
    {
        var started = NewSignal();
        var release = NewSignal();
        var calls = 0;
        using var runner = new CoalescingAsyncRunner(() =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                started.SetResult();
                return release.Task;
            }

            return Task.CompletedTask;
        });
        var first = runner.RequestAsync();
        var continuation = first.ContinueWith(
            _ => runner.RequestAsync().Wait(WaitTimeout),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            release.SetResult();
            Assert.IsTrue(await continuation.WaitAsync(WaitTimeout + WaitTimeout));
            Assert.AreEqual(2, calls);
        }
        finally
        {
            release.TrySetResult();
            await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public void Constructor_NullAction_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => _ = new CoalescingAsyncRunner(null!));
        Assert.AreEqual("action", exception.ParamName);
    }

    [TestMethod]
    public async Task RequestAsync_SynchronousAction_DoesNotBlockRequestingThread()
    {
        var started = NewSignal();
        using var release = new ManualResetEventSlim();
        var submitted = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var runner = new CoalescingAsyncRunner(() =>
        {
            started.SetResult();
            release.Wait();
            return Task.CompletedTask;
        });
        var caller = Task.Run(() => submitted.SetResult(runner.RequestAsync()));
        try
        {
            var request = await submitted.Task.WaitAsync(WaitTimeout);
            await started.Task.WaitAsync(WaitTimeout);
            Assert.IsTrue(runner.IsBusy);
            Assert.IsFalse(request.IsCompleted);
            release.Set();
            await request.WaitAsync(WaitTimeout);
        }
        finally
        {
            release.Set();
            await caller.WaitAsync(WaitTimeout);
            await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task RequestAsync_BeforeRunStarts_SharesBatchAndReportsBusyImmediately()
    {
        var notified = NewSignal();
        using var release = new ManualResetEventSlim();
        var calls = 0;
        using var runner = new CoalescingAsyncRunner(() =>
        {
            Interlocked.Increment(ref calls);
            return Task.CompletedTask;
        });
        runner.BusyChanged += busy =>
        {
            if (busy)
            {
                notified.SetResult();
                release.Wait();
            }
        };
        var first = runner.RequestAsync();
        try
        {
            Assert.IsTrue(runner.IsBusy);
            Assert.IsFalse(runner.WhenIdleAsync().IsCompleted);
            await notified.Task.WaitAsync(WaitTimeout);
            var second = runner.RequestAsync();
            Assert.AreSame(first, second);
            Assert.AreEqual(0, calls);
            release.Set();
            await Task.WhenAll(first, second).WaitAsync(WaitTimeout);
            await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
            Assert.AreEqual(1, calls);
            Assert.IsFalse(runner.IsBusy);
            Assert.IsTrue(runner.WhenIdleAsync().IsCompletedSuccessfully);
        }
        finally
        {
            release.Set();
            await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task RequestAsync_Bursts_SerializeBatchesWithoutPostponingEarlierRequests()
    {
        var starts = new[] { NewSignal(), NewSignal(), NewSignal() };
        var releases = new[] { NewSignal(), NewSignal(), NewSignal() };
        var transitions = new List<bool>();
        var calls = 0;
        var active = 0;
        var overlap = false;
        using var runner = new CoalescingAsyncRunner(async () =>
        {
            overlap |= Interlocked.Increment(ref active) != 1;
            var index = Interlocked.Increment(ref calls) - 1;
            starts[index].SetResult();
            await releases[index].Task.ConfigureAwait(false);
            Interlocked.Decrement(ref active);
        });
        runner.BusyChanged += transitions.Add;
        var first = runner.RequestAsync();
        try
        {
            await starts[0].Task.WaitAsync(WaitTimeout);
            var second = runner.RequestAsync();
            var alsoSecond = runner.RequestAsync();
            Assert.AreSame(second, alsoSecond);
            Assert.AreNotSame(first, second);
            releases[0].SetResult();
            await first.WaitAsync(WaitTimeout);
            await starts[1].Task.WaitAsync(WaitTimeout);
            var third = runner.RequestAsync();
            Assert.IsFalse(second.IsCompleted);
            Assert.IsTrue(runner.IsBusy);
            releases[1].SetResult();
            await Task.WhenAll(second, alsoSecond).WaitAsync(WaitTimeout);
            await starts[2].Task.WaitAsync(WaitTimeout);
            Assert.IsFalse(third.IsCompleted);
            Assert.IsTrue(runner.IsBusy);
            releases[2].SetResult();
            await third.WaitAsync(WaitTimeout);
            await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
            Assert.AreEqual(3, calls);
            Assert.IsFalse(overlap);
            CollectionAssert.AreEqual(BusyCycle, transitions);
        }
        finally
        {
            foreach (var release in releases)
            {
                release.TrySetResult();
            }

            await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RequestAsync_FailedBatch_PreservesOutcomeAndRunsPendingBatch(bool canceled)
    {
        var started = NewSignal();
        var release = NewSignal();
        var calls = 0;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = new InvalidOperationException("Run failed.");
        using var runner = new CoalescingAsyncRunner(() =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                started.SetResult();
                return release.Task;
            }

            return Task.CompletedTask;
        });
        var first = runner.RequestAsync();
        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            var next = runner.RequestAsync();
            if (canceled)
            {
                release.SetCanceled(cancellation.Token);
                var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
                Assert.AreEqual(cancellation.Token, exception.CancellationToken);
                Assert.IsTrue(first.IsCanceled);
            }
            else
            {
                release.SetException(failure);
                var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => first.WaitAsync(WaitTimeout));
                Assert.AreSame(failure, exception);
            }

            await next.WaitAsync(WaitTimeout);
            await runner.WhenIdleAsync().WaitAsync(WaitTimeout);
            Assert.AreEqual(2, calls);
            Assert.IsTrue(next.IsCompletedSuccessfully);
        }
        finally
        {
            release.TrySetResult();
            await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task RequestAsync_SynchronousFailure_FaultsRequestAndCanRestart()
    {
        var failure = new InvalidOperationException("Synchronous failure.");
        var calls = 0;
        using var runner = new CoalescingAsyncRunner(() => ++calls == 1 ? throw failure : Task.CompletedTask);
        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => runner.RequestAsync().WaitAsync(WaitTimeout));
        Assert.AreSame(failure, actual);
        await runner.WhenIdleAsync().WaitAsync(WaitTimeout);
        await runner.RequestAsync().WaitAsync(WaitTimeout);
        await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task BusyChanged_RequestDuringIdleNotification_IsNotLost(bool concurrent)
    {
        var idleEntered = NewSignal();
        using var releaseIdle = new ManualResetEventSlim();
        var replacement = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var transitions = new List<bool>();
        var calls = 0;
        using var runner = new CoalescingAsyncRunner(() =>
        {
            Interlocked.Increment(ref calls);
            return Task.CompletedTask;
        });
        runner.BusyChanged += busy =>
        {
            transitions.Add(busy);
            if (!busy && transitions.Count == 2)
            {
                if (!concurrent)
                {
                    replacement.SetResult(runner.RequestAsync());
                }

                idleEntered.SetResult();
                releaseIdle.Wait();
            }
        };
        try
        {
            await runner.RequestAsync().WaitAsync(WaitTimeout);
            await idleEntered.Task.WaitAsync(WaitTimeout);
            if (concurrent)
            {
                replacement.SetResult(runner.RequestAsync());
            }

            var next = await replacement.Task.WaitAsync(WaitTimeout);
            Assert.IsTrue(runner.IsBusy);
            Assert.IsFalse(next.IsCompleted);
            releaseIdle.Set();
            await next.WaitAsync(WaitTimeout);
            await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
            Assert.AreEqual(2, calls);
            CollectionAssert.AreEqual(TwoBusyCycles, transitions);
        }
        finally
        {
            releaseIdle.Set();
            await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    [DataRow(false)]
    [DataRow(true)]
    public async Task BusyChanged_ThrowingObserver_LogsFailureAndDrainsEvenIfLoggerThrows(bool loggerThrows)
    {
        var previousLogger = CoreLogger.Instance;
        var logger = new Mock<ILogger>();
        var failure = new InvalidOperationException("Observer failed.");
        logger.Setup(l => l.LogError(It.IsAny<string>(), failure, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Callback(() =>
            {
                if (loggerThrows)
                {
                    throw new InvalidOperationException("Logging failed.");
                }
            });
        var transitions = new List<bool>();
        var calls = 0;
        using var runner = new CoalescingAsyncRunner(() =>
        {
            Interlocked.Increment(ref calls);
            return Task.CompletedTask;
        });
        runner.BusyChanged += _ => throw failure;
        runner.BusyChanged += transitions.Add;
        CoreLogger.InitializeLogger(logger.Object);
        try
        {
            await runner.RequestAsync().WaitAsync(WaitTimeout);
            await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
            Assert.AreEqual(1, calls);
            CollectionAssert.AreEqual(BusyCycle, transitions);
            logger.Verify(l => l.LogError(It.IsAny<string>(), failure, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Exactly(2));
        }
        finally
        {
            CoreLogger.InitializeLogger(previousLogger!);
        }
    }

    [TestMethod]
    public async Task DisposeAsync_DrainsAcceptedBatchesAndFinalNotification_RejectsNewRequests()
    {
        var started = NewSignal();
        var release = NewSignal();
        var idleEntered = NewSignal();
        using var releaseIdle = new ManualResetEventSlim();
        var calls = 0;
        using var runner = new CoalescingAsyncRunner(() =>
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            return release.Task;
        });
        runner.BusyChanged += busy =>
        {
            if (!busy)
            {
                idleEntered.SetResult();
                releaseIdle.Wait();
            }
        };
        var first = runner.RequestAsync();
        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            var second = runner.RequestAsync();
            runner.Dispose();
            runner.Dispose();
            var disposed = runner.DisposeAsync().AsTask();
            var disposedAgain = runner.DisposeAsync().AsTask();
            Assert.IsFalse(disposed.IsCompleted);
            Assert.IsTrue(runner.RequestAsync().IsCanceled);
            release.SetResult();
            await Task.WhenAll(first, second).WaitAsync(WaitTimeout);
            await idleEntered.Task.WaitAsync(WaitTimeout);
            Assert.IsTrue(runner.WhenIdleAsync().IsCompletedSuccessfully);
            Assert.IsFalse(disposed.IsCompleted);
            Assert.IsFalse(disposedAgain.IsCompleted);
            releaseIdle.Set();
            await Task.WhenAll(disposed, disposedAgain).WaitAsync(WaitTimeout);
            Assert.AreEqual(2, calls);
            Assert.IsFalse(runner.IsBusy);
        }
        finally
        {
            release.TrySetResult();
            releaseIdle.Set();
            await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task RequestAsync_CanceledWaiter_DoesNotCancelSharedRun()
    {
        var started = NewSignal();
        var release = NewSignal();
        using var cancellation = new CancellationTokenSource();
        using var runner = new CoalescingAsyncRunner(() =>
        {
            started.SetResult();
            return release.Task;
        });
        var request = runner.RequestAsync();
        var waiter = request.WaitAsync(cancellation.Token);
        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => waiter.WaitAsync(WaitTimeout));
            Assert.IsFalse(request.IsCompleted);
            Assert.IsTrue(runner.IsBusy);
            release.SetResult();
            await request.WaitAsync(WaitTimeout);
            Assert.IsTrue(request.IsCompletedSuccessfully);
        }
        finally
        {
            release.TrySetResult();
            await runner.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }
}
