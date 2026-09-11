// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class PerformanceMonitorDockItemPresentationTests
{
    [TestMethod]
    [DataRow(PerformanceMonitorDockItemPresentation.CpuSubtitleWidth)]
    [DataRow(PerformanceMonitorDockItemPresentation.MemorySubtitleWidth)]
    [DataRow(PerformanceMonitorDockItemPresentation.NetworkUsageSubtitleWidth)]
    [DataRow(PerformanceMonitorDockItemPresentation.DiskActiveTimeSubtitleWidth)]
    [DataRow(PerformanceMonitorDockItemPresentation.GpuSubtitleWidth)]
    [DataRow(PerformanceMonitorDockItemPresentation.BatterySubtitleWidth)]
    public void ConfigureValueLabel_ReservesPercentageAndSubtitleWidthsIndependently(string subtitleWidth)
    {
        var item = new ListItem();

        var configured = PerformanceMonitorDockItemPresentation.ConfigureValueLabel(
            item,
            PerformanceMonitorDockItemPresentation.PercentageTitleWidth,
            subtitleWidth);

        Assert.AreSame(item, configured);
        var properties = item.GetProperties();
        Assert.AreEqual("4.6ch", properties[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.AreEqual(subtitleWidth, properties[WellKnownExtensionAttributes.DockSubtitleWidth]);
        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.DockMinLabelWidth));
        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.DockMaxLabelWidth));
        Assert.AreEqual(true, properties[WellKnownExtensionAttributes.DockLabelTabularDigits]);
        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.DockLabelTrailingAlignment));
    }

    [TestMethod]
    public void ConfigureValueLabel_TransferRatesKeepTheirValueWidthInBothModes()
    {
        var item = PerformanceMonitorDockItemPresentation.ConfigureValueLabel(
            new ListItem(),
            PerformanceMonitorDockItemPresentation.TransferRateLabelWidth);
        var properties = item.GetProperties();

        Assert.AreEqual("10ch", properties[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.AreEqual("10ch", properties[WellKnownExtensionAttributes.DockSubtitleWidth]);
        Assert.AreEqual(true, properties[WellKnownExtensionAttributes.DockLabelTabularDigits]);
    }
}
