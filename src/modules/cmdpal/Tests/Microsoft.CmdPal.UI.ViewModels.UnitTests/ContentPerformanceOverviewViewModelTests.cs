// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class ContentPerformanceOverviewViewModelTests
{
    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) => Assert.Fail(ex.ToString());
    }

    private sealed class RecordingPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public Exception? LastException { get; private set; }

        public void ShowException(Exception ex, string? extensionHint = null) => LastException = ex;
    }

    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed partial class TestContentPage : Microsoft.CommandPalette.Extensions.Toolkit.ContentPage
    {
        public override IContent[] GetContent() => [];
    }

    [TestMethod]
    public void ViewModelFromContent_UsesNativeRendererOnlyForPerformanceMarker()
    {
        var context = new TestPageContext();
        var contextReference = new WeakReference<IPageContext>(context);
        var pageViewModel = new CommandPaletteContentPageViewModel(
            new TestContentPage(),
            TaskScheduler.Default,
            new TestAppExtensionHost(),
            CommandProviderContext.Empty);

        var nativeForm = new FormContent
        {
            TemplateJson = NativeFormContentTypes.PerformanceOverview,
            DataJson = BuildPayload("cpu", "11%", 11, 22, 33, 44, 55),
        };
        var adaptiveForm = new FormContent
        {
            TemplateJson = "{}",
            DataJson = "{}",
        };

        Assert.IsInstanceOfType<ContentPerformanceOverviewViewModel>(
            pageViewModel.ViewModelFromContent(nativeForm, contextReference));
        Assert.IsInstanceOfType<ContentFormViewModel>(
            pageViewModel.ViewModelFromContent(adaptiveForm, contextReference));
    }

    [TestMethod]
    public void DataJsonUpdate_MutatesExistingObservableViewModel()
    {
        var context = new TestPageContext();
        var form = new FormContent
        {
            TemplateJson = NativeFormContentTypes.PerformanceOverview,
            DataJson = BuildPayload("cpu", "11%", 11, 22, 33, 44, 55),
        };
        var viewModel = new ContentPerformanceOverviewViewModel(
            form,
            new WeakReference<IPageContext>(context));

        viewModel.InitializeProperties();
        var originalViewModel = viewModel;

        form.DataJson = BuildPayload("network", "66%", 12, 23, 34, 45, 66);

        Assert.AreSame(originalViewModel, viewModel);
        Assert.AreEqual("network", viewModel.HeroMetric);
        Assert.AreEqual("Network", viewModel.HeroLabelText);
        Assert.AreEqual("66%", viewModel.HeroValueText);
        Assert.AreEqual(12, viewModel.CpuPercent);
        Assert.AreEqual(23, viewModel.GpuPercent);
        Assert.AreEqual(34, viewModel.MemoryPercent);
        Assert.AreEqual(45, viewModel.DiskPercent);
        Assert.AreEqual(66, viewModel.NetworkPercent);
        Assert.AreEqual("Send 1.0 MB/s · Receive 2.0 MB/s", viewModel.NetworkDetailText);
    }

    [TestMethod]
    public void DataJsonUpdate_ClampsPercentValues()
    {
        var context = new TestPageContext();
        var form = new FormContent
        {
            TemplateJson = NativeFormContentTypes.PerformanceOverview,
            DataJson = BuildPayload("memory", "100%", -1, 101, 50, 0, 500),
        };
        var viewModel = new ContentPerformanceOverviewViewModel(
            form,
            new WeakReference<IPageContext>(context));

        viewModel.InitializeProperties();

        Assert.AreEqual(0, viewModel.CpuPercent);
        Assert.AreEqual(100, viewModel.GpuPercent);
        Assert.AreEqual(50, viewModel.MemoryPercent);
        Assert.AreEqual(0, viewModel.DiskPercent);
        Assert.AreEqual(100, viewModel.NetworkPercent);
    }

    [TestMethod]
    public void EmptyInitialData_AcceptsFirstLiveSampleWithoutRecreatingViewModel()
    {
        var context = new TestPageContext();
        var form = new FormContent
        {
            TemplateJson = NativeFormContentTypes.PerformanceOverview,
            DataJson = string.Empty,
        };
        var viewModel = new ContentPerformanceOverviewViewModel(
            form,
            new WeakReference<IPageContext>(context));

        viewModel.InitializeProperties();
        form.DataJson = BuildPayload("cpu", "37%", 37, 10, 20, 30, 40);

        Assert.AreEqual("cpu", viewModel.HeroMetric);
        Assert.AreEqual("37%", viewModel.HeroValueText);
        Assert.AreEqual(37, viewModel.CpuPercent);
    }

    [TestMethod]
    public void MalformedUpdate_DoesNotPartiallyMutateExistingSample()
    {
        var context = new RecordingPageContext();
        var form = new FormContent
        {
            TemplateJson = NativeFormContentTypes.PerformanceOverview,
            DataJson = BuildPayload("cpu", "11%", 11, 22, 33, 44, 55),
        };
        var viewModel = new ContentPerformanceOverviewViewModel(
            form,
            new WeakReference<IPageContext>(context));

        viewModel.InitializeProperties();
        form.DataJson = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["titleText"] = "Incomplete update",
        }.ToJsonString();

        Assert.IsNotNull(context.LastException);
        Assert.AreEqual("Performance monitor", viewModel.TitleText);
        Assert.AreEqual("11%", viewModel.HeroValueText);
        Assert.AreEqual(11, viewModel.CpuPercent);
    }

    private static string BuildPayload(
        string heroMetric,
        string heroValue,
        int cpuPercent,
        int gpuPercent,
        int memoryPercent,
        int diskPercent,
        int networkPercent)
    {
        var heroLabel = heroMetric switch
        {
            "cpu" => "CPU",
            "gpu" => "GPU",
            "memory" => "RAM",
            "disk" => "Disk",
            "network" => "Network",
            _ => throw new ArgumentOutOfRangeException(nameof(heroMetric)),
        };

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["titleText"] = "Performance monitor",
            ["statusText"] = "Live",
            ["heroMetric"] = heroMetric,
            ["heroLabelText"] = heroLabel,
            ["heroValueText"] = heroValue,
            ["cpuLabelText"] = "CPU",
            ["cpuDetailText"] = $"{cpuPercent}% · 4.0 GHz",
            ["cpuPercent"] = cpuPercent,
            ["gpuLabelText"] = "GPU",
            ["gpuDetailText"] = $"{gpuPercent}% · 40 °C",
            ["gpuPercent"] = gpuPercent,
            ["memoryLabelText"] = "RAM",
            ["memoryDetailText"] = "8.0 GB of 16.0 GB",
            ["memoryPercent"] = memoryPercent,
            ["diskLabelText"] = "Disk",
            ["diskDetailText"] = "Read 1.0 MB/s · Write 2.0 MB/s",
            ["diskPercent"] = diskPercent,
            ["networkLabelText"] = "Network",
            ["networkDetailText"] = "Send 1.0 MB/s · Receive 2.0 MB/s",
            ["networkPercent"] = networkPercent,
        }.ToJsonString();
    }
}
