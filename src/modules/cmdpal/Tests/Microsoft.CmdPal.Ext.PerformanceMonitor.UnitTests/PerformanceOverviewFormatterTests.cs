// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class PerformanceOverviewFormatterTests
{
    [DataTestMethod]
    [DataRow(null, 0)]
    [DataRow("", 0)]
    [DataRow("   ", 0)]
    [DataRow("0%", 0)]
    [DataRow("42%", 42)]
    [DataRow("100%", 100)]
    [DataRow(" 42% ", 42)]
    [DataRow("42 %", 42)]
    [DataRow("150%", 100)]
    [DataRow("-5%", 0)]
    [DataRow("--", 0)]
    [DataRow("???", 0)]
    [DataRow("not a percent", 0)]
    public void ParsePercentText_ParsesAndClampsExpectedValues(string percentText, int expected)
    {
        Assert.AreEqual(expected, PerformanceOverviewFormatter.ParsePercentText(percentText));
    }

    [DataTestMethod]
    [DataRow(-100, 0)]
    [DataRow(-1, 0)]
    [DataRow(0, 0)]
    [DataRow(42, 42)]
    [DataRow(100, 100)]
    [DataRow(101, 100)]
    [DataRow(1000, 100)]
    public void ClampPercent_ClampsToZeroToHundredRange(int percent, int expected)
    {
        Assert.AreEqual(expected, PerformanceOverviewFormatter.ClampPercent(percent));
    }

    [DataTestMethod]
    [DataRow((int)PerformanceMetricKind.Cpu, "cpu")]
    [DataRow((int)PerformanceMetricKind.Memory, "memory")]
    [DataRow((int)PerformanceMetricKind.Network, "network")]
    [DataRow((int)PerformanceMetricKind.Disk, "disk")]
    [DataRow((int)PerformanceMetricKind.Gpu, "gpu")]
    public void GetMetricKey_ReturnsStableKey(int metricValue, string expectedKey)
    {
        Assert.AreEqual(expectedKey, PerformanceOverviewFormatter.GetMetricKey((PerformanceMetricKind)metricValue));
    }

    [TestMethod]
    public void GetMetricKey_RejectsMetricsNotShownInOverview()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            PerformanceOverviewFormatter.GetMetricKey(PerformanceMetricKind.Battery));
    }

    [DataTestMethod]
    [DataRow((int)PerformanceMetricKind.Cpu, "cpu")]
    [DataRow((int)PerformanceMetricKind.Memory, "memory")]
    [DataRow((int)PerformanceMetricKind.Network, "network")]
    [DataRow((int)PerformanceMetricKind.Disk, "disk")]
    [DataRow((int)PerformanceMetricKind.Gpu, "gpu")]
    public void SelectMetricValue_ReturnsValueForSelectedMetric(int metricValue, string expected)
    {
        var actual = PerformanceOverviewFormatter.SelectMetricValue(
            (PerformanceMetricKind)metricValue,
            "cpu",
            "memory",
            "network",
            "disk",
            "gpu");

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SelectMetricValue_RejectsMetricsNotShownInOverview()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            PerformanceOverviewFormatter.SelectMetricValue(
                PerformanceMetricKind.Battery,
                "cpu",
                "memory",
                "network",
                "disk",
                "gpu"));
    }

    [TestMethod]
    public void RollingThroughputNormalizer_AddsHeadroomAbovePeak()
    {
        var normalizer = new RollingThroughputNormalizer();
        var timestamp = DateTimeOffset.UtcNow;

        Assert.AreEqual(83, normalizer.AddSample(10_000_000, timestamp));
    }

    [TestMethod]
    public void RollingThroughputNormalizer_UsesMinimumScaleForIdleTraffic()
    {
        var normalizer = new RollingThroughputNormalizer();
        var timestamp = DateTimeOffset.UtcNow;

        Assert.AreEqual(10, normalizer.AddSample(12_500, timestamp));
    }

    [TestMethod]
    public void RollingThroughputNormalizer_RetainsPeakForSixtySeconds()
    {
        var normalizer = new RollingThroughputNormalizer();
        var timestamp = DateTimeOffset.UtcNow;

        normalizer.AddSample(10_000_000, timestamp);

        Assert.AreEqual(8, normalizer.AddSample(1_000_000, timestamp.AddSeconds(59)));
        Assert.AreEqual(83, normalizer.AddSample(1_000_000, timestamp.AddSeconds(61)));
    }

    [TestMethod]
    public void RollingThroughputNormalizer_ClampsInvalidSamplesToZero()
    {
        var normalizer = new RollingThroughputNormalizer();

        Assert.AreEqual(0, normalizer.AddSample(double.NaN, DateTimeOffset.UtcNow));
        Assert.AreEqual(0, normalizer.AddSample(-1, DateTimeOffset.UtcNow));
    }

    [TestMethod]
    public void RollingThroughputNormalizer_UsesSharedScaleForPair()
    {
        var normalizer = new RollingThroughputNormalizer();
        var percentages = normalizer.AddPairSample(10_000_000, 5_000_000, DateTimeOffset.UtcNow);

        Assert.AreEqual(83, percentages.FirstPercent);
        Assert.AreEqual(42, percentages.SecondPercent);
    }
}
