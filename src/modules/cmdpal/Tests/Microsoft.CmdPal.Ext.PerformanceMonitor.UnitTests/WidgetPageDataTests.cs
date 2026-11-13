// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Nodes;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
public partial class WidgetPageDataTests
{
    private sealed partial class TestWidgetPage : WidgetPage
    {
        public Dictionary<string, JsonNode> Data => ContentData;

        public JsonObject Snapshot => ContentDataJson;

        protected override void LoadContentData()
        {
        }

        protected override string GetTemplatePath(WidgetPageState page) => string.Empty;
    }

    [TestMethod]
    public void SnapshotPreservesStringsAndStructuredData()
    {
        var page = new TestWidgetPage();
        page.Data["cpuUsage"] = "25%";
        page.Data["cpuSeries"] = new JsonArray(new JsonObject { ["value"] = 25.0 });
        page.Data["cpuCores"] = new JsonArray(new JsonObject { ["label"] = "CPU 0", ["value"] = 50 });
        page.Data["cpuTopProcesses"] = new JsonArray(new JsonObject { ["name"] = "Example", ["usage"] = "10%" });

        var snapshot = JsonNode.Parse(page.Snapshot.ToJsonString());

        Assert.AreEqual("25%", snapshot["cpuUsage"].GetValue<string>());
        Assert.AreEqual(25.0, snapshot["cpuSeries"][0]["value"].GetValue<double>());
        Assert.AreEqual("CPU 0", snapshot["cpuCores"][0]["label"].GetValue<string>());
        Assert.AreEqual(50, snapshot["cpuCores"][0]["value"].GetValue<int>());
        Assert.AreEqual("Example", snapshot["cpuTopProcesses"][0]["name"].GetValue<string>());
        Assert.AreEqual("10%", snapshot["cpuTopProcesses"][0]["usage"].GetValue<string>());
    }

    [TestMethod]
    public void SnapshotsDoNotReparentOrShareSourceNodes()
    {
        var page = new TestWidgetPage();
        var series = new JsonArray(new JsonObject { ["value"] = 25 });
        page.Data["cpuUsage"] = "25%";
        page.Data["cpuSeries"] = series;

        var first = page.Snapshot;
        var second = page.Snapshot;
        first["cpuSeries"][0]["value"] = 50;
        first["cpuUsage"] = "50%";

        Assert.IsNull(series.Parent);
        Assert.IsNull(page.Data["cpuUsage"].Parent);
        Assert.AreEqual(25, series[0]["value"].GetValue<int>());
        Assert.AreEqual(25, second["cpuSeries"][0]["value"].GetValue<int>());
        Assert.AreEqual("25%", second["cpuUsage"].GetValue<string>());
        Assert.AreEqual("25%", page.Data["cpuUsage"].GetValue<string>());
    }

    [TestMethod]
    public void ClearingDataRemovesBothStringsAndStructuredValues()
    {
        var page = new TestWidgetPage();
        page.Data["cpuUsage"] = "25%";
        page.Data["cpuSeries"] = new JsonArray(25);
        page.Data["cpuCores"] = new JsonArray(50);
        page.Data["cpuTopProcesses"] = new JsonArray("Example");

        page.Data.Clear();
        page.Data["errorMessage"] = "Could not retrieve stats.";

        var snapshot = page.Snapshot;

        Assert.AreEqual(1, snapshot.Count);
        Assert.AreEqual("Could not retrieve stats.", snapshot["errorMessage"].GetValue<string>());
    }
}
