// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class WindowHelperTests
{
    private const int EHandle = unchecked((int)0x80070006);
    private static readonly IntPtr TestWindow = new(0x1234);

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, true)]
    [DataRow(2, true)]
    [DataRow(4, true)]
    public void IsWindowCloakedMapsEveryDwmCloakFlag(int cloakState, bool expected)
    {
        var actual = WindowHelper.IsWindowCloaked(
            TestWindow,
            hWnd =>
            {
                Assert.AreEqual(TestWindow, hWnd);
                return (0, cloakState);
            });

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void IsWindowCloakedReportsInvalidOrDestroyedWindow()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => WindowHelper.IsWindowCloaked(TestWindow, _ => (EHandle, 0)));

        Assert.Contains("DWMWA_CLOAKED", exception.Message);
        Assert.Contains("0x80070006", exception.Message);
        Assert.Contains("0x1234", exception.Message);
    }

    [TestMethod]
    public void GetVisibleBoundsPreservesPhysicalDwmCoordinates()
    {
        var expected = (Left: -2880, Top: 144, Right: -960, Bottom: 1224);

        var actual = WindowHelper.GetVisibleBounds(TestWindow, _ => (0, expected));

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void GetVisibleBoundsReportsInvalidOrDestroyedWindow()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => WindowHelper.GetVisibleBounds(TestWindow, _ => (EHandle, default)));

        Assert.Contains("DWMWA_EXTENDED_FRAME_BOUNDS", exception.Message);
        Assert.Contains("0x80070006", exception.Message);
        Assert.Contains("0x1234", exception.Message);
    }

    [TestMethod]
    public void GetVisibleBoundsRejectsEmptyDwmFrame()
    {
        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => WindowHelper.GetVisibleBounds(
                TestWindow,
                _ => (0, (Left: 20, Top: 30, Right: 20, Bottom: 80))));

        Assert.Contains("invalid bounds", exception.Message);
        Assert.Contains("(20,30)-(20,80)", exception.Message);
    }
}
