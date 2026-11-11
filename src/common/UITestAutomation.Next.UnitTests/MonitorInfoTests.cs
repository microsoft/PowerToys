// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class MonitorInfoTests
{
    private const uint MonitorDefaultToNearest = 0x2;
    private static readonly IntPtr TestWindow = new(0x1234);

    [TestMethod]
    public void GetFromWindowReturnsMappedMonitorAndWorkArea()
    {
        var monitors = new[]
        {
            (
                Handle: new IntPtr(1),
                Monitor: new MonitorInfo.Monitor(
                    "\\\\.\\DISPLAY1",
                    0,
                    0,
                    1920,
                    1080,
                    0,
                    0,
                    1920,
                    1040,
                    true)),
            (
                Handle: new IntPtr(2),
                Monitor: new MonitorInfo.Monitor(
                    "\\\\.\\DISPLAY2",
                    -2880,
                    0,
                    0,
                    1620,
                    -2880,
                    0,
                    0,
                    1560,
                    false)),
        };

        foreach (var expected in monitors)
        {
            var actual = MonitorInfo.GetFromWindow(
                TestWindow,
                _ => true,
                (hWnd, flags) =>
                {
                    Assert.AreEqual(TestWindow, hWnd);
                    Assert.AreEqual(MonitorDefaultToNearest, flags);
                    return expected.Handle;
                },
                hMonitor =>
                {
                    Assert.AreEqual(expected.Handle, hMonitor);
                    return expected.Monitor;
                });

            Assert.AreEqual(expected.Monitor, actual);
            Assert.AreEqual(expected.Monitor.WorkRight - expected.Monitor.WorkLeft, actual.WorkWidth);
            Assert.AreEqual(expected.Monitor.WorkBottom - expected.Monitor.WorkTop, actual.WorkHeight);
        }
    }

    [TestMethod]
    public void GetFromWindowRejectsInvalidWindowBeforeNativeLookup()
    {
        var monitorLookupCalled = false;

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => MonitorInfo.GetFromWindow(
                TestWindow,
                _ => false,
                (_, _) =>
                {
                    monitorLookupCalled = true;
                    return new IntPtr(1);
                },
                _ => throw new AssertFailedException("Monitor details should not be queried.")));

        Assert.IsFalse(monitorLookupCalled);
        Assert.Contains("invalid or destroyed", exception.Message);
        Assert.Contains("0x1234", exception.Message);
    }

    [TestMethod]
    public void GetFromWindowRejectsWindowDestroyedDuringLookup()
    {
        var windowChecks = new Queue<bool>([true, false]);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => MonitorInfo.GetFromWindow(
                TestWindow,
                _ => windowChecks.Dequeue(),
                (_, flags) =>
                {
                    Assert.AreEqual(MonitorDefaultToNearest, flags);
                    return new IntPtr(1);
                },
                _ => new MonitorInfo.Monitor(
                    "\\\\.\\DISPLAY1",
                    0,
                    0,
                    1920,
                    1080,
                    0,
                    0,
                    1920,
                    1040,
                    true)));

        Assert.IsEmpty(windowChecks);
        Assert.Contains("destroyed during", exception.Message);
        Assert.Contains("0x1234", exception.Message);
    }

    [TestMethod]
    public void GetFromWindowReportsMissingMonitor()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => MonitorInfo.GetFromWindow(
                TestWindow,
                _ => true,
                (_, _) => IntPtr.Zero,
                _ => throw new AssertFailedException("Monitor details should not be queried.")));

        Assert.Contains("MonitorFromWindow failed", exception.Message);
        Assert.Contains("0x1234", exception.Message);
    }
}
