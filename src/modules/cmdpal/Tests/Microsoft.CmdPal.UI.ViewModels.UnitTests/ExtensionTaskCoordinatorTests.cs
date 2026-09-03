// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class ExtensionTaskCoordinatorTests
{
    private static readonly int[] AllInputs = [1, 2, 3];
    private static readonly int[] FiveInputs = [1, 2, 3, 4, 5];
    private static readonly int[] FirstInput = [1];
    private static readonly int[] RetryInput = [2];

    [TestMethod]
    public async Task RunConcurrentlyAsync_PreservesOrderAndIsolatesFailures()
    {
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        var errors = new ConcurrentQueue<Exception>();

        async Task<string?> LoadAsync(int value)
        {
            if (Interlocked.Increment(ref started) == 3)
            {
                allStarted.SetResult();
            }

            await release.Task;
            if (value == 2)
            {
                throw new InvalidOperationException("load failed");
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }

        var loadTask = ExtensionTaskCoordinator.RunConcurrentlyAsync(
            AllInputs,
            LoadAsync,
            (_, exception) => errors.Enqueue(exception),
            3,
            CancellationToken.None);

        await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        release.SetResult();

        var results = await loadTask;
        Assert.HasCount(2, results);
        Assert.AreEqual("1", results[0]);
        Assert.AreEqual("3", results[1]);
        Assert.HasCount(1, errors);
        Assert.IsInstanceOfType<InvalidOperationException>(errors.Single());
    }

    [TestMethod]
    public async Task RunConcurrentlyAsync_RespectsLimitAndStartsQueuedWork()
    {
        var firstWaveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeLock = new object();
        var started = 0;
        var active = 0;
        var maxActive = 0;

        async Task<string?> LoadAsync(int value)
        {
            var currentActive = Interlocked.Increment(ref active);
            lock (activeLock)
            {
                maxActive = Math.Max(maxActive, currentActive);
            }

            if (Interlocked.Increment(ref started) == 2)
            {
                firstWaveStarted.SetResult();
            }

            await release.Task;
            Interlocked.Decrement(ref active);
            return value.ToString(CultureInfo.InvariantCulture);
        }

        var loadTask = ExtensionTaskCoordinator.RunConcurrentlyAsync(
            FiveInputs,
            LoadAsync,
            (_, _) => Assert.Fail(),
            2,
            CancellationToken.None);

        await firstWaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.AreEqual(2, Volatile.Read(ref started));
        Assert.AreEqual(2, Volatile.Read(ref maxActive));

        release.SetResult();
        var results = await loadTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.HasCount(5, results);
        Assert.AreEqual(5, started);
        Assert.AreEqual(2, maxActive);
    }

    [TestMethod]
    public async Task RunConcurrentlyAsync_CancellationDoesNotStartQueuedWorkAndAllowsRetry()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new ConcurrentQueue<int>();

        async Task<string?> LoadAsync(int value)
        {
            started.Enqueue(value);
            firstStarted.TrySetResult();
            await releaseFirst.Task;
            return value.ToString(CultureInfo.InvariantCulture);
        }

        var loadTask = ExtensionTaskCoordinator.RunConcurrentlyAsync(
            AllInputs,
            LoadAsync,
            (_, _) => Assert.Fail(),
            1,
            cancellationTokenSource.Token);

        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellationTokenSource.Cancel();
        releaseFirst.SetResult();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => loadTask);
        CollectionAssert.AreEqual(FirstInput, started.ToArray());

        var retry = await ExtensionTaskCoordinator.RunConcurrentlyAsync(
            RetryInput,
            value => Task.FromResult<string?>(value.ToString(CultureInfo.InvariantCulture)),
            (_, _) => Assert.Fail(),
            1,
            CancellationToken.None);
        Assert.AreEqual("2", retry.Single());
    }

    [TestMethod]
    public async Task RunConcurrentlyAsync_FailureReleasesPermit()
    {
        var errors = new ConcurrentQueue<Exception>();

        var results = await ExtensionTaskCoordinator.RunConcurrentlyAsync(
            AllInputs,
            value => value == 1
                ? Task.FromException<string?>(new InvalidOperationException("failed"))
                : Task.FromResult<string?>(value.ToString(CultureInfo.InvariantCulture)),
            (_, exception) => errors.Enqueue(exception),
            1,
            CancellationToken.None);

        Assert.HasCount(2, results);
        Assert.AreEqual("2", results[0]);
        Assert.AreEqual("3", results[1]);
        Assert.HasCount(1, errors);
    }

    [TestMethod]
    public async Task RunWithConcurrencyLimitAsync_SharesLimitAcrossIndependentCallers()
    {
        using var concurrencyGate = new SemaphoreSlim(2, 2);
        var firstWaveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        var active = 0;
        var maxActive = 0;
        var activeLock = new object();

        async Task<int> StartAsync(int value)
        {
            var currentActive = Interlocked.Increment(ref active);
            lock (activeLock)
            {
                maxActive = Math.Max(maxActive, currentActive);
            }

            if (Interlocked.Increment(ref started) == 2)
            {
                firstWaveStarted.SetResult();
            }

            await release.Task;
            Interlocked.Decrement(ref active);
            return value;
        }

        var tasks = FiveInputs.Select(value =>
            ExtensionTaskCoordinator.RunWithConcurrencyLimitAsync(
                concurrencyGate,
                () => StartAsync(value),
                CancellationToken.None)).ToArray();

        await firstWaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.AreEqual(2, started);
        release.SetResult();
        await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(1));
        Assert.AreEqual(5, started);
        Assert.AreEqual(2, maxActive);
    }

    [TestMethod]
    public async Task RunWithConcurrencyLimitAsync_CanceledWaiterDoesNotRunAndPermitCanBeReused()
    {
        using var concurrencyGate = new SemaphoreSlim(1, 1);
        using var cancellationTokenSource = new CancellationTokenSource();
        await concurrencyGate.WaitAsync();
        var operationStarted = false;

        var canceled = ExtensionTaskCoordinator.RunWithConcurrencyLimitAsync(
            concurrencyGate,
            () =>
            {
                operationStarted = true;
                return Task.FromResult(1);
            },
            cancellationTokenSource.Token);
        cancellationTokenSource.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => canceled);
        Assert.IsFalse(operationStarted);

        concurrencyGate.Release();
        var result = await ExtensionTaskCoordinator.RunWithConcurrencyLimitAsync(
            concurrencyGate,
            () => Task.FromResult(2),
            CancellationToken.None);
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public async Task RunWithConcurrencyLimitAsync_FailureReleasesPermit()
    {
        using var concurrencyGate = new SemaphoreSlim(1, 1);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => ExtensionTaskCoordinator.RunWithConcurrencyLimitAsync<int>(
                concurrencyGate,
                () => throw new InvalidOperationException("failed"),
                CancellationToken.None));

        var result = await ExtensionTaskCoordinator.RunWithConcurrencyLimitAsync(
            concurrencyGate,
            () => Task.FromResult(2),
            CancellationToken.None);
        Assert.AreEqual(2, result);
    }

    [TestMethod]
    public async Task RunBlockingConcurrentlyAsync_UsesOneAggregateTimeout()
    {
        using var release = new ManualResetEventSlim();
        using var allStarted = new CountdownEvent(3);
        var errors = new ConcurrentQueue<Exception>();
        var timedOut = false;
        var stopwatch = Stopwatch.StartNew();

        await ExtensionTaskCoordinator.RunBlockingConcurrentlyAsync(
            AllInputs,
            _ =>
            {
                allStarted.Signal();
                release.Wait(TimeSpan.FromSeconds(5));
            },
            TimeSpan.FromMilliseconds(200),
            (_, exception) => errors.Enqueue(exception),
            () => timedOut = true);

        stopwatch.Stop();
        release.Set();
        Assert.IsTrue(allStarted.IsSet);
        Assert.IsTrue(timedOut);
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.IsEmpty(errors);
    }

    [TestMethod]
    public async Task RunBlockingConcurrentlyAsync_IsolatesFailures()
    {
        var completed = new ConcurrentBag<int>();
        var errors = new ConcurrentQueue<Exception>();

        await ExtensionTaskCoordinator.RunBlockingConcurrentlyAsync(
            AllInputs,
            value =>
            {
                if (value == 2)
                {
                    throw new InvalidOperationException("dispose failed");
                }

                completed.Add(value);
            },
            TimeSpan.FromSeconds(1),
            (value, exception) => errors.Enqueue(exception),
            Assert.Fail);

        Assert.HasCount(2, completed);
        Assert.IsTrue(completed.Contains(1));
        Assert.IsTrue(completed.Contains(3));
        Assert.HasCount(1, errors);
        Assert.IsInstanceOfType<InvalidOperationException>(errors.Single());
    }

    [TestMethod]
    public async Task ObserveAsync_ReportsFailuresAndIgnoresRequestedCancellation()
    {
        var errors = new ConcurrentQueue<Exception>();
        void OnError(string operation, Exception exception) => errors.Enqueue(exception);

        await ExtensionTaskCoordinator.ObserveAsync(
            Task.FromException(new InvalidOperationException("watcher failed")),
            "watcher",
            OnError,
            CancellationToken.None);

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        await ExtensionTaskCoordinator.ObserveAsync(
            Task.FromCanceled(cancellationTokenSource.Token),
            "watcher",
            OnError,
            cancellationTokenSource.Token);

        Assert.HasCount(1, errors);
        Assert.IsInstanceOfType<InvalidOperationException>(errors.Single());
    }

    [TestMethod]
    public async Task RunInBackgroundAsync_DoesNotBlockTheCaller()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var errors = new ConcurrentQueue<Exception>();
        var stopwatch = Stopwatch.StartNew();

        var observed = ExtensionTaskCoordinator.RunInBackgroundAsync(
            () =>
            {
                started.Set();
                release.Wait(TimeSpan.FromSeconds(5));
                return Task.CompletedTask;
            },
            "watcher",
            (_, exception) => errors.Enqueue(exception),
            CancellationToken.None);

        stopwatch.Stop();
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(1)));
        release.Set();
        await observed.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsEmpty(errors);
    }
}
