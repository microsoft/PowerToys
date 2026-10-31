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
