// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using FancyZonesCLI.CommandLine.Commands;
using FancyZonesCLI.Utils;
using FancyZonesEditorCommon.Data;

namespace UnitTestsFancyZonesEditor;

[TestClass]
public class FancyZonesCliAppliedLayoutsTests
{
    private const string MonitorName = "\\\\?\\DISPLAY#DEL4362";
    private const string SerialNumber = "6TJLB84";
    private const string VirtualDesktop = "{00000000-0000-0000-0000-000000000001}";
    private const string CustomLayoutUuid = "{5B5C93B2-9C7E-4E47-BB02-26BD08B05AB8}";

    [TestMethod]
    public void MatchesDeviceUsesInstanceBeforeMonitorNumber()
    {
        var storedDevice = CreateDevice("instance-1", 1);

        Assert.IsFalse(AppliedLayoutsHelper.MatchesDevice(storedDevice, MonitorName, "instance-2", SerialNumber, 1, VirtualDesktop));
        Assert.IsTrue(AppliedLayoutsHelper.MatchesDevice(storedDevice, MonitorName, "instance-1", SerialNumber, 2, VirtualDesktop));
    }

    [TestMethod]
    public void MatchesDeviceFallsBackToMonitorNumberWhenInstanceIsMissing()
    {
        var storedDevice = CreateDevice(string.Empty, 2);

        Assert.IsTrue(AppliedLayoutsHelper.MatchesDevice(storedDevice, MonitorName, "instance-2", SerialNumber, 2, VirtualDesktop));
        Assert.IsFalse(AppliedLayoutsHelper.MatchesDevice(storedDevice, MonitorName, "instance-3", SerialNumber, 3, VirtualDesktop));
    }

    [TestMethod]
    public void MatchesDeviceRetainsLegacyFallbackWhenBothIdentityFieldsAreMissing()
    {
        var storedDevice = CreateDevice(string.Empty, 0);

        Assert.IsTrue(AppliedLayoutsHelper.MatchesDevice(storedDevice, MonitorName, string.Empty, SerialNumber, 0, VirtualDesktop));
        Assert.IsFalse(AppliedLayoutsHelper.MatchesDevice(storedDevice, MonitorName, string.Empty, "different-serial", 0, VirtualDesktop));
    }

    [TestMethod]
    public void FindLayoutForMonitorSelectsTheMatchingInstanceAmongIdenticalDisplays()
    {
        var layouts = new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
            {
                CreateLayout("instance-1", 1, "focus"),
                CreateLayout("instance-2", 2, "columns"),
                CreateLayout("instance-3", 3, "rows"),
            },
        };
        var connectedMonitors = new List<EditorParameters.NativeMonitorDataWrapper>
        {
            CreateMonitor("instance-1", 1),
            CreateMonitor("instance-2", 2),
            CreateMonitor("instance-3", 3),
        };

        var match = AppliedLayoutsHelper.FindLayoutForMonitor(
            layouts,
            MonitorName,
            "instance-2",
            SerialNumber,
            2,
            VirtualDesktop,
            connectedMonitors);

        Assert.IsTrue(match.HasValue);
        Assert.AreEqual("columns", match.Value.AppliedLayout.Type);
    }

    [TestMethod]
    public void FindLayoutForMonitorDoesNotGuessWhenLegacyIdentityIsAmbiguous()
    {
        var layouts = new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
            {
                CreateLayout(string.Empty, 0, "focus"),
            },
        };
        var connectedMonitors = new List<EditorParameters.NativeMonitorDataWrapper>
        {
            CreateMonitor("instance-1", 1),
            CreateMonitor("instance-2", 2),
        };

        var match = AppliedLayoutsHelper.FindLayoutForMonitor(
            layouts,
            MonitorName,
            "instance-1",
            SerialNumber,
            1,
            VirtualDesktop,
            connectedMonitors);

        Assert.IsFalse(match.HasValue);
    }

    [TestMethod]
    public void FindLayoutForMonitorAllowsUniqueLegacyIdentity()
    {
        var layouts = new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
            {
                CreateLayout(string.Empty, 0, "focus"),
            },
        };
        var connectedMonitors = new List<EditorParameters.NativeMonitorDataWrapper>
        {
            CreateMonitor("instance-1", 1),
        };

        var match = AppliedLayoutsHelper.FindLayoutForMonitor(
            layouts,
            MonitorName,
            "instance-1",
            SerialNumber,
            1,
            VirtualDesktop,
            connectedMonitors);

        Assert.IsTrue(match.HasValue);
        Assert.AreEqual("focus", match.Value.AppliedLayout.Type);
    }

    [TestMethod]
    public void MergeWithHistoricalLayoutsReplacesOnlyTheTargetIdenticalMonitor()
    {
        var existingLayouts = new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
            {
                CreateLayout("instance-1", 1, "focus"),
                CreateLayout("instance-2", 2, "rows"),
                CreateLayout("instance-3", 3, "columns"),
            },
        };
        var newLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
        {
            CreateLayout("instance-2", 2, "custom", CustomLayoutUuid),
        };
        var connectedMonitors = new List<EditorParameters.NativeMonitorDataWrapper>
        {
            CreateMonitor("instance-1", 1),
            CreateMonitor("instance-2", 2),
            CreateMonitor("instance-3", 3),
        };

        var merged = SetLayoutCommand.MergeWithHistoricalLayouts(existingLayouts, newLayouts, connectedMonitors);

        Assert.AreEqual(3, merged.AppliedLayouts.Count);
        Assert.AreEqual("focus", FindLayout(merged, "instance-1").AppliedLayout.Type);
        Assert.AreEqual("custom", FindLayout(merged, "instance-2").AppliedLayout.Type);
        Assert.AreEqual(CustomLayoutUuid, FindLayout(merged, "instance-2").AppliedLayout.Uuid);
        Assert.AreEqual("columns", FindLayout(merged, "instance-3").AppliedLayout.Type);
    }

    [TestMethod]
    public void MergeWithHistoricalLayoutsFallsBackToMonitorNumber()
    {
        var existingLayouts = new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
            {
                CreateLayout(string.Empty, 1, "focus"),
                CreateLayout(string.Empty, 2, "rows"),
            },
        };
        var newLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
        {
            CreateLayout(string.Empty, 2, "custom", CustomLayoutUuid),
        };
        var connectedMonitors = new List<EditorParameters.NativeMonitorDataWrapper>
        {
            CreateMonitor(string.Empty, 1),
            CreateMonitor(string.Empty, 2),
        };

        var merged = SetLayoutCommand.MergeWithHistoricalLayouts(existingLayouts, newLayouts, connectedMonitors);

        Assert.AreEqual(2, merged.AppliedLayouts.Count);
        Assert.AreEqual("focus", FindLayout(merged, string.Empty, 1).AppliedLayout.Type);
        Assert.AreEqual("custom", FindLayout(merged, string.Empty, 2).AppliedLayout.Type);
        Assert.AreEqual(CustomLayoutUuid, FindLayout(merged, string.Empty, 2).AppliedLayout.Uuid);
    }

    [TestMethod]
    public void MergeWithHistoricalLayoutsRejectsAmbiguousLegacyIdentityForPartialUpdate()
    {
        var existingLayouts = new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
            {
                CreateLayout(string.Empty, 0, "focus"),
            },
        };
        var newLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
        {
            CreateLayout("instance-1", 1, "{custom-layout}"),
        };
        var connectedMonitors = new List<EditorParameters.NativeMonitorDataWrapper>
        {
            CreateMonitor("instance-1", 1),
            CreateMonitor("instance-2", 2),
        };

        Assert.ThrowsExactly<InvalidOperationException>(
            () => SetLayoutCommand.MergeWithHistoricalLayouts(existingLayouts, newLayouts, connectedMonitors));
    }

    [TestMethod]
    public void MergeWithHistoricalLayoutsReplacesLegacyRecordsWhenAllPeersAreUpdated()
    {
        var existingLayouts = new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
            {
                CreateLayout(string.Empty, 0, "focus"),
            },
        };
        var newLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>
        {
            CreateLayout("instance-1", 1, "columns"),
            CreateLayout("instance-2", 2, "columns"),
        };
        var connectedMonitors = new List<EditorParameters.NativeMonitorDataWrapper>
        {
            CreateMonitor("instance-1", 1),
            CreateMonitor("instance-2", 2),
        };

        var merged = SetLayoutCommand.MergeWithHistoricalLayouts(existingLayouts, newLayouts, connectedMonitors);

        Assert.AreEqual(2, merged.AppliedLayouts.Count);
        Assert.AreEqual("columns", FindLayout(merged, "instance-1").AppliedLayout.Type);
        Assert.AreEqual("columns", FindLayout(merged, "instance-2").AppliedLayout.Type);
    }

    private static AppliedLayouts.AppliedLayoutWrapper FindLayout(
        AppliedLayouts.AppliedLayoutsListWrapper layouts,
        string monitorInstance,
        int? monitorNumber = null)
    {
        foreach (var layout in layouts.AppliedLayouts)
        {
            if (layout.Device.MonitorInstance == monitorInstance &&
                (!monitorNumber.HasValue || layout.Device.MonitorNumber == monitorNumber.Value))
            {
                return layout;
            }
        }

        Assert.Fail("Expected monitor layout was not found.");
        return default;
    }

    private static AppliedLayouts.AppliedLayoutWrapper CreateLayout(
        string monitorInstance,
        int monitorNumber,
        string type,
        string uuid = "{00000000-0000-0000-0000-000000000000}")
    {
        return new AppliedLayouts.AppliedLayoutWrapper
        {
            Device = CreateDevice(monitorInstance, monitorNumber),
            AppliedLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
            {
                Uuid = uuid,
                Type = type,
            },
        };
    }

    private static AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper CreateDevice(string monitorInstance, int monitorNumber)
    {
        return new AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper
        {
            Monitor = MonitorName,
            MonitorInstance = monitorInstance,
            MonitorNumber = monitorNumber,
            SerialNumber = SerialNumber,
            VirtualDesktop = VirtualDesktop,
        };
    }

    private static EditorParameters.NativeMonitorDataWrapper CreateMonitor(string monitorInstance, int monitorNumber)
    {
        return new EditorParameters.NativeMonitorDataWrapper
        {
            Monitor = MonitorName,
            MonitorInstanceId = monitorInstance,
            MonitorNumber = monitorNumber,
            MonitorSerialNumber = SerialNumber,
            VirtualDesktop = VirtualDesktop,
        };
    }
}
