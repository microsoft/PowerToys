// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;
using static Microsoft.CmdPal.Common.UnitTests.Helpers.SupersedingAsyncValueGateTestHelper;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

public sealed partial class SupersedingAsyncValueGateTests
{
    [TestMethod]
    public async Task ExecuteAsync_CompletedAction_ExecutesOnce()
    {
        var calls = 0;
        using var gate = CreateGate(_ =>
        {
            Interlocked.Increment(ref calls);
            return Task.CompletedTask;
        });

        await gate.ExecuteAsync().WaitAsync(WaitTimeout);
        await gate.WhenIdleAsync().WaitAsync(WaitTimeout);

        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_PendingAction_WaitsForActionCompletion()
    {
        var started = NewSignal();
        var release = NewSignal();
        using var gate = CreateGate(_ =>
        {
            started.TrySetResult();
            return release.Task;
        });
        var request = gate.ExecuteAsync();

        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            Assert.IsFalse(request.IsCompleted);
            release.SetResult();
            await request.WaitAsync(WaitTimeout);
            Assert.IsTrue(request.IsCompletedSuccessfully);
        }
        finally
        {
            release.TrySetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_Burst_SupersedesEarlierRequestsAndSerializesLatest()
    {
        var started = NewSignal();
        var release = NewSignal();
        var calls = 0;
        var active = 0;
        var overlap = false;
        CancellationToken firstToken = default;
        using var gate = CreateGate(async token =>
        {
            if (Interlocked.Increment(ref active) != 1)
            {
                overlap = true;
            }

            try
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    firstToken = token;
                    started.SetResult();
                    await release.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        });
        var first = gate.ExecuteAsync();

        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            var middle = gate.ExecuteAsync();
            var latest = gate.ExecuteAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
            await Assert.ThrowsAsync<OperationCanceledException>(() => middle.WaitAsync(WaitTimeout));
            Assert.IsTrue(first.IsCanceled);
            Assert.IsTrue(middle.IsCanceled);
            Assert.IsTrue(firstToken.IsCancellationRequested);
            Assert.IsFalse(latest.IsCompleted);
            Assert.AreEqual(1, Volatile.Read(ref calls));

            release.SetResult();
            await latest.WaitAsync(WaitTimeout);

            Assert.AreEqual(2, calls);
            Assert.IsFalse(overlap);
            Assert.AreEqual(0, active);
        }
        finally
        {
            release.TrySetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_ActionFails_PropagatesExceptionAndCanRunAgain(bool asynchronousFailure)
    {
        var started = NewSignal();
        var failure = NewSignal();
        var expected = new InvalidOperationException("Action failed.");
        var calls = 0;
        using var gate = CreateGate(_ =>
        {
            if (Interlocked.Increment(ref calls) != 1)
            {
                return Task.CompletedTask;
            }

            started.SetResult();
            return asynchronousFailure ? failure.Task : throw expected;
        });
        var first = gate.ExecuteAsync();

        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            if (asynchronousFailure)
            {
                failure.SetException(expected);
            }

            var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => first.WaitAsync(WaitTimeout));
            Assert.AreSame(expected, actual);
            Assert.IsTrue(first.IsFaulted);
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);

            await gate.ExecuteAsync().WaitAsync(WaitTimeout);
            Assert.AreEqual(2, calls);
        }
        finally
        {
            failure.TrySetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ActionCanceled_ReturnsCanceledTask()
    {
        using var gate = CreateGate(_ => Task.FromCanceled(new CancellationToken(canceled: true)));
        var request = gate.ExecuteAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => request.WaitAsync(WaitTimeout));
        await gate.WhenIdleAsync().WaitAsync(WaitTimeout);

        Assert.IsTrue(request.IsCanceled);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_SupersededActionFails_DoesNotFailLatestRequest(bool canceledAction)
    {
        var started = NewSignal();
        var release = NewSignal();
        var calls = 0;
        using var gate = CreateGate(_ =>
        {
            if (Interlocked.Increment(ref calls) != 1)
            {
                return Task.CompletedTask;
            }

            started.SetResult();
            return release.Task;
        });
        var first = gate.ExecuteAsync();

        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            var latest = gate.ExecuteAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));

            if (canceledAction)
            {
                release.SetCanceled();
            }
            else
            {
                release.SetException(new InvalidOperationException("Superseded action failed."));
            }

            await latest.WaitAsync(WaitTimeout);
            Assert.IsTrue(latest.IsCompletedSuccessfully);
            Assert.AreEqual(2, calls);
        }
        finally
        {
            release.TrySetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_ExternalCancellation_CancelsWaitButAllowsWorkToFinish(bool canceledBeforeCall)
    {
        using var cancellation = new CancellationTokenSource();
        var started = NewSignal();
        var release = NewSignal();
        var workFinished = false;
        CancellationToken workToken = default;
        using var gate = CreateGate(async token =>
        {
            workToken = token;
            started.SetResult();
            await release.Task.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            workFinished = true;
        });

        if (canceledBeforeCall)
        {
            cancellation.Cancel();
        }

        var request = gate.ExecuteAsync(cancellation.Token);
        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            cancellation.Cancel();
            var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => request.WaitAsync(WaitTimeout));

            Assert.AreEqual(cancellation.Token, exception.CancellationToken);
            Assert.IsFalse(workToken.IsCancellationRequested);
            Assert.IsFalse(workFinished);

            release.SetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);

            Assert.IsTrue(workFinished);
            Assert.IsTrue(request.IsCanceled);
        }
        finally
        {
            release.TrySetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_IndependentWaiterCancels_OtherAwaiterStillCompletes()
    {
        using var cancellation = new CancellationTokenSource();
        var started = NewSignal();
        var release = NewSignal();
        var calls = 0;
        CancellationToken workToken = default;
        using var gate = CreateGate(token =>
        {
            Interlocked.Increment(ref calls);
            workToken = token;
            started.SetResult();
            return release.Task;
        });
        var operation = gate.ExecuteAsync();
        var canceledWaiter = operation.WaitAsync(cancellation.Token);
        var otherWaiter = ObserveOperationAsync(operation);

        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => canceledWaiter.WaitAsync(WaitTimeout));

            Assert.IsFalse(workToken.IsCancellationRequested);
            Assert.IsFalse(operation.IsCompleted);
            Assert.IsFalse(otherWaiter.IsCompleted);

            release.SetResult();
            await otherWaiter.WaitAsync(WaitTimeout);

            Assert.IsTrue(operation.IsCompletedSuccessfully);
            Assert.IsTrue(otherWaiter.IsCompletedSuccessfully);
            Assert.IsTrue(canceledWaiter.IsCanceled);
            Assert.AreEqual(1, calls);
        }
        finally
        {
            release.TrySetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_SupersededWaiterCancels_DoesNotCancelLatestRequest()
    {
        using var cancellation = new CancellationTokenSource();
        var started = NewSignal();
        var release = NewSignal();
        var calls = 0;
        CancellationToken latestToken = default;
        using var gate = CreateGate(token =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                started.SetResult();
                return release.Task;
            }

            latestToken = token;
            return Task.CompletedTask;
        });
        var first = gate.ExecuteAsync(cancellation.Token);

        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            var latest = gate.ExecuteAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
            cancellation.Cancel();
            release.SetResult();

            await latest.WaitAsync(WaitTimeout);
            Assert.IsFalse(latestToken.IsCancellationRequested);
            Assert.IsTrue(latest.IsCompletedSuccessfully);
            Assert.AreEqual(2, calls);
        }
        finally
        {
            release.TrySetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task ExecuteAsync_CompletionContinuationRequestsNext_DoesNotLoseRequest()
    {
        await Task.Run(async () =>
        {
            using var cleanupCancellation = new CancellationTokenSource();
            var started = NewSignal();
            var release = NewSignal();
            var calls = 0;
            using var gate = CreateGate(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    started.SetResult();
                    return release.Task;
                }

                return Task.CompletedTask;
            });
            var first = gate.ExecuteAsync(cleanupCancellation.Token);
            var next = first.ContinueWith(
                _ => gate.ExecuteAsync(cleanupCancellation.Token),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap();

            try
            {
                await started.Task.WaitAsync(WaitTimeout);
                release.SetResult();
                await next.WaitAsync(WaitTimeout);
                Assert.AreEqual(2, calls);
            }
            finally
            {
                release.TrySetResult();
                cleanupCancellation.Cancel();
                await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
                try
                {
                    await next.WaitAsync(WaitTimeout);
                }
                catch (OperationCanceledException)
                {
                }
            }
        });
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task ExecuteAsync_AfterDispose_ReturnsCanceledTaskWithoutInvokingFactory()
    {
        var calls = 0;
        using var gate = CreateGate(_ =>
        {
            Interlocked.Increment(ref calls);
            return Task.CompletedTask;
        });
        gate.Dispose();

        try
        {
            var rejected = gate.ExecuteAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(() => rejected.WaitAsync(WaitTimeout));
            Assert.IsTrue(rejected.IsCanceled);
            Assert.AreEqual(0, calls);
        }
        finally
        {
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task Dispose_ActiveOperation_CancelsWorkTokenAndRequest()
    {
        var started = NewSignal();
        var release = NewSignal();
        CancellationToken workToken = default;
        using var gate = CreateGate(token =>
        {
            workToken = token;
            started.SetResult();
            return release.Task;
        });
        var request = gate.ExecuteAsync();

        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            gate.Dispose();

            await Assert.ThrowsAsync<OperationCanceledException>(() => request.WaitAsync(WaitTimeout));
            Assert.IsTrue(request.IsCanceled);
            Assert.IsTrue(workToken.IsCancellationRequested);
        }
        finally
        {
            release.TrySetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task Dispose_PendingReplacement_DoesNotStartReplacement()
    {
        var started = NewSignal();
        var release = NewSignal();
        var calls = 0;
        using var gate = CreateGate(_ =>
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            return release.Task;
        });
        var first = gate.ExecuteAsync();

        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            var latest = gate.ExecuteAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
            gate.Dispose();
            await Assert.ThrowsAsync<OperationCanceledException>(() => latest.WaitAsync(WaitTimeout));
            Assert.IsTrue(latest.IsCanceled);
            release.SetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);

            Assert.AreEqual(1, calls);
        }
        finally
        {
            release.TrySetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    [TestCategory("Regression")]
    public async Task Dispose_RepeatedWhileOperationIsActive_DoesNotThrow()
    {
        var started = NewSignal();
        var release = NewSignal();
        using var gate = CreateGate(_ =>
        {
            started.SetResult();
            return release.Task;
        });
        var request = gate.ExecuteAsync();

        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            gate.Dispose();
            gate.Dispose();
        }
        finally
        {
            release.TrySetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
            await Assert.ThrowsAsync<OperationCanceledException>(() => request.WaitAsync(WaitTimeout));
        }
    }

    private static async Task ObserveOperationAsync(Task operation)
    {
        await operation.ConfigureAwait(false);
    }
}
