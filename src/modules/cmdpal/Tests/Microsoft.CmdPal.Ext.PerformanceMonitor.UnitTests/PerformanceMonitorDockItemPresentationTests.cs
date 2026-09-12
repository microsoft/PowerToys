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
    [DataRow("CPU")]
    [DataRow("Arbeitsspeicher")]
    [DataRow("Network")]
    [DataRow("\u010cas aktivity")]
    [DataRow("Battery")]
    public void ConfigureValueLabel_ReservesPercentageAndLocalizedSubtitleSamples(string subtitle)
    {
        var item = new ListItem { Subtitle = subtitle };

        var configured = PerformanceMonitorDockItemPresentation.ConfigureValueLabel(
            item,
            PerformanceMonitorDockItemPresentation.PercentageTitleWidth);

        Assert.AreSame(item, configured);
        var properties = item.GetProperties();
        Assert.AreEqual("text:100%", properties[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.AreEqual("text:" + subtitle, properties[WellKnownExtensionAttributes.DockSubtitleWidth]);
        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.DockMinLabelWidth));
        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.DockMaxLabelWidth));
        Assert.AreEqual(true, properties[WellKnownExtensionAttributes.DockLabelTabularDigits]);
        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.DockLabelTrailingAlignment));

        item.Title = "100%";
        item.Subtitle = string.Empty;

        Assert.AreEqual("text:100%", properties[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.AreEqual("text:" + subtitle, properties[WellKnownExtensionAttributes.DockSubtitleWidth]);
    }

    [TestMethod]
    public void ConfigureValueLabel_DynamicGpuSubtitleReplacesAnySampleWithAFixedWidth()
    {
        var item = new ListItem { Subtitle = "GPU" }.SetDockLabelReservations(null, DockLabelWidth.Sample("GPU"));

        PerformanceMonitorDockItemPresentation.ConfigureValueLabel(
            item,
            PerformanceMonitorDockItemPresentation.PercentageTitleWidth,
            PerformanceMonitorDockItemPresentation.GpuSubtitleWidth);
        item.Subtitle = "A different graphics adapter";

        var properties = item.GetProperties();
        Assert.AreEqual("text:100%", properties[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.AreEqual("12ch", properties[WellKnownExtensionAttributes.DockSubtitleWidth]);
    }

    [TestMethod]
    public void ConfigureValueLabel_TransferRatesKeepTheirValueReservation()
    {
        var item = PerformanceMonitorDockItemPresentation.ConfigureValueLabel(
            new ListItem { Subtitle = "Download" },
            PerformanceMonitorDockItemPresentation.TransferRateLabelWidth);
        var properties = item.GetProperties();

        Assert.AreEqual("10ch", properties[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.AreEqual("text:Download", properties[WellKnownExtensionAttributes.DockSubtitleWidth]);

        item.Title = "999 Mbps";

        Assert.AreEqual("10ch", properties[WellKnownExtensionAttributes.DockTitleWidth]);
        Assert.AreEqual("text:Download", properties[WellKnownExtensionAttributes.DockSubtitleWidth]);
    }
}
