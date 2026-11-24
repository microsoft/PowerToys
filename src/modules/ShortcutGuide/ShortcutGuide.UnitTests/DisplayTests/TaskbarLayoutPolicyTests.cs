// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShortcutGuide.Helpers;
using Windows.Graphics;

namespace ShortcutGuide.UnitTests.DisplayTests;

[TestClass]
public sealed class TaskbarLayoutPolicyTests
{
    private static readonly RectInt32 Monitor = new(-1010, -2160, 3840, 2160);

    // The hidden sensor inset should preserve the taskbar activation edge at each DPI
    // scale.
    [TestMethod]
    [DataRow(3, 1.25f, 2155)]
    [DataRow(3, 1.75f, 2153)]
    [DataRow(1, 1.25f, 2155)]
    [DataRow(0, 1.75f, 3833)]
    [DataRow(2, 1.75f, 3833)]
    public void CalculateAutoHideLayout_HiddenTaskbar_LeavesSensorMargin(
        int edgeValue,
        float dpiScale,
        int expectedPrimaryDimension)
    {
        TaskbarEdge edge = (TaskbarEdge)edgeValue;
        AutoHideTaskbarLayout layout = TaskbarLayoutPolicy.CalculateAutoHideLayout(
            Monitor,
            edge,
            dpiScale,
            overlapWidth: 0,
            overlapHeight: 0);

        Assert.IsFalse(layout.IsTaskbarVisible);
        int actualPrimaryDimension = edge is TaskbarEdge.Bottom or TaskbarEdge.Top
            ? layout.UsableArea.Height
            : layout.UsableArea.Width;
        Assert.AreEqual(expectedPrimaryDimension, actualPrimaryDimension);
    }

    [TestMethod]
    [DataRow(3, 1920, 100)]
    [DataRow(1, 1920, 100)]
    [DataRow(0, 100, 1080)]
    [DataRow(2, 100, 1080)]
    public void CalculateAutoHideLayout_RevealedTaskbar_ExcludesMeasuredOverlap(
        int edgeValue,
        int overlapWidth,
        int overlapHeight)
    {
        TaskbarEdge edge = (TaskbarEdge)edgeValue;
        AutoHideTaskbarLayout layout = TaskbarLayoutPolicy.CalculateAutoHideLayout(
            new RectInt32(0, 0, 1920, 1080),
            edge,
            dpiScale: 1.25f,
            overlapWidth,
            overlapHeight);

        Assert.IsTrue(layout.IsTaskbarVisible);
        int expectedPrimaryDimension = edge is TaskbarEdge.Bottom or TaskbarEdge.Top ? 980 : 1820;
        int actualPrimaryDimension = edge is TaskbarEdge.Bottom or TaskbarEdge.Top
            ? layout.UsableArea.Height
            : layout.UsableArea.Width;
        Assert.AreEqual(expectedPrimaryDimension, actualPrimaryDimension);
    }

    // At 125% DPI, the 4-DIP reveal threshold rounds up to 5 physical pixels.
    // An overlap equal to that threshold remains hidden; the first pixel above
    // it is treated as a revealed taskbar.
    [TestMethod]
    public void CalculateAutoHideLayout_OverlapAtThreshold_IsStillHidden()
    {
        AutoHideTaskbarLayout layout = TaskbarLayoutPolicy.CalculateAutoHideLayout(
            new RectInt32(0, 0, 1920, 1080),
            TaskbarEdge.Bottom,
            dpiScale: 1.25f,
            overlapWidth: 1920,
            overlapHeight: 5);

        Assert.IsFalse(layout.IsTaskbarVisible);
        Assert.AreEqual(1075, layout.UsableArea.Height);

        layout = TaskbarLayoutPolicy.CalculateAutoHideLayout(
            new RectInt32(0, 0, 1920, 1080),
            TaskbarEdge.Bottom,
            dpiScale: 1.25f,
            overlapWidth: 1920,
            overlapHeight: 6);

        Assert.IsTrue(layout.IsTaskbarVisible);
        Assert.AreEqual(1074, layout.UsableArea.Height);
    }

    [TestMethod]
    public void ResolveMonitor_UsesLayoutSnapshotEvenIfOverlayMonitorDiffers()
    {
        nint layoutMonitor = (nint)123;
        nint overlayMonitor = (nint)456;

        Assert.AreEqual(layoutMonitor, TaskbarLayoutPolicy.ResolveMonitor(layoutMonitor, overlayMonitor));
    }

    [TestMethod]
    public void ResolveMonitor_FallsBackToOverlayMonitorWhenSnapshotUnavailable()
    {
        nint overlayMonitor = (nint)456;

        Assert.AreEqual(overlayMonitor, TaskbarLayoutPolicy.ResolveMonitor(nint.Zero, overlayMonitor));
    }
}
