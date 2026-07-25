// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
        public IContent[] Content { get; init; } = [];

        public override IContent[] GetContent() => Content;
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
    public void InlineAdapterCommands_AreNotPromotedToPageKeyboardActions()
    {
        var form = new FormContent
        {
            TemplateJson = NativeFormContentTypes.PerformanceOverview,
            DataJson = BuildPayload("cpu", "11%", 11, 22, 33, 44, 55),
        };
        var page = new TestContentPage
        {
            Content = [form],
            Commands =
            [
                new CommandContextItem(new NoOpCommand { Name = "Previous GPU" }),
                new CommandContextItem(new NoOpCommand { Name = "Next GPU" }),
            ],
        };
        var pageViewModel = new CommandPaletteContentPageViewModel(
            page,
            TaskScheduler.Default,
            new TestAppExtensionHost(),
            CommandProviderContext.Empty);

        pageViewModel.InitializeProperties();

        Assert.IsTrue(pageViewModel.HasInlineCommandSurface);
        Assert.IsFalse(pageViewModel.HasCommands);
        Assert.IsFalse(pageViewModel.HasMoreCommands);
        Assert.IsFalse(pageViewModel.CanOpenContextMenu);
        Assert.IsNull(pageViewModel.PrimaryCommand);
        Assert.IsNull(pageViewModel.SecondaryCommand);
        Assert.AreEqual(2, pageViewModel.AllCommands.Count);
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
        Assert.AreEqual("NVIDIA Test GPU", viewModel.GpuAdapterName);
        Assert.AreEqual("Test Ethernet", viewModel.NetworkAdapterName);
        Assert.IsTrue(viewModel.CanSwitchGpu);
        Assert.IsTrue(viewModel.CanSwitchNetwork);
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

    [TestMethod]
    public void AdapterCommands_RouteStableCommandIds()
    {
        var invokedCommandIds = new List<string>();
        var form = new FormContent
        {
            TemplateJson = NativeFormContentTypes.PerformanceOverview,
            DataJson = BuildPayload("cpu", "11%", 11, 22, 33, 44, 55),
        };
        var viewModel = new ContentPerformanceOverviewViewModel(
            form,
            new WeakReference<IPageContext>(new TestPageContext()),
            invokedCommandIds.Add);

        viewModel.PreviousGpuCommand.Execute(null);
        viewModel.NextGpuCommand.Execute(null);
        viewModel.PreviousNetworkCommand.Execute(null);
        viewModel.NextNetworkCommand.Execute(null);

        CollectionAssert.AreEqual(
            new[]
            {
                NativePerformanceOverviewCommandIds.PreviousGpu,
                NativePerformanceOverviewCommandIds.NextGpu,
                NativePerformanceOverviewCommandIds.PreviousNetwork,
                NativePerformanceOverviewCommandIds.NextNetwork,
            },
            invokedCommandIds);
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
            ["gpuAdapterName"] = "NVIDIA Test GPU",
            ["canSwitchGpu"] = true,
            ["gpuDetailText"] = $"{gpuPercent}% · 40 °C",
            ["gpuPercent"] = gpuPercent,
            ["memoryLabelText"] = "RAM",
            ["memoryDetailText"] = "8.0 GB of 16.0 GB",
            ["memoryPercent"] = memoryPercent,
            ["diskLabelText"] = "Disk",
            ["diskDetailText"] = "Read 1.0 MB/s · Write 2.0 MB/s",
            ["diskPercent"] = diskPercent,
            ["networkLabelText"] = "Network",
            ["networkAdapterName"] = "Test Ethernet",
            ["canSwitchNetwork"] = true,
            ["networkDetailText"] = "Send 1.0 MB/s · Receive 2.0 MB/s",
            ["networkPercent"] = networkPercent,
            ["previousGpuCommandText"] = "Previous GPU",
            ["nextGpuCommandText"] = "Next GPU",
            ["previousNetworkCommandText"] = "Previous network",
            ["nextNetworkCommandText"] = "Next network",
        }.ToJsonString();
    }
}
