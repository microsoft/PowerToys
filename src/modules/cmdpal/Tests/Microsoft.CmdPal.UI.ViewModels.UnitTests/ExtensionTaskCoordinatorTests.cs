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
