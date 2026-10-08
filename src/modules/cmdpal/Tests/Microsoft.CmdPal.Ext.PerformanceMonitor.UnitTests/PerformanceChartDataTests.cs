// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class PerformanceChartDataTests
{
    [TestMethod]
    public void ShortHistoryIsPaddedAtTheStart()
    {
        var data = PerformanceChartData.Create(new PerformanceChartData.Series("CPU", "categoricalBlue", [10f, 20.25f]));

        var series = (JsonObject)data[0];
        var values = (JsonArray)series["values"];
        Assert.AreEqual("CPU", (string)series["legend"]);
        Assert.AreEqual("categoricalBlue", (string)series["color"]);
        Assert.AreEqual(PerformanceChartData.HistoryLength, values.Count);
        Assert.IsNull(values[0]["y"]);
        Assert.AreEqual(10d, (double)values[^2]["y"]);
        Assert.AreEqual(20.3d, (double)values[^1]["y"], 1e-9);
    }

    [TestMethod]
    public void LongHistoryKeepsTheNewestSamples()
    {
        var samples = Enumerable.Range(0, PerformanceChartData.HistoryLength + 5).Select(i => (float)i).ToArray();

        var data = PerformanceChartData.Create(new PerformanceChartData.Series("CPU", "good", samples));

        var values = (JsonArray)data[0]["values"];
        Assert.AreEqual(PerformanceChartData.HistoryLength, values.Count);
        Assert.AreEqual(5d, (double)values[0]["y"]);
        Assert.AreEqual(PerformanceChartData.HistoryLength + 4d, (double)values[^1]["y"]);
    }

    [TestMethod]
    public void EachSeriesIsWritten()
    {
        var data = PerformanceChartData.Create(
            new PerformanceChartData.Series("Send", "categoricalMarigold", [1f]),
            new PerformanceChartData.Series("Receive", "categoricalTeal", [2f]));

        Assert.AreEqual(2, data.Count);
        Assert.AreEqual("Receive", (string)data[1]["legend"]);
    }

    [TestMethod]
    public void UptimeUsesDaysHoursMinutesSeconds()
    {
        var uptime = new TimeSpan(3, 4, 5, 6);

        Assert.AreEqual("3:04:05:06", SystemCPUUsageWidgetPage.UptimeToString(uptime));
    }
}
