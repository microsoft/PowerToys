// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;
using Microsoft.CmdPal.Core.Common.Helpers;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public class ThrottledDebouncedActionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [TestMethod]
    public void Invoke_JoinsPendingPriorityRun()
    {
        var runs = 0;
        using var ran = new ManualResetEventSlim();
        using var action = new ThrottledDebouncedAction(
            () =>
            {
                Interlocked.Increment(ref runs);
                ran.Set();
            },
            TimeSpan.FromMilliseconds(400));

        action.InvokePriority(TimeSpan.FromMilliseconds(200));

        // Plain calls keep arriving faster than their own debounce, which used to postpone the run forever.
        var stopwatch = Stopwatch.StartNew();
        while (!ran.IsSet && stopwatch.Elapsed < TestTimeout)
        {
            action.Invoke();
            Thread.Sleep(20);
        }

        Assert.IsTrue(ran.IsSet, "Plain calls postponed the pending priority run.");
        Assert.AreEqual(1, Volatile.Read(ref runs));
    }

    [TestMethod]
    public void InvokePriority_ReschedulesPendingRun()
    {
        var runs = 0;
        var priorityInterval = TimeSpan.FromMilliseconds(150);
        using var action = new ThrottledDebouncedAction(() => Interlocked.Increment(ref runs), TimeSpan.FromMilliseconds(300));

        action.Invoke();

        // Priority calls keep arriving past the plain run's deadline, so they coalesce it.
        var stopwatch = Stopwatch.StartNew();
        var lastCall = TimeSpan.Zero;
        var longestGap = TimeSpan.Zero;
        while (stopwatch.ElapsedMilliseconds < 600)
        {
            action.InvokePriority(priorityInterval);
            var now = stopwatch.Elapsed;
            longestGap = TimeSpan.FromTicks(Math.Max(longestGap.Ticks, (now - lastCall).Ticks));
            lastCall = now;
            Thread.Sleep(50);
        }

        var runsWhileCalling = Volatile.Read(ref runs);
        longestGap = TimeSpan.FromTicks(Math.Max(longestGap.Ticks, (stopwatch.Elapsed - lastCall).Ticks));

        // A sleep that overshoots the priority deadline lets the run fire as designed.
        if (runsWhileCalling > 0 && longestGap >= priorityInterval)
        {
            Assert.Inconclusive($"A {longestGap.TotalMilliseconds:F0} ms pause between calls let the pending run fire.");
        }

        Assert.AreEqual(0, runsWhileCalling, "A run fired while priority calls kept arriving.");
        Assert.IsTrue(SpinWait.SpinUntil(() => Volatile.Read(ref runs) > 0, TestTimeout));
        Thread.Sleep(400);
        Assert.AreEqual(1, Volatile.Read(ref runs));
    }

    [TestMethod]
    public void Invoke_WhileActionRuns_SchedulesNewRun()
    {
        var runs = 0;
        using var firstStarted = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var action = new ThrottledDebouncedAction(
            () =>
            {
                if (Interlocked.Increment(ref runs) == 1)
                {
                    firstStarted.Set();
                    releaseFirst.Wait(TestTimeout);
                }
            },
            TimeSpan.FromMilliseconds(20));

        action.InvokePriority();
        Assert.IsTrue(firstStarted.Wait(TestTimeout));

        // The running action already claimed its run, so this call must schedule another one.
        action.Invoke();
        releaseFirst.Set();

        Assert.IsTrue(SpinWait.SpinUntil(() => Volatile.Read(ref runs) == 2, TestTimeout));
    }

    [TestMethod]
    public void Cancel_ClearsPendingPriority()
    {
        var runs = 0;
        using var action = new ThrottledDebouncedAction(() => Interlocked.Increment(ref runs), TimeSpan.FromMilliseconds(20));

        action.InvokePriority(TimeSpan.FromSeconds(10));
        action.Cancel();
        action.Invoke();

        Assert.IsTrue(SpinWait.SpinUntil(() => Volatile.Read(ref runs) == 1, TestTimeout), "A plain call joined a cancelled priority run.");
    }
}
