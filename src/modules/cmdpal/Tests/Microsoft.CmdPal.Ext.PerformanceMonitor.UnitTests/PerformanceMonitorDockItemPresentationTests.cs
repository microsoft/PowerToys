// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CoreWidgetProvider.Helpers;
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
    [DataRow(0)]
    [DataRow(1)]
    public void ConfigureGpuValueLabel_SingleGpuUsesTheLocalizedLabelAndItsMeasuredReservation(int adapterCount)
    {
        var item = new ListItem();
        var gpu = new GPUStats.DisplayInfo("NVIDIA RTX A3000 Laptop GPU", "RTX A3000", adapterCount);

        PerformanceMonitorDockItemPresentation.ConfigureGpuValueLabel(item, gpu);

        var subtitle = Resources.GetResource("GPU_Usage_Subtitle");
        Assert.AreEqual(subtitle, item.Subtitle);
        Assert.AreEqual("text:" + subtitle, item.GetProperties()[WellKnownExtensionAttributes.DockSubtitleWidth]);
        Assert.AreEqual("text:100%", item.GetProperties()[WellKnownExtensionAttributes.DockTitleWidth]);
    }

    [TestMethod]
    public void ConfigureGpuValueLabel_CyclingModelsPreservesTheReservation()
    {
        var item = new ListItem();
        PerformanceMonitorDockItemPresentation.ConfigureGpuValueLabel(item, new("NVIDIA RTX A3000 Laptop GPU", "RTX A3000", 2));
        var widthNotifications = 0;
        item.PropChanged += (_, args) => widthNotifications += args.PropertyName == WellKnownExtensionAttributes.DockLabelWidthPropertyName ? 1 : 0;

        Assert.AreEqual("RTX A3000", item.Subtitle);
        PerformanceMonitorDockItemPresentation.ConfigureGpuValueLabel(item, new("Intel(R) Iris(R) Xe Graphics", "Iris Xe", 2));
        item.Title = "99%";
        PerformanceMonitorDockItemPresentation.ConfigureGpuValueLabel(item, new("Intel(R) Iris(R) Xe Graphics", "Iris Xe", 2));

        Assert.AreEqual("Iris Xe", item.Subtitle);
        Assert.AreEqual("12ch", item.GetProperties()[WellKnownExtensionAttributes.DockSubtitleWidth]);
        Assert.AreEqual(0, widthNotifications);
    }

    [TestMethod]
    public void ConfigureGpuValueLabel_ReservationChangesOnlyWhenThePresentationModeChanges()
    {
        var item = new ListItem();
        var gpu = new GPUStats.DisplayInfo("NVIDIA RTX A3000 Laptop GPU", "RTX A3000", 1);
        PerformanceMonitorDockItemPresentation.ConfigureGpuValueLabel(item, gpu);
        var widthNotifications = 0;
        item.PropChanged += (_, args) => widthNotifications += args.PropertyName == WellKnownExtensionAttributes.DockLabelWidthPropertyName ? 1 : 0;

        PerformanceMonitorDockItemPresentation.ConfigureGpuValueLabel(item, gpu);
        Assert.AreEqual(0, widthNotifications);
        PerformanceMonitorDockItemPresentation.ConfigureGpuValueLabel(item, gpu with { AdapterCount = 2 });
        Assert.AreEqual(1, widthNotifications);
        Assert.AreEqual("12ch", item.GetProperties()[WellKnownExtensionAttributes.DockSubtitleWidth]);
        PerformanceMonitorDockItemPresentation.ConfigureGpuValueLabel(item, gpu);
        Assert.AreEqual(2, widthNotifications);
        Assert.AreEqual("text:" + Resources.GetResource("GPU_Usage_Subtitle"), item.GetProperties()[WellKnownExtensionAttributes.DockSubtitleWidth]);
    }

    [TestMethod]
    public void ConfigureGpuValueLabel_UnavailableModelUsesTheGenericLabel()
    {
        var item = new ListItem();

        PerformanceMonitorDockItemPresentation.ConfigureGpuValueLabel(item, null);
        Assert.AreEqual(Resources.GetResource("GPU_Usage_Subtitle"), item.Subtitle);
        PerformanceMonitorDockItemPresentation.ConfigureGpuValueLabel(item, new("NVIDIA RTX A3000 Laptop GPU", "RTX A3000", 2));
        var widthNotifications = 0;
        item.PropChanged += (_, args) => widthNotifications += args.PropertyName == WellKnownExtensionAttributes.DockLabelWidthPropertyName ? 1 : 0;

        PerformanceMonitorDockItemPresentation.ConfigureGpuValueLabel(item, new(string.Empty, string.Empty, 2));

        Assert.AreEqual(Resources.GetResource("GPU_Usage_Subtitle"), item.Subtitle);
        Assert.AreEqual("12ch", item.GetProperties()[WellKnownExtensionAttributes.DockSubtitleWidth]);
        Assert.AreEqual(0, widthNotifications);
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
