// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CoreWidgetProvider.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class GpuTemperatureTests
{
    [TestMethod]
    public void DriverTemperaturesAreInTenthsOfADegree()
    {
        Assert.AreEqual(44.4f, GpuTemperature.FromDeciCelsius(444), 1e-4f);
        Assert.AreEqual(0f, GpuTemperature.FromDeciCelsius(0));
    }

    [TestMethod]
    [DataRow(0x0000_0000_0001_7138L)]
    [DataRow(unchecked((long)0x8000_0001_0000_0002UL))]
    public void ToLuidInvertsTheAdapterKey(long luidKey)
    {
        var luid = GpuTemperature.ToLuid(luidKey);

        // The same key GpuAdapterNames builds from DXGI, including a HighPart with its sign bit set.
        Assert.AreEqual(luidKey, ((long)(uint)luid.HighPart << 32) | luid.LowPart);
    }

    [TestMethod]
    public void AnAdapterThatDoesNotExistHasNoTemperature()
    {
        Assert.AreEqual(0f, GpuTemperature.Read(0));
    }
}
