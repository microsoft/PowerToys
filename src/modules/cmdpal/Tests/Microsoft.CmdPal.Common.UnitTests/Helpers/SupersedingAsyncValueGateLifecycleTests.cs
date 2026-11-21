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
    public async Task ExecuteAsync_CompletionContinuation_CanWaitForNextRun()
    {
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
        var first = gate.ExecuteAsync();
        var continuation = first.ContinueWith(
            _ => gate.ExecuteAsync().Wait(WaitTimeout),
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
            await gate.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task Dispose_DuringApplication_WaitsForCommittedApplication()
    {
        var applyStarted = NewSignal();
        using var releaseApply = new ManualResetEventSlim();
        var disposeStarted = NewSignal();
        var applied = 0;
        using var gate = new SupersedingAsyncValueGate<int>(
            _ => Task.FromResult(42),
            _ =>
            {
                applyStarted.SetResult();
                releaseApply.Wait();
                Interlocked.Increment(ref applied);
            });
        var request = gate.ExecuteAsync();
        Task? disposal = null;
        try
        {
            await applyStarted.Task.WaitAsync(WaitTimeout);
            disposal = Task.Run(() =>
            {
                disposeStarted.SetResult();
                gate.Dispose();
            });
            await disposeStarted.Task.WaitAsync(WaitTimeout);
            await Assert.ThrowsExactlyAsync<TimeoutException>(() => disposal.WaitAsync(TimeSpan.FromMilliseconds(100)));
            releaseApply.Set();
            await disposal.WaitAsync(WaitTimeout);
            Assert.AreEqual(1, applied);
            Assert.AreEqual(42, await request.WaitAsync(WaitTimeout));
            await Assert.ThrowsAsync<OperationCanceledException>(() => gate.ExecuteAsync());
        }
        finally
        {
            releaseApply.Set();
            if (disposal is not null)
            {
                await disposal.WaitAsync(WaitTimeout);
            }

            await gate.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task Dispose_DuringApplication_CancelsPendingAndRejectsNewRequests()
    {
        var applyStarted = NewSignal();
        using var releaseApply = new ManualResetEventSlim();
        var calls = 0;
        using var gate = new SupersedingAsyncValueGate<int>(
            _ => Task.FromResult(Interlocked.Increment(ref calls)),
            _ =>
            {
                applyStarted.TrySetResult();
                releaseApply.Wait();
            });
        var request = gate.ExecuteAsync();
        Task? disposal = null;
        try
        {
            await applyStarted.Task.WaitAsync(WaitTimeout);
            var pending = gate.ExecuteAsync();
            disposal = Task.Run(gate.Dispose);

            await Assert.ThrowsAsync<OperationCanceledException>(() => pending.WaitAsync(WaitTimeout));
            Assert.IsTrue(pending.IsCanceled);
            Assert.IsFalse(disposal.IsCompleted);
            Assert.IsFalse(request.IsCompleted);
            Assert.IsTrue(gate.ExecuteAsync().IsCanceled);

            releaseApply.Set();
            await disposal.WaitAsync(WaitTimeout);
            Assert.AreEqual(1, await request.WaitAsync(WaitTimeout));
            Assert.AreEqual(1, calls);
        }
        finally
        {
            releaseApply.Set();
            if (disposal is not null)
            {
                await disposal.WaitAsync(WaitTimeout);
            }

            await gate.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task DisposeAsync_DuringApplication_ReturnsBeforeApplicationFinishes()
    {
        var applyStarted = NewSignal();
        using var releaseApply = new ManualResetEventSlim();
        var returned = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var gate = new SupersedingAsyncValueGate<int>(
            _ => Task.FromResult(Interlocked.Increment(ref calls)),
            _ =>
            {
                applyStarted.SetResult();
                releaseApply.Wait();
            });
        var request = gate.ExecuteAsync();
        Task? invocation = null;
        try
        {
            await applyStarted.Task.WaitAsync(WaitTimeout);
            var pending = gate.ExecuteAsync();
            invocation = Task.Run(() =>
            {
                returned.SetResult(gate.DisposeAsync().AsTask());
            });
            var disposal = await returned.Task.WaitAsync(WaitTimeout);
            Assert.IsFalse(disposal.IsCompleted);
            Assert.IsFalse(request.IsCompleted);
            Assert.AreSame(disposal, gate.DisposeAsync().AsTask());
            await Assert.ThrowsAsync<OperationCanceledException>(() => pending.WaitAsync(WaitTimeout));
            Assert.IsTrue(gate.ExecuteAsync().IsCanceled);
            releaseApply.Set();
            await disposal.WaitAsync(WaitTimeout);
            Assert.AreEqual(1, await request.WaitAsync(WaitTimeout));
            Assert.AreEqual(1, calls);
        }
        finally
        {
            releaseApply.Set();
            if (invocation is not null)
            {
                await invocation.WaitAsync(WaitTimeout);
            }

            await gate.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task Apply_ReentersAsyncDisposal_DrainsAfterApplication()
    {
        SupersedingAsyncValueGate<int>? gate = null;
        Task? disposal = null;
        gate = new SupersedingAsyncValueGate<int>(_ => Task.FromResult(42), _ =>
        {
            disposal = gate!.DisposeAsync().AsTask();
            Assert.IsFalse(disposal.IsCompleted);
            Assert.IsTrue(gate.ExecuteAsync().IsCanceled);
        });
        using (gate)
        {
            Assert.AreEqual(42, await gate.ExecuteAsync().WaitAsync(WaitTimeout));
            Assert.IsNotNull(disposal);
            await disposal.WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancellationCallback_CompletesFactoryInline_CannotPublishObsoleteValue(bool dispose)
    {
        var started = NewSignal();
        var result = new TaskCompletionSource<int>();
        var applied = new List<int>();
        var calls = 0;
        using var gate = new SupersedingAsyncValueGate<int>(
            token =>
            {
                if (Interlocked.Increment(ref calls) != 1)
                {
                    return Task.FromResult(20);
                }

                token.Register(() => result.TrySetResult(10));
                started.SetResult();
                return result.Task;
            },
            applied.Add);
        var first = gate.ExecuteAsync();
        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            if (dispose)
            {
                gate.Dispose();
                await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
            }
            else
            {
                var next = gate.ExecuteAsync();
                await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
                Assert.AreEqual(20, await next.WaitAsync(WaitTimeout));
            }

            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
            Assert.HasCount(dispose ? 0 : 1, applied);
            if (!dispose)
            {
                Assert.AreEqual(20, applied[0]);
            }
        }
        finally
        {
            result.TrySetResult(10);
            await gate.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancellationCallback_SupersedeOrDispose_DoesNotHoldSchedulingLock(bool dispose)
    {
        var started = NewSignal();
        var release = NewSignal();
        var callbackFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SupersedingAsyncValueGate<int>? gate = null;
        gate = new SupersedingAsyncValueGate<int>(
            async token =>
            {
                using var registration = token.Register(() =>
                {
                    var readState = Task.Run(() =>
                    {
                        _ = gate!.WhenIdleAsync();
                    });
                    callbackFinished.TrySetResult(readState.Wait(WaitTimeout));
                });
                started.TrySetResult();
                await release.Task.ConfigureAwait(false);
                return 42;
            },
            _ => { });
        using (gate)
        {
            var first = gate.ExecuteAsync();
            Task<int>? next = null;
            try
            {
                await started.Task.WaitAsync(WaitTimeout);
                if (dispose)
                {
                    gate.Dispose();
                    await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
                }
                else
                {
                    next = gate.ExecuteAsync();
                    await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
                }

                Assert.IsTrue(await callbackFinished.Task.WaitAsync(WaitTimeout + WaitTimeout));
                release.SetResult();
                if (next is not null)
                {
                    Assert.AreEqual(42, await next.WaitAsync(WaitTimeout));
                }
            }
            finally
            {
                release.TrySetResult();
                await gate.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancellationCallback_Throws_DoesNotStrandReplacementOrDisposal(bool dispose)
    {
        var started = NewSignal();
        var release = NewSignal();
        var callbackEntered = NewSignal();
        var calls = 0;
        using var gate = new SupersedingAsyncValueGate<int>(
            async token =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    using var registration = token.Register(() =>
                    {
                        callbackEntered.SetResult();
                        throw new InvalidOperationException("Cancellation callback failed.");
                    });
                    started.SetResult();
                    await release.Task.ConfigureAwait(false);
                }

                return 42;
            },
            _ => { });
        var first = gate.ExecuteAsync();
        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            Task<int>? next = null;
            if (dispose)
            {
                gate.Dispose();
                await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
            }
            else
            {
                next = gate.ExecuteAsync();
                await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
            }

            await callbackEntered.Task.WaitAsync(WaitTimeout);
            release.SetResult();
            if (next is not null)
            {
                Assert.AreEqual(42, await next.WaitAsync(WaitTimeout));
            }

            await gate.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
            Assert.AreEqual(dispose ? 1 : 2, calls);
        }
        finally
        {
            release.TrySetResult();
            await gate.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task DisposeAsync_WaitsForNonCooperativeFactoryAndCallbacks_WithoutApplyingResult()
    {
        var started = NewSignal();
        var releaseFactory = NewSignal();
        var callbackEntered = NewSignal();
        using var releaseCallback = new ManualResetEventSlim();
        var applied = 0;
        using var gate = new SupersedingAsyncValueGate<int>(
            token =>
            {
                token.Register(() =>
                {
                    callbackEntered.SetResult();
                    releaseCallback.Wait();
                });
                started.SetResult();
                return CompleteFactoryAsync();
            },
            _ => Interlocked.Increment(ref applied));
        var request = gate.ExecuteAsync();
        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            var disposal = gate.DisposeAsync().AsTask();
            var secondDisposal = gate.DisposeAsync().AsTask();
            await callbackEntered.Task.WaitAsync(WaitTimeout);
            await Assert.ThrowsAsync<OperationCanceledException>(() => request.WaitAsync(WaitTimeout));
            Assert.IsTrue(request.IsCanceled);
            Assert.IsFalse(disposal.IsCompleted);
            releaseFactory.SetResult();
            Assert.IsFalse(disposal.IsCompleted);
            Assert.IsFalse(secondDisposal.IsCompleted);
            releaseCallback.Set();
            await Task.WhenAll(disposal, secondDisposal).WaitAsync(WaitTimeout);
            Assert.AreEqual(0, applied);
            await Assert.ThrowsAsync<OperationCanceledException>(() => gate.ExecuteAsync());
        }
        finally
        {
            releaseFactory.TrySetResult();
            releaseCallback.Set();
            await gate.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        }

        async Task<int> CompleteFactoryAsync()
        {
            await releaseFactory.Task.ConfigureAwait(false);
            return 42;
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_FactoryCancellation_PreservesOriginalToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var gate = new SupersedingAsyncValueGate<int>(_ => Task.FromCanceled<int>(cancellation.Token), _ => Assert.Fail("Canceled values cannot be applied."));
        var request = gate.ExecuteAsync();
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => request.WaitAsync(WaitTimeout));
        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsTrue(request.IsCanceled);
        await gate.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Apply_ReentersGate_CompletesCommittedApplication(bool dispose)
    {
        SupersedingAsyncValueGate<int>? gate = null;
        Task<int>? next = null;
        var applied = new List<int>();
        var calls = 0;
        gate = new SupersedingAsyncValueGate<int>(_ => Task.FromResult(++calls), value =>
        {
            applied.Add(value);
            if (value == 1)
            {
                if (dispose)
                {
                    gate!.Dispose();
                }
                else
                {
                    next = gate!.ExecuteAsync();
                }
            }
        });
        using (gate)
        {
            Assert.AreEqual(1, await gate.ExecuteAsync().WaitAsync(WaitTimeout));
            if (!dispose)
            {
                Assert.IsNotNull(next);
                Assert.AreEqual(2, await next.WaitAsync(WaitTimeout));
            }

            await gate.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
            Assert.HasCount(dispose ? 1 : 2, applied);
            Assert.AreEqual(1, applied[0]);
            if (!dispose)
            {
                Assert.AreEqual(2, applied[1]);
            }
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_SequentialRestarts_CompleteEveryRequest()
    {
        var applied = 0;
        using var gate = new SupersedingAsyncValueGate<int>(_ => Task.FromResult(42), _ => Interlocked.Increment(ref applied));
        for (var i = 0; i < 1000; i++)
        {
            Assert.AreEqual(42, await gate.ExecuteAsync().WaitAsync(WaitTimeout));
        }

        await gate.DisposeAsync().AsTask().WaitAsync(WaitTimeout);
        Assert.AreEqual(1000, applied);
    }
}
