// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class CardFormattingTests
{
    private CultureInfo _originalCulture;

    [TestInitialize]
    public void UseInvariantCulture()
    {
        _originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [TestCleanup]
    public void RestoreCulture() => CultureInfo.CurrentCulture = _originalCulture;

    [TestMethod]
    public void RateScaleUsesBitsForTheDefaultUnit()
    {
        var (divisor, unit) = PerformanceChartData.GetRateScale(SpeedUnit.BitsPerSecond, 50 * 1024 * 1024 / 8);

        Assert.AreEqual("Mbps", unit);
        Assert.AreEqual(50f, 50 * 1024 * 1024 / 8 / divisor, 1e-3);
    }

    [TestMethod]
    public void RateScaleUsesDecimalOrBinaryBytes()
    {
        Assert.AreEqual((1000f, "KB/s"), PerformanceChartData.GetRateScale(SpeedUnit.BytesPerSecond, 999_999));
        Assert.AreEqual((1_000_000f, "MB/s"), PerformanceChartData.GetRateScale(SpeedUnit.BytesPerSecond, 1_000_000));
        Assert.AreEqual((1024f * 1024, "MiB/s"), PerformanceChartData.GetRateScale(SpeedUnit.BinaryBytesPerSecond, 3 * 1024 * 1024));
    }

    [TestMethod]
    public void RateScaleStopsAtTheLargestUnit()
    {
        Assert.AreEqual((1e9f, "GB/s"), PerformanceChartData.GetRateScale(SpeedUnit.BytesPerSecond, 5e12f));
        Assert.AreEqual((1000f, "KB/s"), PerformanceChartData.GetRateScale(SpeedUnit.BytesPerSecond, 0));
    }

    [TestMethod]
    public void ScaleAndMaxCoverEverySeries()
    {
        var scaled = PerformanceChartData.Scale([1000f, 2500f], 1000f);
        Assert.AreEqual(2, scaled.Length);
        Assert.AreEqual(1f, scaled[0]);
        Assert.AreEqual(2.5f, scaled[1]);
        Assert.AreEqual(9f, PerformanceChartData.Max([1f, 9f], [4f]));
        Assert.AreEqual(0f, PerformanceChartData.Max());
    }

    [TestMethod]
    public void MemoryUseShowsTheSizeUnit()
    {
        const ulong BytesPerGigabyte = 1024UL * 1024 * 1024;

        Assert.AreEqual("1.5 / 8.0 GB", WidgetPage.FormatUsedOfTotal(BytesPerGigabyte + (BytesPerGigabyte / 2), 8 * BytesPerGigabyte));
        Assert.AreEqual("128 / 512 MB", WidgetPage.FormatUsedOfTotal(128UL * 1024 * 1024, 512UL * 1024 * 1024));
    }

    [TestMethod]
    public void PercentIsClampedAndHandlesAnUnknownSize()
    {
        Assert.AreEqual(25d, WidgetPage.GetPercent(1, 4));
        Assert.AreEqual(100d, WidgetPage.GetPercent(9, 4));
        Assert.AreEqual(0d, WidgetPage.GetPercent(9, 0));
    }
}
