// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using AdaptiveCards.Templating;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public partial class PerformanceSmallContentTests
{
    [TestMethod]
    public void PropChanged_ActivatesUntilLastSubscriptionIsRemoved()
    {
        var widget = new TrackingWidget();
        var content = new PerformanceSmallContent(widget, new Size(360, 180));
        var updates = 0;
        TypedEventHandler<object, IPropChangedEventArgs> handler = (_, args) =>
        {
            Assert.AreEqual(nameof(IFormContent.DataJson), args.PropertyName);
            updates++;
        };
        content.PropChanged -= handler;
        Assert.AreEqual(0, widget.Activations);

        content.PropChanged += handler;
        content.PropChanged += handler;
        widget.UpdateWidget();
        content.PropChanged -= handler;
        Assert.AreEqual(1, widget.Activations);
        Assert.AreEqual(0, widget.Deactivations);
        Assert.AreEqual(2, updates);

        content.PropChanged -= handler;
        content.PropChanged -= handler;
        widget.UpdateWidget();
        Assert.AreEqual(1, widget.Deactivations);
        Assert.AreEqual(2, updates);
    }

    [TestMethod]
    public void PropChanged_DockAndTrayShareTheWidgetWithoutPrematurelyStoppingIt()
    {
        var widget = new TrackingWidget();
        var first = new PerformanceSmallContent(widget, new Size(360, 180));
        var second = new PerformanceSmallContent(widget, new Size(360, 180));
        TypedEventHandler<object, IPropChangedEventArgs> handler = (_, _) => { };
        widget.PushActivate();
        first.PropChanged += handler;
        second.PropChanged += handler;
        first.PropChanged -= handler;
        second.PropChanged -= handler;
        Assert.AreEqual(1, widget.Activations);
        Assert.AreEqual(0, widget.Deactivations);

        widget.PopActivate();
        Assert.AreEqual(1, widget.Deactivations);
    }

    [TestMethod]
    [DataRow(40, "60")]
    [DataRow(200, "92")]
    [DataRow(336, "160")]
    [DataRow(1000, "160")]
    public void Template_UsesAdvisoryWidthWithinCompactBounds(int width, string expectedWidth)
    {
        var content = new PerformanceSmallContent(new TrackingWidget(), new Size(width, 100));
        using var card = JsonDocument.Parse(content.TemplateJson);
        var columns = card.RootElement.GetProperty("body")[0].GetProperty("columns");
        var chart = columns[1].GetProperty("items")[0];

        Assert.AreEqual("Chart.Line", chart.GetProperty("type").GetString());
        Assert.AreEqual("${widgets[0].hasChart}", chart.GetProperty("$when").GetString());
        Assert.AreEqual("${widgets[0].series}", chart.GetProperty("data").GetString());
        Assert.AreEqual("${widgets[0].value}", columns[0].GetProperty("items")[1].GetProperty("text").GetString());
        Assert.AreEqual(1, columns[0].GetProperty("items")[1].GetProperty("maxLines").GetInt32());
        Assert.AreEqual(expectedWidth, columns[1].GetProperty("width").GetString());
        Assert.AreEqual(CommandResultKind.KeepOpen, content.SubmitForm("{}", "{}").Kind);
    }

    [TestMethod]
    [DataRow((int)PerformanceMetricKind.Cpu, "cpuUsage", "cpuSpeed", "cpuSeries", 30d)]
    [DataRow((int)PerformanceMetricKind.Memory, "memUsage", "memUsedOfTotal", "memSeries", 100d)]
    [DataRow((int)PerformanceMetricKind.Network, "netReceived", "netSent", "netSeries", 25d)]
    [DataRow((int)PerformanceMetricKind.NetworkSpeed, "netReceived", "netSent", "netSeries", 25d)]
    [DataRow((int)PerformanceMetricKind.Disk, "diskUsage", "diskName", "diskActiveSeries", 30d)]
    [DataRow((int)PerformanceMetricKind.Gpu, "gpuUsage", "gpuShortName", "gpuSeries", 30d)]
    public void Data_UsesNativeNumericHistoryAndCurrentMetricValues(int metric, string valueKey, string detailKey, string seriesKey, double expectedMax)
    {
        var series = PerformanceChartData.Create(new PerformanceChartData.Series("Usage", "categoricalBlue", [10f, 25f]));
        var snapshot = new JsonObject { [valueKey] = "25%", [detailKey] = "details", [seriesKey] = series };

        var data = PerformanceSmallContent.CreateData((PerformanceMetricKind)metric, "Metric", snapshot);

        Assert.AreEqual("Metric", (string)data["title"]);
        Assert.AreEqual("25%", (string)data["value"]);
        Assert.IsTrue((bool)data["hasChart"]);
        Assert.AreEqual(expectedMax, (double)data["chartMax"]);
        Assert.AreEqual(25d, (double)data["series"][0]["values"][1]["y"]);
        Assert.AreEqual("categoricalBlue", (string)data["series"][0]["color"]);
        data["series"][0]["values"][1]["y"] = 90;
        Assert.AreEqual(25d, (double)series[0]["values"][1]["y"]);
    }

    [TestMethod]
    public void Data_BatteryUsesChargeAndStatusWithNativeProgress()
    {
        var snapshot = new JsonObject
        {
            ["batteryCharge"] = "80%",
            ["batteryStatus"] = "Charging",
            ["batteryPercent"] = 80,
            ["batteryColor"] = "good",
        };

        var data = PerformanceSmallContent.CreateData(PerformanceMetricKind.Battery, "Battery", snapshot);

        Assert.AreEqual("80%", (string)data["value"]);
        Assert.AreEqual("Charging", (string)data["detail"]);
        Assert.AreEqual(80, (int)data["batteryPercent"]);
        Assert.AreEqual("good", (string)data["batteryColor"]);
        Assert.IsTrue((bool)data["isBattery"]);
        Assert.IsFalse((bool)data["hasChart"]);
    }

    [TestMethod]
    public void Data_MissingHistoryDoesNotShowAnEmptyChartAndPreservesErrors()
    {
        var data = PerformanceSmallContent.CreateData(PerformanceMetricKind.Cpu, "CPU", new JsonObject { ["errorMessage"] = "Unavailable" });

        Assert.IsFalse((bool)data["hasChart"]);
        Assert.AreEqual(0, ((JsonArray)data["series"]).Count);
        Assert.AreEqual("Unavailable", (string)data["error"]);

        var emptySeries = new JsonObject
        {
            ["cpuSeries"] = PerformanceChartData.Create(new PerformanceChartData.Series("CPU", "categoricalBlue", [])),
        };
        Assert.IsFalse((bool)PerformanceSmallContent.CreateData(PerformanceMetricKind.Cpu, "CPU", emptySeries)["hasChart"]);
    }

    [TestMethod]
    [DataRow(2, 1, 2)]
    [DataRow(5, 2, 3)]
    [DataRow(6, 2, 3)]
    public void GroupedTemplate_UsesCompactRowsForDefaultAndOverview(int count, int expectedRows, int expectedColumns)
    {
        var widgets = Enumerable.Range(0, count).Select(_ => (WidgetPage)new TrackingWidget()).ToArray();
        var content = new PerformanceSmallContent(widgets, new Size(336, 100));
        using var card = JsonDocument.Parse(content.TemplateJson);
        var rows = card.RootElement.GetProperty("body");

        Assert.AreEqual(expectedRows, rows.GetArrayLength());
        Assert.AreEqual(expectedColumns, rows[0].GetProperty("columns").GetArrayLength());
        foreach (var row in rows.EnumerateArray())
        {
            Assert.AreEqual("none", row.GetProperty("spacing").GetString());
            foreach (var column in row.GetProperty("columns").EnumerateArray())
            {
                Assert.AreEqual("small", column.GetProperty("items")[1].GetProperty("size").GetString());
            }
        }

        Assert.IsFalse(content.TemplateJson.Contains("\"Image\""));
        if (count > 2)
        {
            Assert.IsFalse(content.TemplateJson.Contains("Chart.Line"));
        }

        using var data = JsonDocument.Parse(content.DataJson);
        Assert.AreEqual(count, data.RootElement.GetProperty("widgets").GetArrayLength());
    }

    [TestMethod]
    public void GroupedContent_ActivatesAndReleasesEverySourceAndPublishesUpdates()
    {
        var first = new TrackingWidget();
        var second = new TrackingWidget();
        var content = new PerformanceSmallContent([first, second], new Size(336, 100));
        var updates = 0;
        TypedEventHandler<object, IPropChangedEventArgs> handler = (_, _) => updates++;

        content.PropChanged += handler;
        first.UpdateWidget();
        second.UpdateWidget();
        content.PropChanged -= handler;
        first.UpdateWidget();

        Assert.AreEqual(2, updates);
        Assert.AreEqual(1, first.Activations);
        Assert.AreEqual(1, second.Activations);
        Assert.AreEqual(1, first.Deactivations);
        Assert.AreEqual(1, second.Deactivations);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(6)]
    public void Template_ExpandsEveryWidgetAndKeepsChartValuesNumeric(int count)
    {
        var widgets = Enumerable.Range(0, count).Select(_ => new TrackingWidget()).ToArray();
        foreach (var widget in widgets)
        {
            widget.Snapshot["cpuUsage"] = "25%";
            widget.Snapshot["cpuSeries"] = PerformanceChartData.Create(new PerformanceChartData.Series("CPU", "categoricalBlue", [10f, 25f]));
        }

        var content = new PerformanceSmallContent(widgets, new Size(336, 100));
        var expanded = new AdaptiveCardTemplate(content.TemplateJson).Expand(content.DataJson);
        using var card = JsonDocument.Parse(expanded);

        Assert.IsFalse(expanded.Contains("${"));
        var columns = card.RootElement.GetProperty("body")[0].GetProperty("columns");
        var chart = count == 1
            ? columns[1].GetProperty("items")[0]
            : columns[0].GetProperty("items").EnumerateArray().FirstOrDefault(item => item.GetProperty("type").GetString() == "Chart.Line");
        if (count <= 2)
        {
            Assert.AreEqual(30d, chart.GetProperty("yMax").GetDouble());
            Assert.AreEqual(25d, chart.GetProperty("data")[0].GetProperty("values")[1].GetProperty("y").GetDouble());
        }
    }

    [TestMethod]
    public void Provider_ResolvesAllAdvertisedSmallContentIncludingBothOverviewIds()
    {
        using var provider = new PerformanceMonitorCommandsProvider();
        var ids = provider.GetAvailableSmallContent();
        CollectionAssert.Contains(ids, "com.microsoft.cmdpal.overview_widget");
        CollectionAssert.Contains(ids, PerformanceWidgetsPage.GetBandId(PerformanceMetricKind.Overview));

        foreach (var id in ids)
        {
            var content = provider.GetSmallContent(id, new Size(336, 100));
            Assert.IsInstanceOfType<IFormContent>(content, $"No compact form was returned for {id}.");
        }
    }

    private sealed partial class TrackingWidget : WidgetPage
    {
        public int Activations { get; private set; }

        public int Deactivations { get; private set; }

        public JsonObject Snapshot { get; } = new();

        internal override JsonObject GetSmallContentData() =>
            PerformanceSmallContent.CreateData(PerformanceMetricKind.Cpu, "CPU", Snapshot);

        protected override void LoadContentData()
        {
        }

        protected override string GetTemplatePath(WidgetPageState page) => string.Empty;

        protected override void OnActivated() => Activations++;

        protected override void OnDeactivated() => Deactivations++;
    }
}
