// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;
using static Microsoft.CmdPal.Common.UnitTests.Helpers.SupersedingAsyncValueGateTestHelper;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public sealed partial class SupersedingAsyncValueGateTests
{
    [TestMethod]
    public void Constructor_NullValueFactory_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => _ = new SupersedingAsyncValueGate<int>(null!, _ => { }));

        Assert.AreEqual("valueFactory", exception.ParamName);
    }

    [TestMethod]
    public void Constructor_NullApply_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => _ = new SupersedingAsyncValueGate<int>(_ => Task.FromResult(42), null!));

        Assert.AreEqual("apply", exception.ParamName);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(42)]
    public async Task ExecuteAsync_ComputedValue_AppliesOnceAndReturnsValue(int value)
    {
        var applied = new List<int>();
        using var gate = new SupersedingAsyncValueGate<int>(_ => Task.FromResult(value), applied.Add);

        var actual = await gate.ExecuteAsync().WaitAsync(WaitTimeout);
        await gate.WhenIdleAsync().WaitAsync(WaitTimeout);

        Assert.AreEqual(value, actual);
        CollectionAssert.AreEqual(new[] { value }, applied);
    }

    [TestMethod]
    public async Task ExecuteAsync_NullValue_AppliesAndReturnsNull()
    {
        var applied = new List<string?>();
        using var gate = new SupersedingAsyncValueGate<string?>(_ => Task.FromResult<string?>(null), applied.Add);

        var actual = await gate.ExecuteAsync().WaitAsync(WaitTimeout);
        await gate.WhenIdleAsync().WaitAsync(WaitTimeout);

        Assert.IsNull(actual);
        Assert.HasCount(1, applied);
        Assert.IsNull(applied[0]);
    }

    [TestMethod]
    public async Task ExecuteAsync_PendingApply_DoesNotCompleteBeforeApplication()
    {
        var applyStarted = NewSignal();
        using var releaseApply = new ManualResetEventSlim();
        var applied = false;
        using var gate = new SupersedingAsyncValueGate<int>(
            _ => Task.FromResult(42),
            _ =>
            {
                applyStarted.SetResult();
                if (!releaseApply.Wait(WaitTimeout))
                {
                    throw new TimeoutException("The test did not release application.");
                }

                applied = true;
            });
        var request = gate.ExecuteAsync();

        try
        {
            await applyStarted.Task.WaitAsync(WaitTimeout);
            Assert.IsFalse(request.IsCompleted);
            Assert.IsFalse(applied);

            releaseApply.Set();
            Assert.AreEqual(42, await request.WaitAsync(WaitTimeout));
            Assert.IsTrue(applied);
        }
        finally
        {
            releaseApply.Set();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ApplyThrows_PropagatesExceptionAndCanRunAgain()
    {
        var expected = new InvalidOperationException("Application failed.");
        var calls = 0;
        using var gate = new SupersedingAsyncValueGate<int>(
            _ => Task.FromResult(42),
            _ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    throw expected;
                }
            });

        var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => gate.ExecuteAsync().WaitAsync(WaitTimeout));
        Assert.AreSame(expected, actual);
        await gate.WhenIdleAsync().WaitAsync(WaitTimeout);

        Assert.AreEqual(42, await gate.ExecuteAsync().WaitAsync(WaitTimeout));
        await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_ApplyThrowsCancellation_FaultsWithOriginalException()
    {
        var expected = new OperationCanceledException("Application failed.", new CancellationToken(canceled: true));
        using var gate = new SupersedingAsyncValueGate<int>(_ => Task.FromResult(42), _ => throw expected);
        var request = gate.ExecuteAsync();

        var actual = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => request.WaitAsync(WaitTimeout));

        Assert.AreSame(expected, actual);
        Assert.IsTrue(request.IsFaulted);
        await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExecuteAsync_FactoryFails_DoesNotApplyValue(bool canceledFactory)
    {
        var applied = new List<int>();
        var expected = new InvalidOperationException("Factory failed.");
        using var gate = new SupersedingAsyncValueGate<int>(
            _ => canceledFactory
                ? Task.FromCanceled<int>(new CancellationToken(canceled: true))
                : Task.FromException<int>(expected),
            applied.Add);
        var request = gate.ExecuteAsync();

        if (canceledFactory)
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => request.WaitAsync(WaitTimeout));
        }
        else
        {
            var actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => request.WaitAsync(WaitTimeout));
            Assert.AreSame(expected, actual);
        }

        await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        Assert.IsEmpty(applied);
    }

    [TestMethod]
    public async Task ExecuteAsync_SupersededFactoryIgnoresCancellation_AppliesOnlyLatestValue()
    {
        var started = NewSignal();
        var release = NewSignal();
        var applied = new List<int>();
        var calls = 0;
        using var gate = new SupersedingAsyncValueGate<int>(
            async _ =>
            {
                var call = Interlocked.Increment(ref calls);
                if (call == 1)
                {
                    started.SetResult();
                    await release.Task.ConfigureAwait(false);
                }

                return call * 10;
            },
            applied.Add);
        var first = gate.ExecuteAsync();

        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            var latest = gate.ExecuteAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(WaitTimeout));
            release.SetResult();

            Assert.AreEqual(20, await latest.WaitAsync(WaitTimeout));
            Assert.HasCount(1, applied);
            Assert.AreEqual(20, applied[0]);
            Assert.AreEqual(2, calls);
        }
        finally
        {
            release.TrySetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ExternalWaitCanceled_StillAppliesComputedValue()
    {
        using var cancellation = new CancellationTokenSource();
        var started = NewSignal();
        var release = NewSignal();
        var applied = new List<int>();
        using var gate = new SupersedingAsyncValueGate<int>(
            async token =>
            {
                started.SetResult();
                await release.Task.ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return 42;
            },
            applied.Add);
        var request = gate.ExecuteAsync(cancellation.Token);

        try
        {
            await started.Task.WaitAsync(WaitTimeout);
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => request.WaitAsync(WaitTimeout));
            release.SetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);

            Assert.HasCount(1, applied);
            Assert.AreEqual(42, applied[0]);
            Assert.IsTrue(request.IsCanceled);
        }
        finally
        {
            release.TrySetResult();
            await gate.WhenIdleAsync().WaitAsync(WaitTimeout);
        }
    }
}
