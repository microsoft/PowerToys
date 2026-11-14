// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Net.NetworkInformation;
using CoreWidgetProvider.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Win32.NetworkManagement.IpHelper;
using Windows.Win32.NetworkManagement.Ndis;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class NetworkInterfaceSnapshotProviderTests
{
    [TestMethod]
    [DataRow(NetworkInterfaceType.Ethernet, true, false, true)]
    [DataRow(NetworkInterfaceType.Ethernet, true, true, true)]
    [DataRow(NetworkInterfaceType.Wireless80211, true, true, true)]
    [DataRow(NetworkInterfaceType.Ethernet, false, false, true)]
    [DataRow(NetworkInterfaceType.Ethernet, false, true, false)]
    [DataRow(NetworkInterfaceType.Ppp, false, false, true)]
    [DataRow(NetworkInterfaceType.Ppp, false, true, false)]
    [DataRow(NetworkInterfaceType.Tunnel, false, false, true)]
    [DataRow(NetworkInterfaceType.Tunnel, false, true, false)]
    [DataRow((NetworkInterfaceType)53, false, false, true)]
    [DataRow((NetworkInterfaceType)53, false, true, false)]
    public void ShouldInclude_FiltersVirtualAdaptersUsingHardwareFlag(
        NetworkInterfaceType type, bool hardwareInterface, bool excludeVirtualAdapters, bool expected)
    {
        var row = new MIB_IF_ROW2
        {
            Type = (uint)type,
            OperStatus = IF_OPER_STATUS.IfOperStatusUp,
        };
        row.InterfaceAndOperStatusFlags.HardwareInterface = hardwareInterface;

        Assert.AreEqual(expected, NetworkInterfaceSnapshotProvider.ShouldInclude(row, true, excludeVirtualAdapters, true));
    }

    [TestMethod]
    [DataRow(OperationalStatus.Up, true)]
    [DataRow(OperationalStatus.Down, false)]
    [DataRow(OperationalStatus.Testing, false)]
    [DataRow(OperationalStatus.Unknown, false)]
    [DataRow(OperationalStatus.Dormant, false)]
    [DataRow(OperationalStatus.NotPresent, false)]
    [DataRow(OperationalStatus.LowerLayerDown, false)]
    public void ShouldInclude_DisconnectedFilterUsesOperationalStatus(OperationalStatus status, bool expected)
    {
        var row = new MIB_IF_ROW2
        {
            Type = (uint)NetworkInterfaceType.Ethernet,
            OperStatus = (IF_OPER_STATUS)status,
        };
        row.InterfaceAndOperStatusFlags.HardwareInterface = true;

        Assert.AreEqual(expected, NetworkInterfaceSnapshotProvider.ShouldInclude(row, true, false, true));
        Assert.IsTrue(NetworkInterfaceSnapshotProvider.ShouldInclude(row, true, false, false));
    }

    [TestMethod]
    [DataRow(NetworkInterfaceType.Loopback, false, false)]
    [DataRow(NetworkInterfaceType.Ethernet, true, false)]
    [DataRow(NetworkInterfaceType.Ethernet, false, true)]
    public void ShouldInclude_AlwaysExcludesLoopbackFilterAndEndpointInterfaces(
        NetworkInterfaceType type, bool filterInterface, bool endpointInterface)
    {
        var row = new MIB_IF_ROW2
        {
            Type = (uint)type,
            OperStatus = IF_OPER_STATUS.IfOperStatusUp,
        };
        row.InterfaceAndOperStatusFlags.HardwareInterface = true;
        row.InterfaceAndOperStatusFlags.FilterInterface = filterInterface;
        row.InterfaceAndOperStatusFlags.EndPointInterface = endpointInterface;

        Assert.IsFalse(NetworkInterfaceSnapshotProvider.ShouldInclude(row, true, false, false));
        Assert.IsFalse(NetworkInterfaceSnapshotProvider.ShouldInclude(row, true, true, true));
    }

    [TestMethod]
    public void GetSnapshots_ExcludesInternalVirtualInterfacesWithoutIpBindings()
    {
        var row = CreateRow(Guid.NewGuid(), "Internal adapter", "WAN Miniport (IP)", false);
        var provider = new NetworkInterfaceSnapshotProvider(() => []);

        Assert.AreEqual(0, provider.GetSnapshots([row]).Count);
    }

    [TestMethod]
    [DataRow("vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter #2", "vEthernet (WSL)")]
    [DataRow("Ethernet", "Intel Ethernet Adapter", "Ethernet")]
    [DataRow("VPN", "VPN", "VPN")]
    [DataRow("VPN", "vpn", "VPN")]
    [DataRow("Ethernet", "", "Ethernet")]
    [DataRow("Ethernet", " ", "Ethernet")]
    [DataRow(" ", "VPN driver", "VPN driver")]
    [DataRow("", "", "11111111-1111-1111-1111-111111111111")]
    public void GetSnapshots_KeepsDescriptionSeparateFromNameWithFallbacks(string alias, string description, string expected)
    {
        var id = new Guid("11111111-1111-1111-1111-111111111111");
        var row = CreateRow(id, alias, description, false);
        var provider = new NetworkInterfaceSnapshotProvider(() => [row.InterfaceLuid.Value]);

        var snapshots = provider.GetSnapshots([row]);

        Assert.AreEqual(1, snapshots.Count);
        Assert.AreEqual(expected, snapshots[0].Name);
        Assert.AreEqual(description, snapshots[0].Description);
        Assert.IsFalse(snapshots[0].IsHardware);
    }

    [TestMethod]
    public void GetSnapshots_PreservesInterfaceTypeAndOperationalStatus()
    {
        using var file = new SettingsTestFile();
        var settings = new SettingsManager(file.FilePath);
        settings.Settings.Update("""{"performanceMonitor.ExcludeDisconnectedNetworkAdapters": "false"}""");
        var row = CreateRow(Guid.NewGuid(), "Wi-Fi", "Wireless adapter", true);
        row.Type = (uint)NetworkInterfaceType.Wireless80211;
        row.OperStatus = IF_OPER_STATUS.IfOperStatusDormant;
        var provider = new NetworkInterfaceSnapshotProvider(() => []);
        provider.Configure(settings);

        var snapshots = provider.GetSnapshots([row]);

        Assert.AreEqual(1, snapshots.Count);
        Assert.AreEqual(NetworkInterfaceType.Wireless80211, snapshots[0].Type);
        Assert.AreEqual(OperationalStatus.Dormant, snapshots[0].Status);
    }

    [TestMethod]
    public void GetSnapshots_UsesConfiguredSettingsForInitialAndSubsequentReads()
    {
        using var file = new SettingsTestFile();
        var settings = new SettingsManager(file.FilePath);
        settings.Settings.Update("""
            {
                "performanceMonitor.ExcludeVirtualNetworkAdapters": "true",
                "performanceMonitor.ExcludeDisconnectedNetworkAdapters": "false"
            }
            """);
        var physical = CreateRow(Guid.NewGuid(), "Ethernet", "Physical NIC", true);
        var virtualAdapter = CreateRow(Guid.NewGuid(), "vEthernet (WSL)", "Virtual NIC", false, 2);
        var disconnected = CreateRow(Guid.NewGuid(), "USB Ethernet", "USB NIC", true, 3);
        disconnected.OperStatus = IF_OPER_STATUS.IfOperStatusDown;
        MIB_IF_ROW2[] rows = [physical, virtualAdapter, disconnected];
        var lookupCount = 0;
        var provider = new NetworkInterfaceSnapshotProvider(() =>
        {
            lookupCount++;
            return [virtualAdapter.InterfaceLuid.Value];
        });
        provider.Configure(settings);

        var snapshots = provider.GetSnapshots(rows);
        Assert.AreEqual(0, lookupCount);
        Assert.AreEqual(2, snapshots.Count);
        Assert.AreEqual("Ethernet", snapshots[0].Name);
        Assert.IsTrue(snapshots[0].IsHardware);
        Assert.AreEqual("USB Ethernet", snapshots[1].Name);

        settings.Settings.Update("""
            {
                "performanceMonitor.ExcludeVirtualNetworkAdapters": "false",
                "performanceMonitor.ExcludeDisconnectedNetworkAdapters": "true"
            }
            """);
        snapshots = provider.GetSnapshots(rows);
        Assert.AreEqual(1, lookupCount);
        Assert.AreEqual(2, snapshots.Count);
        Assert.AreEqual("Ethernet", snapshots[0].Name);
        Assert.AreEqual("vEthernet (WSL)", snapshots[1].Name);

        settings.Settings.Update("""{"performanceMonitor.ExcludeVirtualNetworkAdapters": "true"}""");
        snapshots = provider.GetSnapshots(rows);
        Assert.AreEqual(1, snapshots.Count);
        Assert.AreEqual(1, lookupCount);

        settings.Settings.Update("""{"performanceMonitor.ExcludeVirtualNetworkAdapters": "false"}""");
        snapshots = provider.GetSnapshots(rows);
        Assert.AreEqual(2, snapshots.Count);
        Assert.AreEqual(2, lookupCount);
    }

    [TestMethod]
    public void GetSnapshots_RefreshesIpInterfacesOnEverySampleWithoutNotifications()
    {
        var physical = CreateRow(Guid.NewGuid(), "Ethernet", "Physical NIC", true);
        var vpn = CreateRow(Guid.NewGuid(), "VPN", "Virtual NIC", false, 2);
        var wsl = CreateRow(Guid.NewGuid(), "WSL", "Virtual NIC", false, 3);
        MIB_IF_ROW2[] rows = [physical, vpn, wsl];
        var lookupCount = 0;
        var provider = new NetworkInterfaceSnapshotProvider(() => ++lookupCount == 1 ? [2] : [3]);

        var snapshots = provider.GetSnapshots(rows);
        Assert.AreEqual(2, snapshots.Count);
        Assert.AreEqual(2UL, snapshots[1].InterfaceLuid);
        Assert.AreEqual(1, lookupCount);

        var refreshed = provider.GetSnapshots(rows);
        Assert.AreEqual(2, lookupCount);
        Assert.AreEqual(2, refreshed.Count);
        Assert.AreEqual(3UL, refreshed[1].InterfaceLuid);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GetSnapshots_IpLookupFailureKeepsPhysicalCountersUpdatingAndRetriesNextSample(bool hasPreviousInterfaces)
    {
        var physical = CreateRow(Guid.NewGuid(), "Ethernet", "Physical NIC", true);
        var virtualAdapter = CreateRow(Guid.NewGuid(), "VPN", "Virtual NIC", false, 2);
        MIB_IF_ROW2[] rows = [physical, virtualAdapter];
        var lookupCount = 0;
        var lookupFails = false;
        var provider = new NetworkInterfaceSnapshotProvider(() =>
        {
            lookupCount++;
            if (lookupFails)
            {
                throw new Win32Exception("IP lookup failed.");
            }

            return [virtualAdapter.InterfaceLuid.Value];
        });
        if (hasPreviousInterfaces)
        {
            provider.GetSnapshots(rows);
        }

        lookupFails = true;
        var stats = new NetworkStats(provider, "All physical network adapters");
        stats.ApplySnapshots(provider.GetSnapshots(rows), 0);
        rows[0].InOctets = 1_000;
        rows[0].OutOctets = 500;
        var snapshots = provider.GetSnapshots(rows);
        stats.ApplySnapshots(snapshots, 1);

        Assert.AreEqual(hasPreviousInterfaces ? 2 : 1, snapshots.Count);
        Assert.AreEqual(hasPreviousInterfaces ? 3 : 2, lookupCount);
        var total = stats.GetNetworkAdapter(NetworkStats.AllPhysicalAdaptersId).Usage;
        Assert.AreEqual(1_000f, total.Received);
        Assert.AreEqual(500f, total.Sent);

        lookupFails = false;
        Assert.AreEqual(2, provider.GetSnapshots(rows).Count);
        Assert.AreEqual(hasPreviousInterfaces ? 4 : 3, lookupCount);
    }

    [TestMethod]
    public void GetSnapshots_EmptyIpTableReplacesThePreviousResult()
    {
        var row = CreateRow(Guid.NewGuid(), "VPN", "Virtual NIC", false);
        var lookupCount = 0;
        var provider = new NetworkInterfaceSnapshotProvider(() =>
        {
            if (++lookupCount == 1)
            {
                return [row.InterfaceLuid.Value];
            }

            return [];
        });

        Assert.AreEqual(1, provider.GetSnapshots([row]).Count);
        Assert.AreEqual(0, provider.GetSnapshots([row]).Count);
        Assert.AreEqual(2, lookupCount);
    }

    [TestMethod]
    [DataRow(OperationalStatus.Down)]
    [DataRow(OperationalStatus.Dormant)]
    [DataRow(OperationalStatus.NotPresent)]
    [DataRow(OperationalStatus.LowerLayerDown)]
    [DataRow(OperationalStatus.Testing)]
    [DataRow(OperationalStatus.Unknown)]
    public void GetSnapshots_InactiveAdaptersDoNotInflateAggregateBandwidth(OperationalStatus status)
    {
        using var file = new SettingsTestFile();
        var settings = new SettingsManager(file.FilePath);
        settings.Settings.Update("""{"performanceMonitor.ExcludeDisconnectedNetworkAdapters": "false"}""");
        var connected = CreateRow(Guid.NewGuid(), "Ethernet", "Physical NIC", true);
        connected.ReceiveLinkSpeed = 1_000_000_000;
        var disconnected = CreateRow(Guid.NewGuid(), "USB Ethernet", "USB NIC", true, 2);
        disconnected.OperStatus = (IF_OPER_STATUS)status;
        disconnected.ReceiveLinkSpeed = 10_000_000_000;
        disconnected.TransmitLinkSpeed = 10_000_000_000;
        var provider = new NetworkInterfaceSnapshotProvider(() => []);
        provider.Configure(settings);
        var stats = new NetworkStats(provider, "All physical network adapters");
        stats.ApplySnapshots(provider.GetSnapshots([connected, disconnected]), 0);

        connected.InOctets = 125_000_000;
        var snapshots = provider.GetSnapshots([connected, disconnected]);
        stats.ApplySnapshots(snapshots, 1);

        Assert.AreEqual(2, snapshots.Count);
        Assert.AreEqual(0UL, snapshots[1].LinkSpeed);
        Assert.AreEqual(1f, stats.GetNetworkAdapter(NetworkStats.AllPhysicalAdaptersId).Usage.Usage);
        Assert.AreEqual(0f, stats.GetNetworkAdapter("network-interface:" + disconnected.InterfaceGuid.ToString("D")).Usage.Usage);
    }

    private static MIB_IF_ROW2 CreateRow(Guid id, string alias, string description, bool hardware, ulong luid = 1)
    {
        var row = new MIB_IF_ROW2
        {
            InterfaceGuid = id,
            Type = (uint)NetworkInterfaceType.Ethernet,
            OperStatus = IF_OPER_STATUS.IfOperStatusUp,
        };
        row.InterfaceLuid.Value = luid;
        row.InterfaceAndOperStatusFlags.HardwareInterface = hardware;
        alias.AsSpan().CopyTo(row.Alias.AsSpan());
        description.AsSpan().CopyTo(row.Description.AsSpan());
        return row;
    }
}
