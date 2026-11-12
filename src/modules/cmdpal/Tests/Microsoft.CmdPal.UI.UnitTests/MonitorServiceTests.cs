// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Services;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class MonitorServiceTests
{
    private static readonly MonitorInfo PrimaryMonitor = new()
    {
        DeviceId = @"\\.\DISPLAY1",
        StableId = "primary-monitor",
        DisplayName = "Primary monitor",
        Bounds = new ScreenRect(0, 0, 1920, 1080),
        WorkArea = new ScreenRect(0, 0, 1920, 1040),
        Dpi = 96,
        IsPrimary = true,
    };

    private static readonly MonitorInfo SecondaryMonitor = PrimaryMonitor with
    {
        DeviceId = @"\\.\DISPLAY2",
        StableId = "secondary-monitor",
        DisplayName = "Secondary monitor",
        Bounds = new ScreenRect(1920, 0, 3840, 1080),
        WorkArea = new ScreenRect(1920, 0, 3840, 1040),
        Dpi = 144,
        IsPrimary = false,
    };

    [TestMethod]
    public void GetMonitors_ForceRefresh_ReplacesSnapshotReadDuringTopologyChange()
    {
        IReadOnlyList<MonitorInfo> connectedMonitors = [PrimaryMonitor, SecondaryMonitor];
        var service = new MonitorService(() => connectedMonitors);
        var notifications = 0;
        service.MonitorsChanged += (_, _) => notifications++;
        var original = service.GetMonitors();

        connectedMonitors = [PrimaryMonitor];
        service.NotifyMonitorsChanged();
        var transient = service.GetMonitors();
        Assert.HasCount(1, transient);

        connectedMonitors = [PrimaryMonitor, SecondaryMonitor];
        Assert.AreSame(transient, service.GetMonitors());

        var refreshed = service.GetMonitors(forceRefresh: true);

        Assert.HasCount(2, refreshed);
        Assert.AreNotSame(original, refreshed);
        Assert.AreSame(refreshed, service.GetMonitors());
        Assert.AreSame(SecondaryMonitor, service.GetMonitorByStableId(SecondaryMonitor.StableId));
        Assert.HasCount(1, transient, "Refreshing must not mutate a snapshot held by another reader.");
        Assert.AreEqual(1, notifications, "Refreshing must not schedule another topology-change debounce.");
    }

    [TestMethod]
    public void GetMonitors_WithoutRefresh_ReusesSnapshotUntilInvalidated()
    {
        var enumerations = 0;
        var service = new MonitorService(() =>
        {
            enumerations++;
            return new[] { PrimaryMonitor };
        });

        var original = service.GetMonitors();
        Assert.AreSame(original, service.GetMonitors());
        Assert.AreSame(PrimaryMonitor, service.GetPrimaryMonitor());
        Assert.AreSame(PrimaryMonitor, service.GetMonitorByDeviceId(PrimaryMonitor.DeviceId));
        Assert.AreEqual(1, enumerations);

        service.NotifyMonitorsChanged();

        Assert.AreNotSame(original, service.GetMonitors());
        Assert.AreEqual(2, enumerations);
    }
}
