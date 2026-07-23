// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;
using static PowerDisplay.Common.Services.TrayWheelAdjustmentPlanner;

namespace PowerDisplay.UnitTests;

[TestClass]
public class TrayWheelAdjustmentPlannerTests
{
    private static Target Monitor(
        string id,
        string gdi,
        int brightness,
        bool supportsBrightness = true,
        bool hasBrightnessReading = true)
        => new(id, gdi, supportsBrightness, hasBrightnessReading, brightness);

    [TestMethod]
    public void Plan_Disabled_ReturnsNoAdjustments()
    {
        var result = Plan(
            MouseWheelControlMode.Disabled,
            [Monitor("a", @"\\.\DISPLAY1", 50)],
            @"\\.\DISPLAY1",
            5);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Plan_PrimaryDisplay_SelectsByGdiNameNotMonitorOrder()
    {
        Target[] targets =
        [
            Monitor("first", @"\\.\DISPLAY2", 40),
            Monitor("primary", @"\\.\DISPLAY7", 60),
        ];

        var result = Plan(
            MouseWheelControlMode.PrimaryDisplay,
            targets,
            @"\\.\display7",
            5);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(new Adjustment("primary", 65), result[0]);
    }

    [TestMethod]
    public void Plan_PrimaryDisplay_SelectsEveryMirroredPhysicalTarget()
    {
        Target[] targets =
        [
            Monitor("mirror-a", @"\\.\DISPLAY1", 30),
            Monitor("mirror-b", @"\\.\DISPLAY1", 70),
            Monitor("other", @"\\.\DISPLAY2", 50),
        ];

        var result = Plan(
            MouseWheelControlMode.PrimaryDisplay,
            targets,
            @"\\.\DISPLAY1",
            -10);

        CollectionAssert.AreEqual(
            new[] { new Adjustment("mirror-a", 20), new Adjustment("mirror-b", 60) },
            result.ToArray());
    }

    [TestMethod]
    public void Plan_PrimaryDisplayWithoutResolvedGdi_ReturnsNoAdjustments()
    {
        var result = Plan(
            MouseWheelControlMode.PrimaryDisplay,
            [Monitor("a", @"\\.\DISPLAY1", 50)],
            null,
            5);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Plan_AllDisplays_PreservesPerMonitorOffsets()
    {
        Target[] targets =
        [
            Monitor("a", @"\\.\DISPLAY1", 20),
            Monitor("b", @"\\.\DISPLAY2", 80),
        ];

        var result = Plan(
            MouseWheelControlMode.AllDisplays,
            targets,
            null,
            5);

        CollectionAssert.AreEqual(
            new[] { new Adjustment("a", 25), new Adjustment("b", 85) },
            result.ToArray());
    }

    [TestMethod]
    public void Plan_SkipsUnsupportedUnreadAndEmptyIdTargets()
    {
        Target[] targets =
        [
            Monitor("valid", @"\\.\DISPLAY1", 50),
            Monitor("unsupported", @"\\.\DISPLAY2", 50, supportsBrightness: false),
            Monitor("unread", @"\\.\DISPLAY3", 0, hasBrightnessReading: false),
            Monitor(string.Empty, @"\\.\DISPLAY4", 50),
        ];

        var result = Plan(MouseWheelControlMode.AllDisplays, targets, null, 5);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(new Adjustment("valid", 55), result[0]);
    }

    [TestMethod]
    public void Plan_ClampsEachTargetAtBrightnessBoundaries()
    {
        Target[] targets =
        [
            Monitor("low", @"\\.\DISPLAY1", 2),
            Monitor("high", @"\\.\DISPLAY2", 100),
        ];

        var result = Plan(MouseWheelControlMode.AllDisplays, targets, null, 10);

        CollectionAssert.AreEqual(
            new[] { new Adjustment("low", 12), new Adjustment("high", 100) },
            result.ToArray());
    }

    [TestMethod]
    public void Plan_LargeDeltaCannotOverflow()
    {
        var result = Plan(
            MouseWheelControlMode.AllDisplays,
            [Monitor("a", @"\\.\DISPLAY1", 50)],
            null,
            long.MaxValue);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(new Adjustment("a", 100), result[0]);
    }

    [TestMethod]
    public void Plan_LargeNegativeDeltaCannotOverflow()
    {
        var result = Plan(
            MouseWheelControlMode.AllDisplays,
            [Monitor("a", @"\\.\DISPLAY1", 50)],
            null,
            long.MinValue);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(new Adjustment("a", 0), result[0]);
    }
}
