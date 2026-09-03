// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.UI.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public sealed class ProcessShutdownCoordinatorTests
{
    [TestMethod]
    public void RequestExit_WithLiveWindow_ClosesWindowAndExitsOnce()
    {
        var coordinator = CreateCoordinator();
        var closeCount = 0;
        var stopCount = 0;
        var exitCount = 0;

        var started = coordinator.RequestExit(
            () =>
            {
                closeCount++;
                coordinator.RequestExit(null, () => [], () => exitCount++);
            },
            () => GetShutdownOperations(() => stopCount++),
            () => exitCount++);

        Assert.IsTrue(started);
        Assert.AreEqual(1, closeCount);
        Assert.AreEqual(1, stopCount);
        Assert.AreEqual(1, exitCount);
    }

    [TestMethod]
    public void RequestExit_WithoutWindow_StillStopsAndExits()
    {
        var coordinator = CreateCoordinator();
        var stopCount = 0;
        var exitCount = 0;

        coordinator.RequestExit(
            null,
            () => GetShutdownOperations(() => stopCount++),
            () => exitCount++);

        Assert.AreEqual(1, stopCount);
        Assert.AreEqual(1, exitCount);
    }

    [TestMethod]
    public void RequestExit_WhenWindowCloseFails_StillStopsAndExits()
    {
        var errors = new List<Exception>();
        var coordinator = CreateCoordinator(errors.Add);
        var stopCount = 0;
        var exitCount = 0;

        coordinator.RequestExit(
            () => throw new InvalidOperationException("stale window"),
            () => GetShutdownOperations(() => stopCount++),
            () => exitCount++);

        Assert.AreEqual(1, stopCount);
        Assert.AreEqual(1, exitCount);
        Assert.HasCount(1, errors);
        Assert.IsInstanceOfType<InvalidOperationException>(errors[0]);
    }

    [TestMethod]
    public void RequestExit_WhenCloseDoesNotRaiseEvent_StillExitsOnce()
    {
        var coordinator = CreateCoordinator();
        var closeCount = 0;
        var exitCount = 0;

        Assert.IsTrue(coordinator.RequestExit(
            () => closeCount++,
            () => Array.Empty<Func<Task>>(),
            () => exitCount++));
        Assert.IsFalse(coordinator.RequestExit(
            null,
            () => Array.Empty<Func<Task>>(),
            () => exitCount++));

        Assert.AreEqual(1, closeCount);
        Assert.AreEqual(1, exitCount);
    }

    [TestMethod]
    public void RequestExit_WhenShutdownTimesOut_StillExits()
    {
        var errors = new List<Exception>();
        var coordinator = new ProcessShutdownCoordinator(
            TimeSpan.FromMilliseconds(20),
            action => action(),
            errors.Add);
        var exitCount = 0;

        coordinator.RequestExit(
            null,
            () => new List<Func<Task>> { () => new TaskCompletionSource().Task },
            () => exitCount++);

        Assert.AreEqual(1, exitCount);
        Assert.HasCount(1, errors);
        Assert.IsInstanceOfType<TimeoutException>(errors[0]);
    }

    [TestMethod]
    public void RequestExit_SynchronousStopWorkCannotBypassAggregateTimeout()
    {
        using var release = new ManualResetEventSlim();
        var errors = new List<Exception>();
        var coordinator = new ProcessShutdownCoordinator(
            TimeSpan.FromMilliseconds(50),
            action => action(),
            errors.Add);
        var exitCount = 0;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            coordinator.RequestExit(
                null,
                () => GetShutdownOperations(() => release.Wait(TimeSpan.FromSeconds(5))),
                () => exitCount++);
        }
        finally
        {
            release.Set();
        }

        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.AreEqual(1, exitCount);
        Assert.HasCount(1, errors);
        Assert.IsInstanceOfType<TimeoutException>(errors[0]);
    }

    [TestMethod]
    public void RequestExit_SynchronousOperationDiscoveryCannotBypassAggregateTimeout()
    {
        using var release = new ManualResetEventSlim();
        var errors = new List<Exception>();
        var coordinator = new ProcessShutdownCoordinator(
            TimeSpan.FromMilliseconds(50),
            action => action(),
            errors.Add);
        var exitCount = 0;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            coordinator.RequestExit(
                null,
                () =>
                {
                    release.Wait(TimeSpan.FromSeconds(5));
                    return Array.Empty<Func<Task>>();
                },
                () => exitCount++);
        }
        finally
        {
            release.Set();
        }

        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
        Assert.AreEqual(1, exitCount);
        Assert.HasCount(1, errors);
        Assert.IsInstanceOfType<TimeoutException>(errors[0]);
    }

    [TestMethod]
    public void RequestExit_WhenWorkerCannotStart_StillExitsExactlyOnce()
    {
        var errors = new List<Exception>();
        var coordinator = new ProcessShutdownCoordinator(
            TimeSpan.FromSeconds(1),
            action =>
            {
                action();
                throw new InvalidOperationException("worker failed after running");
            },
            errors.Add);
        var exitCount = 0;

        coordinator.RequestExit(
            null,
            () => Array.Empty<Func<Task>>(),
            () => exitCount++);

        Assert.AreEqual(1, exitCount);
        Assert.HasCount(1, errors);
        Assert.IsInstanceOfType<InvalidOperationException>(errors[0]);
    }

    private static ProcessShutdownCoordinator CreateCoordinator(Action<Exception>? onError = null)
    {
        return new ProcessShutdownCoordinator(
            TimeSpan.FromSeconds(1),
            action => action(),
            onError ?? (_ => Assert.Fail("Shutdown should not fail")));
    }

    private static Task StopAsync(Action stop)
    {
        stop();
        return Task.CompletedTask;
    }

    private static IReadOnlyList<Func<Task>> GetShutdownOperations(Action stop)
    {
        return new List<Func<Task>> { () => StopAsync(stop) };
    }
}
