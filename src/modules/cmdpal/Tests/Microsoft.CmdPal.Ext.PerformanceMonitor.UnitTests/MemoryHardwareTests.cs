// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class MemoryHardwareTests
{
    [TestMethod]
    public void CountsSlotsAndReadsTheConfiguredSpeed()
    {
        var table = Table(
            MemoryDevice(sizeMegabytes: 16384, formFactor: 0x0D, ratedSpeed: 5600, configuredSpeed: 5200),
            MemoryDevice(sizeMegabytes: 0, formFactor: 0x0D, ratedSpeed: 0, configuredSpeed: 0),
            MemoryDevice(sizeMegabytes: 16384, formFactor: 0x0D, ratedSpeed: 5600, configuredSpeed: 5200),
            EndOfTable());

        var modules = MemoryHardware.Parse(table);

        Assert.IsNotNull(modules);
        Assert.AreEqual(2, modules.Value.SlotsUsed);
        Assert.AreEqual(3, modules.Value.SlotCount);
        Assert.AreEqual(5200u, modules.Value.SpeedMegatransfers);
        Assert.AreEqual((byte)0x0D, modules.Value.FormFactor);
    }

    [TestMethod]
    public void FallsBackToTheRatedSpeed()
    {
        var modules = MemoryHardware.Parse(Table(MemoryDevice(8192, 0x09, ratedSpeed: 3200, configuredSpeed: 0), EndOfTable()));

        Assert.AreEqual(3200u, modules.Value.SpeedMegatransfers);
    }

    [TestMethod]
    public void SkipsOtherStructuresAndTheirStrings()
    {
        var other = new List<byte> { 0, 4, 0, 0 };
        other.AddRange("Contoso\0BIOS 1.0\0\0"u8.ToArray());

        var modules = MemoryHardware.Parse(Table(other.ToArray(), MemoryDevice(8192, 0x0B, 6400, 6400), EndOfTable()));

        Assert.AreEqual(1, modules.Value.SlotCount);
        Assert.AreEqual((byte)0x0B, modules.Value.FormFactor);
    }

    [TestMethod]
    public void TablesWithoutMemoryDevicesHaveNoModules()
    {
        Assert.IsNull(MemoryHardware.Parse(Table(EndOfTable())));
        Assert.IsNull(MemoryHardware.Parse([]));
        Assert.IsNull(MemoryHardware.Parse([17, 200, 0, 0]));
    }

    [TestMethod]
    public void CommonFormFactorsHaveNames()
    {
        Assert.AreEqual("SODIMM", MemoryHardware.GetFormFactorName(0x0D));
        Assert.AreEqual("DIMM", MemoryHardware.GetFormFactorName(0x09));
        Assert.IsNull(MemoryHardware.GetFormFactorName(0x02));
    }

    private static byte[] Table(params byte[][] structures)
    {
        var table = new List<byte>();
        foreach (var structure in structures)
        {
            table.AddRange(structure);
        }

        return table.ToArray();
    }

    // An SMBIOS 2.7 memory device (type 17, 0x28 bytes) with no strings.
    private static byte[] MemoryDevice(ushort sizeMegabytes, byte formFactor, ushort ratedSpeed, ushort configuredSpeed)
    {
        var device = new byte[0x28 + 2];
        device[0] = 17;
        device[1] = 0x28;
        BitConverter.TryWriteBytes(device.AsSpan(0x0C), sizeMegabytes);
        device[0x0E] = formFactor;
        BitConverter.TryWriteBytes(device.AsSpan(0x15), ratedSpeed);
        BitConverter.TryWriteBytes(device.AsSpan(0x20), configuredSpeed);
        return device;
    }

    private static byte[] EndOfTable() => [127, 4, 0, 0, 0, 0];
}
