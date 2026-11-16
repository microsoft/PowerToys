// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CoreWidgetProvider.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class GpuTemperatureTests
{
    [TestMethod]
    [DataRow(526u, 52.6f)]
    [DataRow(1000u, 100f)]
    [DataRow(1u, 0.1f)]
    [DataRow(2000u, 200f)]
    public void TemperatureUsesTenthsOfCelsius(uint raw, float expected)
    {
        Assert.AreEqual(expected, GpuTemperature.ToCelsius(raw));
    }

    [TestMethod]
    [DataRow(0u)]
    [DataRow(2001u)]
    [DataRow(uint.MaxValue)]
    public void UnsupportedOrInvalidSensorValuesAreUnavailable(uint raw)
    {
        Assert.IsNull(GpuTemperature.ToCelsius(raw));
    }

    [TestMethod]
    public void NativeLayoutsMatchTheWindowsSdk()
    {
        Assert.AreEqual(12, Marshal.SizeOf<GpuTemperature.OpenAdapterFromLuid>());
        Assert.AreEqual(8, Marshal.OffsetOf<GpuTemperature.OpenAdapterFromLuid>("AdapterHandle").ToInt32());
        Assert.AreEqual(IntPtr.Size == 8 ? 24 : 16, Marshal.SizeOf<GpuTemperature.QueryAdapterInfo>());
        Assert.AreEqual(8, Marshal.OffsetOf<GpuTemperature.QueryAdapterInfo>("PrivateDriverData").ToInt32());
        Assert.AreEqual(8 + IntPtr.Size, Marshal.OffsetOf<GpuTemperature.QueryAdapterInfo>("PrivateDriverDataSize").ToInt32());
        Assert.AreEqual(64, Marshal.SizeOf<GpuTemperature.AdapterPerfData>());
        Assert.AreEqual(56, Marshal.OffsetOf<GpuTemperature.AdapterPerfData>("Temperature").ToInt32());
        Assert.AreEqual(4, Marshal.SizeOf<GpuTemperature.CloseAdapter>());
    }

    [TestMethod]
    public void TemperatureIsMatchedByLuidAndRecoversAfterAnUnavailableSample()
    {
        const long integratedLuid = 0x1234567800012345;
        const long discreteLuid = -123456789;
        var readings = new Dictionary<long, float?> { [integratedLuid] = null, [discreteLuid] = 53f };
        using var stats = new GPUStats(
            new()
            {
                [integratedLuid] = new("Integrated GPU", false),
                [discreteLuid] = new("Discrete GPU", false),
            },
            [integratedLuid, discreteLuid],
            luid => readings[luid]);

        Assert.AreEqual("--", stats.GetGPUTemperature(1));
        stats.GetData();
        Assert.AreEqual("--", stats.GetGPUTemperature(0));
        Assert.AreEqual("53 \u00B0C", stats.GetGPUTemperature(1));

        readings[discreteLuid] = null;
        stats.GetData();
        Assert.AreEqual("--", stats.GetGPUTemperature(1));

        readings[discreteLuid] = 54f;
        stats.GetData();
        Assert.AreEqual("54 \u00B0C", stats.GetGPUTemperature(1));
        Assert.AreEqual("--", stats.GetGPUTemperature(-1));
        Assert.AreEqual("--", stats.GetGPUTemperature(2));
    }
}
