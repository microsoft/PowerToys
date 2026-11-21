// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests;

[TestClass]
public class ForegroundWindowWaiterTests
{
    private const uint CmdPalProcessId = 100;
    private const uint TargetProcessId = 200;

    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    [TestMethod]
    public void ReturnsTrueWithoutSleeping_WhenAnotherProcessAlreadyHasForeground()
    {
        var clock = new FakeClock();

        var result = Wait(new Queue<uint?>([TargetProcessId]), clock);

        Assert.IsTrue(result);
        Assert.AreEqual(0, clock.SleepCount);
    }

    [TestMethod]
    public void ReturnsTrue_OnceForegroundLeavesProcess()
    {
        var clock = new FakeClock();

        // CmdPal is still foreground, then there's briefly no foreground window, then the target app activates.
        var result = Wait(new Queue<uint?>([CmdPalProcessId, CmdPalProcessId, null, TargetProcessId]), clock);

        Assert.IsTrue(result);
        Assert.AreEqual(3, clock.SleepCount);
    }

    [TestMethod]
    [DataRow(CmdPalProcessId)]
    [DataRow(null)]
    public void ReturnsFalseAfterTimeout_WhenForegroundNeverLeavesProcess(uint? foregroundProcessId)
    {
        var clock = new FakeClock();
        var calls = 0;

        var result = ForegroundWindowWaiter.WaitForForegroundToLeaveProcess(
            CmdPalProcessId,
            () =>
            {
                calls++;
                return foregroundProcessId;
            },
            () => clock.Elapsed,
            clock.Sleep,
            Timeout,
            PollInterval);

        Assert.IsFalse(result);
        Assert.AreEqual(Timeout, clock.Elapsed);
        Assert.AreEqual(51, calls);
    }

    private static bool Wait(Queue<uint?> foregroundProcessIds, FakeClock clock) =>
        ForegroundWindowWaiter.WaitForForegroundToLeaveProcess(
            CmdPalProcessId,
            foregroundProcessIds.Dequeue,
            () => clock.Elapsed,
            clock.Sleep,
            Timeout,
            PollInterval);

    private sealed class FakeClock
    {
        public TimeSpan Elapsed { get; private set; }

        public int SleepCount { get; private set; }

        public void Sleep(TimeSpan duration)
        {
            SleepCount++;
            Elapsed += duration;
        }
    }
}
