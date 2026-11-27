// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class NetworkWidgetTests
{
    private const string AggregateName = "All physical network adapters";
    private static readonly NetworkInterfaceSnapshot Ethernet = new(1, new("11111111-1111-1111-1111-111111111111"), "Ethernet", 0, 0, 1_000_000_000, true)
    {
        Description = "Intel Ethernet Adapter",
        Type = NetworkInterfaceType.Ethernet,
        Status = OperationalStatus.Up,
    };

    private static readonly NetworkInterfaceSnapshot VirtualAdapter = new(2, new("22222222-2222-2222-2222-222222222222"), "vEthernet (WSL)", 0, 0, 10_000_000_000, false)
    {
        Description = "Hyper-V Virtual Ethernet Adapter",
        Type = NetworkInterfaceType.Ethernet,
        Status = OperationalStatus.Up,
    };

    private static readonly string EthernetId = "network-interface:" + Ethernet.InterfaceGuid.ToString("D");
    private static readonly string VirtualId = "network-interface:" + VirtualAdapter.InterfaceGuid.ToString("D");

    [TestMethod]
    public void Commands_CycleInBothDirectionsAndPersistTheDisplayedAdapter()
    {
        using var file = new SettingsTestFile();
        var settings = new SettingsManager(file.FilePath);
        var stats = CreateNetworkStats();
        using var widget = new SystemNetworkUsageWidgetPage(settings, () => stats);

        widget.UpdateWidget();
        AssertDisplayedName(widget, AggregateName);
        Invoke(widget, "prev");
        AssertDisplayedName(widget, VirtualAdapter.Name);
        Invoke(widget, "next");
        AssertDisplayedName(widget, AggregateName);
        Invoke(widget, "next");
        AssertDisplayedName(widget, Ethernet.Name);
        Invoke(widget, "next");
        AssertDisplayedName(widget, VirtualAdapter.Name);
        Invoke(widget, "prev");
        AssertDisplayedName(widget, Ethernet.Name);
        Invoke(widget, "setDefault");

        Assert.AreEqual(EthernetId, new SettingsManager(file.FilePath).DefaultNetworkAdapterId);
    }

    [TestMethod]
    public void Commands_KeepSelectionAcrossFilteringAndReordering()
    {
        using var file = new SettingsTestFile();
        var settings = new SettingsManager(file.FilePath);
        var stats = CreateNetworkStats();
        using var widget = new SystemNetworkUsageWidgetPage(settings, () => stats);
        settings.SetDefaultNetworkAdapterId(VirtualId);
        widget.UpdateWidget();
        AssertDisplayedName(widget, VirtualAdapter.Name);

        stats.ApplySnapshots([Ethernet], 1);
        widget.UpdateWidget();
        AssertDisplayedName(widget, AggregateName);

        stats.ApplySnapshots([VirtualAdapter, Ethernet], 1);
        widget.UpdateWidget();
        AssertDisplayedName(widget, VirtualAdapter.Name);
        Invoke(widget, "next");
        AssertDisplayedName(widget, Ethernet.Name);
        Invoke(widget, "prev");
        AssertDisplayedName(widget, VirtualAdapter.Name);
        Invoke(widget, "setDefault");

        Assert.AreEqual(VirtualId, new SettingsManager(file.FilePath).DefaultNetworkAdapterId);
    }

    [TestMethod]
    public void SetDefault_WhenSelectionIsHiddenPersistsTheDisplayedAggregate()
    {
        using var file = new SettingsTestFile();
        var settings = new SettingsManager(file.FilePath);
        settings.SetDefaultNetworkAdapterId(VirtualId);
        var stats = CreateNetworkStats();
        using var widget = new SystemNetworkUsageWidgetPage(settings, () => stats);
        widget.UpdateWidget();
        stats.ApplySnapshots([Ethernet], 1);

        Invoke(widget, "setDefault");

        Assert.AreEqual(NetworkStats.AllPhysicalAdaptersId, new SettingsManager(file.FilePath).DefaultNetworkAdapterId);
        stats.ApplySnapshots([Ethernet, VirtualAdapter], 1);
        widget.UpdateWidget();
        AssertDisplayedName(widget, AggregateName);
    }

    [TestMethod]
    [DataRow("next", "Ethernet")]
    [DataRow("prev", "Ethernet")]
    public void Navigation_WhenSelectionIsHiddenStartsFromAggregate(string command, string expected)
    {
        using var file = new SettingsTestFile();
        var settings = new SettingsManager(file.FilePath);
        settings.SetDefaultNetworkAdapterId(VirtualId);
        var stats = CreateNetworkStats();
        using var widget = new SystemNetworkUsageWidgetPage(settings, () => stats);
        widget.UpdateWidget();
        stats.ApplySnapshots([Ethernet], 1);

        Invoke(widget, command);

        AssertDisplayedName(widget, expected);
    }

    [TestMethod]
    [DataRow("next")]
    [DataRow("prev")]
    public void Navigation_WithNoAdaptersKeepsAggregate(string command)
    {
        using var file = new SettingsTestFile();
        var stats = CreateNetworkStats();
        stats.ApplySnapshots([], 1);
        using var widget = new SystemNetworkUsageWidgetPage(new SettingsManager(file.FilePath), () => stats);

        Invoke(widget, command);

        AssertDisplayedName(widget, AggregateName);
    }

    [TestMethod]
    public void AdapterDetails_AreSeparateUpdateStatusAndClearForAggregate()
    {
        using var file = new SettingsTestFile();
        var settings = new SettingsManager(file.FilePath);
        settings.SetDefaultNetworkAdapterId(VirtualId);
        var stats = CreateNetworkStats();
        using var widget = new SystemNetworkUsageWidgetPage(settings, () => stats);

        widget.UpdateWidget();

        var data = widget.ContentDataJson;
        Assert.AreEqual(VirtualAdapter.Name, data["networkName"]?.GetValue<string>());
        Assert.AreEqual(VirtualAdapter.Description, data["networkDescription"]?.GetValue<string>());
        Assert.AreEqual(Resources.GetResource("Network_Adapter_Type_Ethernet") + " · " + Resources.GetResource("Network_Adapter_Virtual"), data["networkType"]?.GetValue<string>());
        Assert.AreEqual(Resources.GetResource("Network_Adapter_Status_Connected"), data["networkStatus"]?.GetValue<string>());

        stats.ApplySnapshots([VirtualAdapter with { Status = OperationalStatus.Down }], 1);
        widget.UpdateWidget();
        Assert.AreEqual(Resources.GetResource("Network_Adapter_Status_Disconnected"), widget.ContentDataJson["networkStatus"]?.GetValue<string>());

        Invoke(widget, "next");
        data = widget.ContentDataJson;
        Assert.AreEqual(AggregateName, data["networkName"]?.GetValue<string>());
        Assert.IsFalse(data.ContainsKey("networkDescription"));
        Assert.IsFalse(data.ContainsKey("networkType"));
        Assert.IsFalse(data.ContainsKey("networkStatus"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("Ethernet")]
    [DataRow("ethernet")]
    public void AdapterDetails_HideEmptyOrDuplicateDescription(string description)
    {
        using var file = new SettingsTestFile();
        var settings = new SettingsManager(file.FilePath);
        settings.SetDefaultNetworkAdapterId(EthernetId);
        var stats = CreateNetworkStats();
        stats.ApplySnapshots([Ethernet with { Description = description }], 1);
        using var widget = new SystemNetworkUsageWidgetPage(settings, () => stats);

        widget.UpdateWidget();

        AssertDisplayedName(widget, Ethernet.Name);
        Assert.IsFalse(widget.ContentDataJson.ContainsKey("networkDescription"));
    }

    [TestMethod]
    [DataRow(NetworkInterfaceType.Ethernet, OperationalStatus.Up, "Ethernet", "Connected")]
    [DataRow(NetworkInterfaceType.Ethernet3Megabit, OperationalStatus.Up, "Ethernet", "Connected")]
    [DataRow(NetworkInterfaceType.FastEthernetT, OperationalStatus.Up, "Ethernet", "Connected")]
    [DataRow(NetworkInterfaceType.FastEthernetFx, OperationalStatus.Up, "Ethernet", "Connected")]
    [DataRow(NetworkInterfaceType.GigabitEthernet, OperationalStatus.Up, "Ethernet", "Connected")]
    [DataRow(NetworkInterfaceType.Wireless80211, OperationalStatus.Up, "WiFi", "Connected")]
    [DataRow(NetworkInterfaceType.Ppp, OperationalStatus.Testing, "Ppp", "Testing")]
    [DataRow(NetworkInterfaceType.Tunnel, OperationalStatus.Dormant, "Tunnel", "Dormant")]
    [DataRow(NetworkInterfaceType.Wwanpp, OperationalStatus.NotPresent, "MobileBroadband", "NotPresent")]
    [DataRow(NetworkInterfaceType.Unknown, OperationalStatus.LowerLayerDown, "Unknown", "LowerLayerDown")]
    [DataRow((NetworkInterfaceType)53, OperationalStatus.Up, "Other", "Connected")]
    [DataRow((NetworkInterfaceType)999, (OperationalStatus)999, "Other", "Unknown")]
    public void AdapterDetails_LabelInterfaceTypeAndStatus(NetworkInterfaceType type, OperationalStatus status, string expectedType, string expectedStatus)
    {
        using var file = new SettingsTestFile();
        var settings = new SettingsManager(file.FilePath);
        settings.SetDefaultNetworkAdapterId(EthernetId);
        var stats = CreateNetworkStats();
        stats.ApplySnapshots([Ethernet with { Type = type, Status = status }], 1);
        using var widget = new SystemNetworkUsageWidgetPage(settings, () => stats);

        widget.UpdateWidget();

        var data = widget.ContentDataJson;
        Assert.AreEqual(Resources.GetResource("Network_Adapter_Type_" + expectedType) + " · " + Resources.GetResource("Network_Adapter_Physical"), data["networkType"]?.GetValue<string>());
        Assert.AreEqual(Resources.GetResource("Network_Adapter_Status_" + expectedStatus), data["networkStatus"]?.GetValue<string>());
    }

    [TestMethod]
    public void AdapterDetails_ProprietaryVirtualInterfaceHasOneVirtualLabel()
    {
        using var file = new SettingsTestFile();
        var settings = new SettingsManager(file.FilePath);
        settings.SetDefaultNetworkAdapterId(VirtualId);
        var stats = CreateNetworkStats();
        stats.ApplySnapshots([VirtualAdapter with { Type = (NetworkInterfaceType)53 }], 1);
        using var widget = new SystemNetworkUsageWidgetPage(settings, () => stats);

        widget.UpdateWidget();

        Assert.AreEqual(Resources.GetResource("Network_Adapter_Type_Virtual"), widget.ContentDataJson["networkType"]?.GetValue<string>());
    }

    private static void Invoke(SystemNetworkUsageWidgetPage widget, string command)
    {
        var item = widget.Commands.OfType<ICommandContextItem>().Single(item => item.Command.Id == "com.microsoft.cmdpal.network_widget." + command);
        ((IInvokableCommand)item.Command).Invoke(widget);
    }

    private static void AssertDisplayedName(SystemNetworkUsageWidgetPage widget, string expected)
    {
        var data = widget.ContentDataJson;
        Assert.AreEqual(expected, data["networkName"]?.GetValue<string>());
    }

    private static NetworkStats CreateNetworkStats()
    {
        var stats = new NetworkStats(new UnusedSnapshotProvider(), AggregateName);
        stats.ApplySnapshots([Ethernet, VirtualAdapter], 0);
        return stats;
    }

    private sealed class UnusedSnapshotProvider : INetworkInterfaceSnapshotProvider
    {
        public IReadOnlyList<NetworkInterfaceSnapshot> GetSnapshots() => throw new InvalidOperationException("This provider is not used by these tests.");
    }
}
