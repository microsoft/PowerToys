// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class OverviewPageTests
{
    private string _settingsPath;

    [TestInitialize]
    public void CreateSettingsPath() =>
        _settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-perfmon-{Guid.NewGuid():N}.json");

    [TestCleanup]
    public void DeleteSettings()
    {
        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }
    }

    [TestMethod]
    public void MainListStartsWithTheOverview()
    {
        using var page = new PerformanceWidgetsPage(new SettingsManager(_settingsPath), isBandPage: false);

        var items = page.GetItems();

        Assert.IsInstanceOfType<SystemOverviewWidgetPage>(items[0].Command);
        Assert.IsTrue(items.Length > 5);
    }

    [TestMethod]
    public void OverviewBandIsOneButtonThatOpensTheOverview()
    {
        using var page = new PerformanceWidgetsPage(new SettingsManager(_settingsPath), isBandPage: true, PerformanceMetricKind.Overview);

        var items = page.GetItems();

        Assert.AreEqual("com.microsoft.cmdpal.performanceWidget.overview", page.Id);
        Assert.AreEqual(1, items.Length);
        Assert.IsInstanceOfType<SystemOverviewWidgetPage>(items[0].Command);
    }

    [TestMethod]
    public void MetricBandsDoNotIncludeTheOverview()
    {
        using var page = new PerformanceWidgetsPage(new SettingsManager(_settingsPath), isBandPage: true, PerformanceMetricKind.Cpu);

        foreach (var item in page.GetItems())
        {
            Assert.IsNotInstanceOfType<SystemOverviewWidgetPage>(item.Command);
        }
    }
}
