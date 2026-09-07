// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using CoreWidgetProvider.Helpers;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public partial class WidgetGraphTests
{
    [TestMethod]
    public void UsagePages_ExposeNativeGraphsAlongsideTheirReadings()
    {
        var settings = new SettingsManager(Path.Combine(Path.GetTempPath(), $"PerformanceMonitorGraphTests-{Guid.NewGuid():N}.json"));
        using var cpu = new SystemCPUUsageWidgetPage(settings);
        using var memory = new SystemMemoryUsageWidgetPage();
        using var disk = new SystemDiskUsageWidgetPage(settings);
        using var network = new SystemNetworkUsageWidgetPage(settings);
        using var gpu = new SystemGPUUsageWidgetPage();
        WidgetPage[] pages = [cpu, memory, disk, network, gpu];

        foreach (var page in pages)
        {
            var content = page.GetContent();
            Assert.HasCount(2, content);
            Assert.IsInstanceOfType<ILineGraphContent>(content[0]);
            var graph = (ILineGraphContent)content[0];
            Assert.IsInstanceOfType<IFormContent>(content[1]);
            Assert.AreEqual(0d, graph.Minimum);
            Assert.AreEqual(100d, graph.Maximum);
            Assert.AreEqual(UsageHistory.Duration, graph.HistoryDuration);
            Assert.AreEqual("%", graph.ValueSuffix);
            Assert.AreSame(content[0], page.GetContent()[0]);
        }

        var cpuGraph = (ILineGraphContent)cpu.GetContent()[0];
        var series = cpuGraph.GetSeries();
        Assert.HasCount(1, series);
        Assert.AreEqual(Resources.GetResource("CPU_Total_Time"), series[0].Name);
        Assert.AreEqual(GraphLineStyle.Solid, series[0].LineStyle);
        Assert.AreEqual(0.2d, cpuGraph.Smoothing);
    }

    [TestMethod]
    public void KernelTimeSetting_UpdatesExistingCpuPagesAndStopsAfterDisposal()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"PerformanceMonitorGraphTests-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new SettingsManager(filePath);
            using var sampler = new CpuSampler(static () => new StubCpuSource());
            using var first = new SystemCPUUsageWidgetPage(settings, sampler);
            using var second = new SystemCPUUsageWidgetPage(settings, sampler);
            var original = first.GetContent();
            var notifications = 0;
            TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) => notifications++;
            first.ItemsChanged += handler;
            first.PopActivate(); // Keep this settings test independent of hardware sampling.

            try
            {
                var form = (IFormContent)settings.Settings.ToContent()[0];
                form.SubmitForm("""{"performanceMonitor.ShowKernelTime":"true"}""", "{}");
                Assert.AreEqual(1, notifications);

                foreach (var page in new[] { first, second })
                {
                    var graph = (ILineGraphContent)page.GetContent()[0];
                    var series = graph.GetSeries();
                    Assert.HasCount(2, series);
                    Assert.AreEqual(Resources.GetResource("CPU_Kernel_Time"), series[1].Name);
                    Assert.AreEqual(GraphLineStyle.Solid, series[0].LineStyle);
                    Assert.AreEqual(GraphLineStyle.Dashed, series[1].LineStyle);
                    Assert.AreNotEqual(series[0].Color, series[1].Color);
                    Assert.AreEqual(0.2d, graph.Smoothing);
                }

                var enabled = first.GetContent();
                Assert.AreNotSame(original[0], enabled[0]);
                Assert.AreSame(original[1], enabled[1]);
                Assert.HasCount(1, ((ILineGraphContent)original[0]).GetSeries());

                // Saving an unchanged option preserves the graph and its presentation.
                form.SubmitForm("""{"performanceMonitor.ShowKernelTime":"true"}""", "{}");
                Assert.AreSame(enabled[0], first.GetContent()[0]);
                Assert.AreEqual(1, notifications);

                form.SubmitForm("""{"performanceMonitor.ShowKernelTime":"false"}""", "{}");
                var disabled = first.GetContent()[0];
                Assert.HasCount(1, ((ILineGraphContent)disabled).GetSeries());
                Assert.HasCount(1, ((ILineGraphContent)second.GetContent()[0]).GetSeries());
                Assert.AreEqual(2, notifications);
                Assert.HasCount(2, ((ILineGraphContent)enabled[0]).GetSeries());

                first.Dispose();
                form.SubmitForm("""{"performanceMonitor.ShowKernelTime":"true"}""", "{}");
                Assert.AreSame(disabled, first.GetContent()[0]);
                Assert.HasCount(2, ((ILineGraphContent)second.GetContent()[0]).GetSeries());
                Assert.AreEqual(2, notifications);
            }
            finally
            {
                first.ItemsChanged -= handler;
            }
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public void UpdateWidget_UpdatesExistingGraphAndClearsItOnFailure()
    {
        var page = new TestWidgetPage();
        var content = page.GetContent();
        var graph = (ILineGraphContent)content[0];
        var notifications = 0;
        graph.PropChanged += (_, args) =>
        {
            if (args.PropertyName == "Data")
            {
                notifications++;
            }
        };

        page.Samples = [GraphSampleHelpers.Create(0, DateTimeOffset.UtcNow, 42)];
        page.UpdateWidget();
        Assert.AreEqual(42d, graph.GetSnapshot()[0].Value);
        Assert.AreEqual(1, notifications);

        page.Fail = true;
        page.UpdateWidget();
        Assert.IsEmpty(graph.GetSnapshot());
        Assert.AreEqual(2, notifications);
        Assert.AreSame(graph, page.GetContent()[0]);
        Assert.Contains("Unavailable", ((IFormContent)content[1]).DataJson);
    }

    private sealed class StubCpuSource : ICpuSampleSource
    {
        public void ResetSamplingInterval()
        {
        }

        public CpuSample Sample(bool includeTopProcesses) => new(0, 0, 0);
    }

    private sealed partial class TestWidgetPage : WidgetPage
    {
        public GraphSample[] Samples { get; set; } = [];

        public bool Fail { get; set; }

        public TestWidgetPage()
        {
            UsageGraph = CreateUsageGraph(new GraphSeriesInfo { Name = "Usage" });
            Template[WidgetPageState.Content] = "{}";
        }

        protected override void LoadContentData()
        {
            ContentData.Clear();
            if (Fail)
            {
                ContentData["errorMessage"] = "Unavailable";
                return;
            }

            UsageGraph!.SetSnapshot(Samples);
        }

        protected override string GetTemplatePath(WidgetPageState page) => string.Empty;
    }
}
