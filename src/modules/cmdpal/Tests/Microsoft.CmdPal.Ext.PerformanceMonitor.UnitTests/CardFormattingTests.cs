// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json.Nodes;
using CoreWidgetProvider.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Win32.Storage.FileSystem;

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
    public void DriveLettersAreReadFromTheInstanceName()
    {
        Assert.AreEqual("C: Z:", string.Join(' ', DiskVolumes.GetDriveLetters("0 C: z:")));
        Assert.AreEqual(0, DiskVolumes.GetDriveLetters("1").Count);
        Assert.AreEqual(0, DiskVolumes.GetDriveLetters("_Total").Count);
    }

    [TestMethod]
    public void DisplayNameIncludesTheDiskNumberAndVolumes()
    {
        var withVolumes = string.Format(CultureInfo.InvariantCulture, Resources.GetResource("DiskUsage_Widget_Template/Disk_Number_Volumes"), 0, "C: D:");
        var withoutVolumes = string.Format(CultureInfo.InvariantCulture, Resources.GetResource("DiskUsage_Widget_Template/Disk_Number"), 2);

        Assert.AreEqual(withVolumes, DiskVolumes.GetDisplayName("0 C: D:"));
        Assert.AreEqual(withoutVolumes, DiskVolumes.GetDisplayName("2"));
        Assert.AreEqual("_Total", DiskVolumes.GetDisplayName("_Total"));
    }

    [TestMethod]
    public void CapacityColorWarnsAsTheVolumeFills()
    {
        Assert.AreEqual("accent", DiskVolumes.GetCapacityColor(50));
        Assert.AreEqual("warning", DiskVolumes.GetCapacityColor(85));
        Assert.AreEqual("attention", DiskVolumes.GetCapacityColor(95));
    }

    [TestMethod]
    public void DiskNumberIsReadFromTheInstanceName()
    {
        Assert.IsTrue(DiskVolumes.TryGetDiskNumber("2 D:", out var number));
        Assert.AreEqual(2, number);
        Assert.IsFalse(DiskVolumes.TryGetDiskNumber("_Total", out _));
    }

    [TestMethod]
    public void DiskTypeNamesTheKindAndTheBus()
    {
        var withBus = string.Format(CultureInfo.InvariantCulture, Resources.GetResource("DiskUsage_Widget_Template/Type_With_Bus"), Resources.GetResource("DiskUsage_Widget_Template/Type_SSD"), "NVMe");

        Assert.AreEqual(withBus, DiskVolumes.GetTypeText(new(true, STORAGE_BUS_TYPE.BusTypeNvme)));
        Assert.AreEqual(Resources.GetResource("DiskUsage_Widget_Template/Type_HDD"), DiskVolumes.GetTypeText(new(false, STORAGE_BUS_TYPE.BusTypeUnknown)));
        Assert.AreEqual("USB", DiskVolumes.GetTypeText(new(null, STORAGE_BUS_TYPE.BusTypeUsb)));
        Assert.IsNull(DiskVolumes.GetTypeText(new(null, STORAGE_BUS_TYPE.BusTypeUnknown)));
    }

    [TestMethod]
    public void ResponseTimeIsInMilliseconds()
    {
        Assert.AreEqual("0.4 ms", SystemDiskUsageWidgetPage.ResponseTimeToString(0.0004f));
        Assert.AreEqual("12 ms", SystemDiskUsageWidgetPage.ResponseTimeToString(0.012f));
        Assert.AreEqual("0 ms", SystemDiskUsageWidgetPage.ResponseTimeToString(-1f));
    }

    [TestMethod]
    public void BytesUseBinaryUnits()
    {
        Assert.AreEqual("512 B", DiskVolumes.FormatBytes(512));
        Assert.AreEqual("1.5 KB", DiskVolumes.FormatBytes(1536));
        Assert.AreEqual("953 GB", DiskVolumes.FormatBytes(953L * 1024 * 1024 * 1024));
        Assert.AreEqual("1.8 TB", DiskVolumes.FormatBytes(2_000_000_000_000));
    }

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
    public void LinkSpeedUsesTheLargestWholeUnit()
    {
        Assert.AreEqual("1 Gbps", SystemNetworkUsageWidgetPage.LinkSpeedToString(1e9));
        Assert.AreEqual("2.5 Gbps", SystemNetworkUsageWidgetPage.LinkSpeedToString(2.5e9));
        Assert.AreEqual("866.7 Mbps", SystemNetworkUsageWidgetPage.LinkSpeedToString(866.7e6));
        Assert.AreEqual("56 Kbps", SystemNetworkUsageWidgetPage.LinkSpeedToString(56e3));
        Assert.AreEqual("—", SystemNetworkUsageWidgetPage.LinkSpeedToString(0));
    }

    [TestMethod]
    public void ChargeColorReflectsTheChargeLevel()
    {
        Assert.AreEqual("good", SystemBatteryUsageWidgetPage.GetChargeColor(isCharging: false, 0.8f));
        Assert.AreEqual("warning", SystemBatteryUsageWidgetPage.GetChargeColor(isCharging: false, 0.4f));
        Assert.AreEqual("attention", SystemBatteryUsageWidgetPage.GetChargeColor(isCharging: false, 0.1f));
        Assert.AreEqual("good", SystemBatteryUsageWidgetPage.GetChargeColor(isCharging: true, 0.1f));
        Assert.AreEqual("accent", SystemBatteryUsageWidgetPage.GetChargeColor(isCharging: false, -1f));
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

    [TestMethod]
    public void SparklineTopRoundsUpWithAFloor()
    {
        static JsonObject Data(params float[] values) => new()
        {
            ["series"] = PerformanceChartData.Create(new PerformanceChartData.Series("CPU", PerformanceChartData.CpuColor, values)),
        };

        Assert.AreEqual(10d, SystemOverviewWidgetPage.GetSparklineMax(Data(0.4f, 2f), "series"));
        Assert.AreEqual(40d, SystemOverviewWidgetPage.GetSparklineMax(Data(12f, 33.5f), "series"));
        Assert.AreEqual(100d, SystemOverviewWidgetPage.GetSparklineMax(Data(100f), "series"));
        Assert.AreEqual(10d, SystemOverviewWidgetPage.GetSparklineMax(null, "series"));
        Assert.AreEqual(10d, SystemOverviewWidgetPage.GetSparklineMax(new JsonObject(), "series"));
    }
}
