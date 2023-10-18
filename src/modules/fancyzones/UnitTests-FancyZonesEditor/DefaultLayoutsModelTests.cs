// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using FancyZonesEditor.Models;

namespace UnitTestsFancyZonesEditor;

[TestClass]
public class DefaultLayoutsModelTests
{
    [TestMethod]
    public void LayoutCapacityShouldBeNumberOfMonitorConfigurations()
    {
        var defaultLayoutsModel = new DefaultLayoutsModel();
        var expectedOptionCount = Enum.GetValues(typeof(MonitorConfigurationType)).Length;

        var actualCapacity = defaultLayoutsModel.Layouts.Capacity;

        Assert.AreEqual(expectedOptionCount, actualCapacity);
    }

    [TestMethod]
    public void OverridingLayoutClearsOldDefault()
    {
        var defaultLayoutsModel = new DefaultLayoutsModel();
        GridLayoutModel firstLayout = new GridLayoutModel();
        CanvasLayoutModel secondLayout = new CanvasLayoutModel("steve");

        defaultLayoutsModel.Set(firstLayout, MonitorConfigurationType.Horizontal);
        Assert.AreEqual(defaultLayoutsModel.Layouts[(int)MonitorConfigurationType.Horizontal], firstLayout);

        defaultLayoutsModel.Set(secondLayout, MonitorConfigurationType.Horizontal);
        Assert.AreNotEqual(defaultLayoutsModel.Layouts[(int)MonitorConfigurationType.Horizontal], firstLayout);
        Assert.AreEqual(defaultLayoutsModel.Layouts[(int)MonitorConfigurationType.Horizontal], secondLayout);
    }

    [TestMethod]
    public void SettingTheVerticalLayoutShouldBeTheDefault()
    {
        var defaultLayoutsModel = new DefaultLayoutsModel();
        GridLayoutModel firstLayout = new GridLayoutModel();
        defaultLayoutsModel.Set(firstLayout, MonitorConfigurationType.Horizontal);
        defaultLayoutsModel.Set(firstLayout, MonitorConfigurationType.Vertical);

        Assert.AreEqual(defaultLayoutsModel.Layouts[(int)MonitorConfigurationType.Vertical], firstLayout);
    }

    [TestMethod]
    public void RestoringLayoutShouldSetLayouts()
    {
        var defaultLayoutsModel = new DefaultLayoutsModel();
        GridLayoutModel firstLayout = new GridLayoutModel();
        CanvasLayoutModel secondLayout = new CanvasLayoutModel("steve");
        var restoredLayouts = new List<LayoutModel> { firstLayout, secondLayout };
        defaultLayoutsModel.Restore(restoredLayouts);

        CollectionAssert.AreEqual(defaultLayoutsModel.Layouts, restoredLayouts);
    }
}
