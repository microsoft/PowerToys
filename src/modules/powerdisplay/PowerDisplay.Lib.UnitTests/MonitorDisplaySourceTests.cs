// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class MonitorDisplaySourceTests
{
    private const string InternalPath = @"\\?\DISPLAY#TMX1560#5&1a8f1f05&0&UID512";
    private const string ExternalPath = @"\\?\DISPLAY#LEN67B8#5&bdc9863&0&UID4352";
    private const string UncontrolledPath = @"\\?\DISPLAY#SAM7310#5&2c4e1a7&0&UID8448";
    private const string InterfaceSuffix = "#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    private static readonly int[] Rates = { 60, 120 };
    private static readonly int[] FractionalRates = { 59, 60, 120 };

    [TestMethod]
    public void ApplyDisplaySources_DuplicateMode_RemapsAndFlagsSharedSource()
    {
        var builtIn = CreateMonitor(InternalPath, @"\\.\DISPLAY1");
        var external = CreateMonitor(ExternalPath, @"\\.\DISPLAY3");

        var changed = MonitorManager.ApplyDisplaySources(
            new[] { builtIn, external },
            new[]
            {
                CreateDisplay(InternalPath, @"\\.\DISPLAY1"),
                CreateDisplay(ExternalPath.ToLowerInvariant(), @"\\.\display1"),
            });

        Assert.IsTrue(changed);
        Assert.AreEqual(@"\\.\DISPLAY1", builtIn.GdiDeviceName);
        Assert.AreEqual(@"\\.\display1", external.GdiDeviceName);
        Assert.IsTrue(builtIn.SharesDisplaySource);
        Assert.IsTrue(external.SharesDisplaySource);
    }

    [TestMethod]
    public void ApplyDisplaySources_DisplayPowerDisplayDoesNotControl_StillCountsAsSharing()
    {
        var builtIn = CreateMonitor(InternalPath, @"\\.\DISPLAY1");

        var changed = MonitorManager.ApplyDisplaySources(
            new[] { builtIn },
            new[]
            {
                CreateDisplay(InternalPath, @"\\.\DISPLAY1"),
                CreateDisplay(UncontrolledPath, @"\\.\DISPLAY1"),
            });

        Assert.IsFalse(changed);
        Assert.IsTrue(builtIn.SharesDisplaySource);
    }

    [TestMethod]
    public void ApplyDisplaySources_ExtendMode_ClearsSharedFlag()
    {
        var builtIn = CreateMonitor(InternalPath, @"\\.\DISPLAY1");
        var external = CreateMonitor(ExternalPath, @"\\.\DISPLAY1");
        builtIn.SharesDisplaySource = true;
        external.SharesDisplaySource = true;

        var changed = MonitorManager.ApplyDisplaySources(
            new[] { builtIn, external },
            new[]
            {
                CreateDisplay(InternalPath, @"\\.\DISPLAY1"),
                CreateDisplay(ExternalPath, @"\\.\DISPLAY3"),
            });

        Assert.IsTrue(changed);
        Assert.AreEqual(@"\\.\DISPLAY3", external.GdiDeviceName);
        Assert.IsFalse(builtIn.SharesDisplaySource);
        Assert.IsFalse(external.SharesDisplaySource);
    }

    [TestMethod]
    public void ApplyDisplaySources_MonitorMissingFromInventory_KeepsExistingValues()
    {
        var monitor = CreateMonitor(ExternalPath, @"\\.\DISPLAY3");
        monitor.SharesDisplaySource = true;

        var changed = MonitorManager.ApplyDisplaySources(new[] { monitor }, Array.Empty<MonitorDisplayInfo>());

        Assert.IsFalse(changed);
        Assert.AreEqual(@"\\.\DISPLAY3", monitor.GdiDeviceName);
        Assert.IsTrue(monitor.SharesDisplaySource);
        Assert.IsFalse(monitor.IsDisplaySourceVerified);
    }

    [TestMethod]
    public void ApplyDisplaySources_NewMonitor_IsUnverifiedUntilTopologyIsRead()
    {
        var monitor = CreateMonitor(ExternalPath, @"\\.\DISPLAY3");
        Assert.IsFalse(monitor.IsDisplaySourceVerified);

        MonitorManager.ApplyDisplaySources(new[] { monitor }, new[] { CreateDisplay(ExternalPath, @"\\.\DISPLAY3") });

        Assert.IsTrue(monitor.IsDisplaySourceVerified);
        Assert.IsFalse(monitor.SharesDisplaySource);
    }

    [TestMethod]
    public void ApplyDisplaySources_TopologyReadFailed_MarksEveryMonitorUnverified()
    {
        var builtIn = CreateMonitor(InternalPath, @"\\.\DISPLAY1");
        var external = CreateMonitor(ExternalPath, @"\\.\DISPLAY3");
        builtIn.IsDisplaySourceVerified = true;
        external.IsDisplaySourceVerified = true;

        var changed = MonitorManager.ApplyDisplaySources(new[] { builtIn, external }, null);

        Assert.IsFalse(changed);
        Assert.AreEqual(@"\\.\DISPLAY3", external.GdiDeviceName);
        Assert.IsFalse(builtIn.IsDisplaySourceVerified);
        Assert.IsFalse(external.IsDisplaySourceVerified);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void ResolveCurrentRefreshRate_DefaultReading_UsesTargetRate(int enumRate)
    {
        // Was 120 Hz; the display now runs at 60 Hz but EnumDisplaySettings reports the default.
        var rate = MonitorManager.ResolveCurrentRefreshRate(enumRate, 60.0, Rates);

        Assert.AreEqual(60, rate);
    }

    [TestMethod]
    public void ResolveCurrentRefreshRate_FractionalTargetRate_MatchesListedOption()
    {
        Assert.AreEqual(59, MonitorManager.ResolveCurrentRefreshRate(0, 59.94, FractionalRates));
        Assert.AreEqual(60, MonitorManager.ResolveCurrentRefreshRate(0, 59.94, Rates));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void ResolveCurrentRefreshRate_DefaultReadingWithoutTargetRate_ClearsSoPreviousRateStaysSelectable(int enumRate)
    {
        var monitor = new Monitor { CurrentRefreshRate = 120 };

        var rate = MonitorManager.ResolveCurrentRefreshRate(enumRate, 0, Rates);
        monitor.CurrentRefreshRate = rate!.Value;

        Assert.AreEqual(-1, rate);
        Assert.AreNotEqual(120, monitor.CurrentRefreshRate);
    }

    [TestMethod]
    public void ResolveCurrentRefreshRate_NumericReading_IsUsedAsIs()
    {
        Assert.AreEqual(120, MonitorManager.ResolveCurrentRefreshRate(120, 60.0, Rates));
    }

    [TestMethod]
    public void ResolveCurrentRefreshRate_FailedRead_KeepsCachedValue()
    {
        Assert.IsNull(MonitorManager.ResolveCurrentRefreshRate(-1, 60.0, Rates));
    }

    private static Monitor CreateMonitor(string id, string gdiDeviceName) =>
        new()
        {
            Id = id,
            GdiDeviceName = gdiDeviceName,
        };

    private static MonitorDisplayInfo CreateDisplay(string monitorPath, string gdiDeviceName) =>
        new()
        {
            DevicePath = monitorPath + InterfaceSuffix,
            GdiDeviceName = gdiDeviceName,
        };
}
