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
    public void ConfigureValueLabel_AppliesStableNumericPresentation()
    {
        var item = new ListItem();

        var configured = PerformanceMonitorDockItemPresentation.ConfigureValueLabel(
            item,
            PerformanceMonitorDockItemPresentation.MemoryLabelWidth);

        Assert.AreSame(item, configured);
        var properties = item.GetProperties();
        Assert.AreEqual(PerformanceMonitorDockItemPresentation.MemoryLabelWidth, properties[WellKnownExtensionAttributes.DockMinLabelWidth]);
        Assert.AreEqual(PerformanceMonitorDockItemPresentation.MemoryLabelWidth, properties[WellKnownExtensionAttributes.DockMaxLabelWidth]);
        Assert.AreEqual(true, properties[WellKnownExtensionAttributes.DockLabelTabularDigits]);
        Assert.IsFalse(properties.ContainsKey(WellKnownExtensionAttributes.DockLabelTrailingAlignment));
    }
}
