// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DockPinTargetMonitorsTests
{
    private static readonly MonitorInfo PrimaryMonitor = new()
    {
        DeviceId = @"\\.\DISPLAY1",
        StableId = @"\\?\DISPLAY#DEL41A3#UID1#{guid}",
        FriendlyName = "DELL U2723QE",
        DisplayName = "DELL U2723QE (Primary)",
        Bounds = new ScreenRect(0, 0, 1920, 1080),
        WorkArea = new ScreenRect(0, 0, 1920, 1040),
        Dpi = 96,
        IsPrimary = true,
    };

    private static readonly MonitorInfo SecondaryMonitor = new()
    {
        DeviceId = @"\\.\DISPLAY2",
        StableId = @"\\?\DISPLAY#DEL41A3#UID2#{guid}",
        FriendlyName = "DELL U2723QE",
        DisplayName = "DELL U2723QE",
        Bounds = new ScreenRect(1920, 0, 3840, 1080),
        WorkArea = new ScreenRect(1920, 0, 3840, 1040),
        Dpi = 96,
        IsPrimary = false,
    };

    [TestMethod]
    public void Get_UsesCustomMonitorNames()
    {
        var settings = new DockSettings
        {
            MonitorConfigs = ImmutableList.Create(
                new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, DisplayNameOverride = "Left desk" },
                new DockMonitorConfig { MonitorDeviceId = SecondaryMonitor.StableId, DisplayNameOverride = "Right desk" }),
        };

        var result = DockPinTargetMonitors.Get([PrimaryMonitor, SecondaryMonitor], settings);

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(new[] { "Left desk (Primary)", "Right desk" }, result.Select(m => m.DisplayName).ToArray());
        CollectionAssert.AreEqual(new[] { PrimaryMonitor.StableId, SecondaryMonitor.StableId }, result.Select(m => m.StableId).ToArray());
    }

    [TestMethod]
    public void Get_WithoutCustomName_UsesSettingsDefaultName()
    {
        var unnamedMonitor = SecondaryMonitor with { FriendlyName = null, DisplayName = @"\\.\DISPLAY2" };
        var settings = new DockSettings
        {
            MonitorConfigs = ImmutableList.Create(
                new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId },
                new DockMonitorConfig { MonitorDeviceId = unnamedMonitor.StableId, FallbackDisplayNumber = 2 }),
        };

        var result = DockPinTargetMonitors.Get([PrimaryMonitor, unnamedMonitor], settings);

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(new[] { "DELL U2723QE (Primary)", "Display B" }, result.Select(m => m.DisplayName).ToArray());
    }

    [TestMethod]
    public void Get_ExcludesMonitorsWithDockDisabledOrNoConfig()
    {
        var thirdMonitor = SecondaryMonitor with { DeviceId = @"\\.\DISPLAY3", StableId = "third" };
        var settings = new DockSettings
        {
            MonitorConfigs = ImmutableList.Create(
                new DockMonitorConfig { MonitorDeviceId = PrimaryMonitor.StableId, DisplayNameOverride = "Left desk" },
                new DockMonitorConfig { MonitorDeviceId = SecondaryMonitor.StableId, Enabled = false }),
        };

        var result = DockPinTargetMonitors.Get([PrimaryMonitor, SecondaryMonitor, thirdMonitor], settings);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("Left desk (Primary)", result[0].DisplayName);
    }

    [TestMethod]
    public void Get_WithoutMonitorConfigs_ReturnsOnlyPrimary()
    {
        var result = DockPinTargetMonitors.Get([PrimaryMonitor, SecondaryMonitor], new DockSettings());

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(PrimaryMonitor.StableId, result[0].StableId);
        Assert.AreEqual("DELL U2723QE (Primary)", result[0].DisplayName);
    }

    [TestMethod]
    public void Get_WithoutMonitors_ReturnsNull()
    {
        Assert.IsNull(DockPinTargetMonitors.Get(null, new DockSettings()));
    }
}
