// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class MonitorIdentityTests
{
    private static readonly DockBandSettings BandA = new() { ProviderId = "p", CommandId = "a" };
    private static readonly DockBandSettings BandB = new() { ProviderId = "p", CommandId = "b" };
    private static readonly string[] ExpectedMergedCommands = ["a", "b"];
    private static readonly string[] ExpectedBandA = ["a"];
    private static readonly string[] ExpectedBandB = ["b"];

    // --- EdidIdentity ---
    [TestMethod]
    public void EdidIdentity_NumericSerial_BuildsId()
    {
        var edid = BuildEdid("DEL", 0x41A3, 12345u, serialString: null);
        Assert.AreEqual("DEL-41A3-12345", EdidIdentity.TryCreate(edid));
    }

    [TestMethod]
    public void EdidIdentity_PrefersSerialStringDescriptor()
    {
        var edid = BuildEdid("GSM", 0x5B7F, 12345u, serialString: "ABC123");
        Assert.AreEqual("GSM-5B7F-ABC123", EdidIdentity.TryCreate(edid));
    }

    [TestMethod]
    public void EdidIdentity_NoUsableSerial_ReturnsNull()
    {
        Assert.IsNull(EdidIdentity.TryCreate(BuildEdid("DEL", 0x41A3, 0u, serialString: null)));
        Assert.IsNull(EdidIdentity.TryCreate(BuildEdid("DEL", 0x41A3, 0x01010101u, serialString: null)));
        Assert.IsNull(EdidIdentity.TryCreate(BuildEdid("DEL", 0x41A3, uint.MaxValue, serialString: "0000")));
    }

    [TestMethod]
    public void EdidIdentity_InvalidBlock_ReturnsNull()
    {
        var edid = BuildEdid("DEL", 0x41A3, 12345u, serialString: null);
        edid[0] = 0x12;
        Assert.IsNull(EdidIdentity.TryCreate(edid));
        Assert.IsNull(EdidIdentity.TryCreate(new byte[64]));
    }

    // --- Reconciler hardware fallback ---
    [TestMethod]
    public void Reconcile_PortChange_RecoversConfigByHardwareId()
    {
        var config = Customized(@"\\?\DISPLAY#DEL41A3#5&aaa&0&UID1#{g}", "DEL-41A3-1", BandA) with { Enabled = true };
        var monitor = Monitor(@"\\.\DISPLAY3", @"\\?\DISPLAY#DEL41A3#5&bbb&0&UID9#{g}", "DEL-41A3-1", isPrimary: false);

        var result = MonitorConfigReconciler.Reconcile(ImmutableList.Create(config), [Primary(), monitor]);

        var moved = result.Single(c => c.MonitorDeviceId == monitor.StableId);
        Assert.IsTrue(moved.Enabled);
        Assert.AreEqual("a", moved.StartBands!.Single().CommandId);
        Assert.IsFalse(result.Any(c => c.MonitorDeviceId == config.MonitorDeviceId));
    }

    [TestMethod]
    public void Reconcile_DuplicateHardwareIds_DoesNotMatch()
    {
        var config = Customized(@"\\?\DISPLAY#DEL41A3#5&aaa&0&UID1#{g}", "DEL-41A3-1", BandA) with { Enabled = true };
        var twin1 = Monitor(@"\\.\DISPLAY2", @"\\?\DISPLAY#DEL41A3#5&bbb&0&UID8#{g}", "DEL-41A3-1", isPrimary: false);
        var twin2 = Monitor(@"\\.\DISPLAY3", @"\\?\DISPLAY#DEL41A3#5&ccc&0&UID9#{g}", "DEL-41A3-1", isPrimary: false);

        var result = MonitorConfigReconciler.Reconcile(ImmutableList.Create(config), [Primary(), twin1, twin2]);

        Assert.IsFalse(result.Single(c => c.MonitorDeviceId == twin1.StableId).Enabled);
        Assert.IsFalse(result.Single(c => c.MonitorDeviceId == twin2.StableId).Enabled);
        Assert.IsTrue(result.Any(c => c.MonitorDeviceId == config.MonitorDeviceId));
    }

    [TestMethod]
    public void Reconcile_ExactMatch_BackfillsHardwareId()
    {
        var primary = Primary();
        var config = new DockMonitorConfig { MonitorDeviceId = primary.StableId, IsPrimary = true };

        var result = MonitorConfigReconciler.Reconcile(ImmutableList.Create(config), [primary]);

        Assert.AreEqual(primary.HardwareId, result.Single().MonitorHardwareId);
    }

    // --- Orphan GDI configs from the old pin dialog ---
    [TestMethod]
    public void Reconcile_GdiOrphan_MergesIntoOwningMonitor()
    {
        var secondary = Monitor(@"\\.\DISPLAY2", @"\\?\DISPLAY#SEC5678#4&bbb&0&UID222#{g}", "SEC-5678-2", isPrimary: false);
        var existing = Customized(secondary.StableId, secondary.HardwareId, BandA) with { Enabled = true };
        var orphan = Customized(secondary.DeviceId, null, BandA, BandB);

        var result = MonitorConfigReconciler.Reconcile(ImmutableList.Create(existing, orphan), [Primary(), secondary]);

        Assert.IsFalse(result.Any(c => c.MonitorDeviceId == orphan.MonitorDeviceId));
        var merged = result.Single(c => c.MonitorDeviceId == secondary.StableId);
        CollectionAssert.AreEqual(ExpectedMergedCommands, merged.StartBands!.Select(b => b.CommandId).ToArray());
    }

    [TestMethod]
    public void Reconcile_GdiOrphan_DoesNotDuplicateBandAcrossSections()
    {
        var secondary = Monitor(@"\\.\DISPLAY2", @"\\?\DISPLAY#SEC5678#4&bbb&0&UID222#{g}", "SEC-5678-2", isPrimary: false);
        var existing = Customized(secondary.StableId, secondary.HardwareId, BandA) with { Enabled = true };
        var orphan = Customized(secondary.DeviceId, null) with
        {
            CenterBands = ImmutableList.Create(BandA),
            EndBands = ImmutableList.Create(BandB),
        };

        var result = MonitorConfigReconciler.Reconcile(ImmutableList.Create(existing, orphan), [Primary(), secondary]);

        var merged = result.Single(c => c.MonitorDeviceId == secondary.StableId);
        CollectionAssert.AreEqual(ExpectedBandA, merged.StartBands!.Select(b => b.CommandId).ToArray());
        Assert.AreEqual(0, merged.CenterBands!.Count);
        CollectionAssert.AreEqual(ExpectedBandB, merged.EndBands!.Select(b => b.CommandId).ToArray());
    }

    [TestMethod]
    public void Reconcile_GdiOrphan_ReplacesInheritedBands()
    {
        var primary = Primary();
        var existing = new DockMonitorConfig { MonitorDeviceId = primary.StableId, IsPrimary = true };
        var orphan = Customized(primary.DeviceId, null, BandB);

        var result = MonitorConfigReconciler.Reconcile(ImmutableList.Create(existing, orphan), [primary]);

        var merged = result.Single();
        Assert.IsTrue(merged.IsCustomized);
        Assert.AreEqual("b", merged.StartBands!.Single().CommandId);
    }

    private static MonitorInfo Primary() =>
        Monitor(@"\\.\DISPLAY1", @"\\?\DISPLAY#PRI1234#4&aaa&0&UID111#{g}", "PRI-1234-1", isPrimary: true);

    private static MonitorInfo Monitor(string deviceId, string stableId, string? hardwareId, bool isPrimary) => new()
    {
        DeviceId = deviceId,
        StableId = stableId,
        HardwareId = hardwareId,
        DisplayName = deviceId,
        Bounds = new ScreenRect(0, 0, 1920, 1080),
        WorkArea = new ScreenRect(0, 0, 1920, 1040),
        Dpi = 96,
        IsPrimary = isPrimary,
    };

    private static DockMonitorConfig Customized(string deviceId, string? hardwareId, params DockBandSettings[] startBands) => new()
    {
        MonitorDeviceId = deviceId,
        MonitorHardwareId = hardwareId,
        IsCustomized = true,
        StartBands = ImmutableList.Create(startBands),
        CenterBands = ImmutableList<DockBandSettings>.Empty,
        EndBands = ImmutableList<DockBandSettings>.Empty,
    };

    private static byte[] BuildEdid(string manufacturer, ushort productCode, uint serial, string? serialString)
    {
        var edid = new byte[128];
        byte[] header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
        header.CopyTo(edid, 0);

        var packed = ((manufacturer[0] - 'A' + 1) << 10) | ((manufacturer[1] - 'A' + 1) << 5) | (manufacturer[2] - 'A' + 1);
        edid[8] = (byte)(packed >> 8);
        edid[9] = (byte)packed;
        BitConverter.GetBytes(productCode).CopyTo(edid, 10);
        BitConverter.GetBytes(serial).CopyTo(edid, 12);

        if (serialString is not null)
        {
            edid[54 + 3] = 0xFF;
            Encoding.ASCII.GetBytes(serialString + "\n").CopyTo(edid, 54 + 5);
        }

        return edid;
    }
}
