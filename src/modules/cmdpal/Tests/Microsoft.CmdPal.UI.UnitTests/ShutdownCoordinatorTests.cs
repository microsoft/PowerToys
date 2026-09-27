// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public sealed class ShutdownCoordinatorTests
{
    [TestMethod]
    [Timeout(5_000)]
    public async Task RunAsyncWaitsForExtensionsAndOnlyRunsOnce()
    {
        var calls = 0;
        var cleanupCount = 0;
        var exitCount = 0;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new ShutdownCoordinator(
            () =>
            {
                Interlocked.Increment(ref calls);
                return completion.Task;
            },
            () =>
            {
                cleanupCount++;
                return Task.CompletedTask;
            },
            () => exitCount++,
            _ => Assert.Fail("Extension shutdown should succeed."),
            _ => Assert.Fail("Cleanup should succeed."),
            TimeSpan.FromSeconds(2));

        var first = coordinator.RunAsync();
        var second = coordinator.RunAsync();

        Assert.AreSame(first, second);
        Assert.AreEqual(1, calls);
        Assert.AreEqual(0, cleanupCount);
        Assert.AreEqual(0, exitCount);

        completion.SetResult();
        await first;

        Assert.AreEqual(1, cleanupCount);
        Assert.AreEqual(1, exitCount);
        Assert.AreSame(first, coordinator.RunAsync());
    }

    [TestMethod]
    public async Task RunAsyncTimesOutButStillCleansUpAndExits()
    {
        var failure = default(Exception);
        var cleanupCount = 0;
        var exitCount = 0;
        var coordinator = new ShutdownCoordinator(
            () => new TaskCompletionSource().Task,
            () =>
            {
                cleanupCount++;
                return Task.CompletedTask;
            },
            () => exitCount++,
            ex => failure = ex,
            _ => Assert.Fail("Cleanup should succeed."),
            TimeSpan.Zero);

        await coordinator.RunAsync();

        Assert.IsInstanceOfType<TimeoutException>(failure);
        Assert.AreEqual(1, cleanupCount);
        Assert.AreEqual(1, exitCount);
    }

    [TestMethod]
    public async Task RunAsyncLogsStopFailureAndStillCleansUp()
    {
        var expected = new InvalidOperationException();
        var failure = default(Exception);
        var cleanupCount = 0;
        var exitCount = 0;
        var coordinator = new ShutdownCoordinator(
            () => Task.FromException(expected),
            () =>
            {
                cleanupCount++;
                return Task.CompletedTask;
            },
            () => exitCount++,
            ex => failure = ex,
            _ => Assert.Fail("Cleanup should succeed."),
            TimeSpan.FromSeconds(1));

        await coordinator.RunAsync();

        Assert.AreSame(expected, failure);
        Assert.AreEqual(1, cleanupCount);
        Assert.AreEqual(1, exitCount);
    }

    [TestMethod]
    public async Task RunAsyncExitsEvenWhenCleanupFails()
    {
        var expected = new InvalidOperationException();
        var failure = default(Exception);
        var exitCount = 0;
        var coordinator = new ShutdownCoordinator(
            () => Task.CompletedTask,
            () => Task.FromException(expected),
            () => exitCount++,
            _ => Assert.Fail("Extension shutdown should succeed."),
            ex => failure = ex,
            TimeSpan.FromSeconds(1));

        await coordinator.RunAsync();

        Assert.AreSame(expected, failure);
        Assert.AreEqual(1, exitCount);
    }

    [TestMethod]
    public async Task RunAsyncDisposesAsyncServicesBeforeExit()
    {
        var resource = new AsyncOnlyResource();
        var services = new ServiceCollection()
            .AddSingleton<IAsyncDisposable>(_ => resource)
            .BuildServiceProvider();
        _ = services.GetRequiredService<IAsyncDisposable>();
        var disposedAtExit = false;
        var coordinator = new ShutdownCoordinator(
            () => Task.CompletedTask,
            () => services.DisposeAsync().AsTask(),
            () => disposedAtExit = resource.IsDisposed,
            _ => Assert.Fail("Shutdown should succeed."),
            _ => Assert.Fail("Cleanup should succeed."),
            TimeSpan.FromSeconds(1));

        await coordinator.RunAsync();

        Assert.IsTrue(resource.IsDisposed);
        Assert.IsTrue(disposedAtExit);
    }

    private sealed class AsyncOnlyResource : IAsyncDisposable
    {
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
