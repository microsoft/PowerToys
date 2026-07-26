// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading;
using CoreWidgetProvider.Helpers;
using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

/// <summary>
/// Compact, single-glance dashboard opened from the Performance Monitor dock
/// band. It follows the Dock design with a contextual headline and thin,
/// full-width CPU/GPU/RAM/Disk/Network utilization rows.
/// </summary>
internal sealed partial class PerformanceOverviewPage : OnLoadContentPage, IDisposable
{
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(150);

    public override string Id => $"com.microsoft.cmdpal.performanceWidget.{PerformanceWidgetsPage.GetMetricSuffix(_heroMetric)}.dockOverview";

    public override string Title => Resources.GetResource("Performance_Monitor_Title");

    public override IconInfo Icon => Icons.PerformanceMonitorIcon;

    private readonly Lock _refreshLock = new();
    private readonly FormContent _formContent = new()
    {
        TemplateJson = NativeFormContentTypes.PerformanceOverview,
    };

    private readonly Timer _refreshTimer;
    private readonly PerformanceMetricKind _heroMetric;
    private readonly string _heroLabel;
    private readonly RollingThroughputNormalizer _networkThroughputNormalizer;
    private readonly RollingThroughputNormalizer _networkDirectionNormalizer;
    private readonly RollingThroughputNormalizer _diskDirectionNormalizer;

    private readonly SystemCPUUsageWidgetPage _cpuPage;
    private readonly SystemMemoryUsageWidgetPage _memoryPage;
    private readonly SystemDiskUsageWidgetPage _diskPage;
    private readonly SystemNetworkUsageWidgetPage _networkPage;
    private readonly SystemGPUUsageWidgetPage _gpuPage;

    private bool _isLoaded;
    private bool _disposed;

    public PerformanceOverviewPage(
        PerformanceWidgetsPage metricsPage,
        PerformanceMetricKind heroMetric,
        RollingThroughputNormalizer networkThroughputNormalizer,
        RollingThroughputNormalizer networkDirectionNormalizer,
        RollingThroughputNormalizer diskDirectionNormalizer)
    {
        _heroMetric = heroMetric;
        _networkThroughputNormalizer = networkThroughputNormalizer;
        _networkDirectionNormalizer = networkDirectionNormalizer;
        _diskDirectionNormalizer = diskDirectionNormalizer;
        _heroLabel = PerformanceOverviewFormatter.SelectMetricValue(
            heroMetric,
            Resources.GetResource("CPU_Usage_Subtitle"),
            Resources.GetResource("Overview_RAM_Label"),
            Resources.GetResource("Network_Usage_Subtitle"),
            Resources.GetResource("Disk_Usage_Subtitle"),
            Resources.GetResource("GPU_Usage_Subtitle"));

        _cpuPage = metricsPage.CpuPage;
        _memoryPage = metricsPage.MemoryPage;
        _diskPage = metricsPage.DiskPage;
        _networkPage = metricsPage.NetworkPage;
        _gpuPage = metricsPage.GpuPage;

        Commands = [
            _gpuPage.PreviousAdapterCommand,
            _gpuPage.NextAdapterCommand,
            _networkPage.PreviousAdapterCommand,
            _networkPage.NextAdapterCommand,
        ];

        _refreshTimer = new Timer(
            _ => RefreshIfLoaded(),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);

        _cpuPage.Updated += OnMetricUpdated;
        _memoryPage.Updated += OnMetricUpdated;
        _diskPage.Updated += OnMetricUpdated;
        _networkPage.Updated += OnMetricUpdated;
        _gpuPage.Updated += OnMetricUpdated;
    }

    protected override void Loaded()
    {
        lock (_refreshLock)
        {
            if (_disposed)
            {
                return;
            }

            _isLoaded = true;
        }

        _cpuPage.PushActivate();
        _memoryPage.PushActivate();
        _diskPage.PushActivate();
        _networkPage.PushActivate();
        _gpuPage.PushActivate();

        Refresh();
    }

    protected override void Unloaded()
    {
        lock (_refreshLock)
        {
            _isLoaded = false;
            if (!_disposed)
            {
                _refreshTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        }

        _cpuPage.PopActivate();
        _memoryPage.PopActivate();
        _diskPage.PopActivate();
        _networkPage.PopActivate();
        _gpuPage.PopActivate();
    }

    public override IContent[] GetContent()
    {
        if (string.IsNullOrEmpty(_formContent.DataJson))
        {
            Refresh();
        }

        return [_formContent];
    }

    private void OnMetricUpdated(object? sender, EventArgs e)
    {
        lock (_refreshLock)
        {
            if (_isLoaded && !_disposed)
            {
                // All metric timers fire in the same burst. Debounce that burst
                // so the native dashboard receives one coherent sample.
                _refreshTimer.Change(RefreshDelay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void Refresh()
    {
        lock (_refreshLock)
        {
            if (!_disposed)
            {
                RefreshCore();
            }
        }
    }

    private void RefreshIfLoaded()
    {
        lock (_refreshLock)
        {
            if (_isLoaded && !_disposed)
            {
                RefreshCore();
            }
        }
    }

    private void RefreshCore() => _formContent.DataJson = BuildDataJson().ToJsonString();

    private JsonObject BuildDataJson()
    {
        var json = new JsonObject();
        var timestamp = DateTimeOffset.UtcNow;

        var cpuPercentText = AddCpuData(json);
        var memoryPercentText = AddMemoryData(json);
        var gpuPercentText = AddGpuData(json);
        var diskPercentText = AddDiskData(json, timestamp);
        var networkPercentText = AddNetworkData(json, timestamp);

        json["schemaVersion"] = 1;
        json["titleText"] = Resources.GetResource("Performance_Monitor_Title");
        json["statusText"] = Resources.GetResource("Overview_Live_Status");
        json["heroMetric"] = PerformanceOverviewFormatter.GetMetricKey(_heroMetric);
        json["heroLabelText"] = _heroLabel;
        json["heroValueText"] = PerformanceOverviewFormatter.SelectMetricValue(
            _heroMetric,
            cpuPercentText,
            memoryPercentText,
            networkPercentText,
            diskPercentText,
            gpuPercentText);
        json["previousGpuCommandText"] = Resources.GetResource("Previous_GPU_Title");
        json["nextGpuCommandText"] = Resources.GetResource("Next_GPU_Title");
        json["previousNetworkCommandText"] = Resources.GetResource("Previous_Network_Title");
        json["nextNetworkCommandText"] = Resources.GetResource("Next_Network_Title");

        return json;
    }

    private string AddCpuData(JsonObject json)
    {
        var rawPercentText = _cpuPage.GetContentValue("cpuUsage");
        var percentText = FirstNonEmpty(rawPercentText, Resources.GetResource("CPU_Usage_Unknown"));

        json["cpuLabelText"] = Resources.GetResource("CPU_Usage_Subtitle");
        json["cpuDetailText"] = FormatPercentDetail(percentText, _cpuPage.GetContentValue("cpuSpeed"));
        json["cpuPercent"] = PerformanceOverviewFormatter.ParsePercentText(rawPercentText);
        return percentText;
    }

    private string AddMemoryData(JsonObject json)
    {
        var rawPercentText = _memoryPage.GetContentValue("memUsage");
        var percentText = FirstNonEmpty(rawPercentText, Resources.GetResource("Memory_Usage_Unknown"));

        json["memoryLabelText"] = Resources.GetResource("Overview_RAM_Label");
        json["memoryDetailText"] = string.Format(
            CultureInfo.CurrentCulture,
            Resources.GetResource("Overview_Memory_Detail_Format"),
            _memoryPage.GetContentValue("usedMem"),
            _memoryPage.GetContentValue("allMem"));
        json["memoryPercent"] = PerformanceOverviewFormatter.ParsePercentText(rawPercentText);
        return percentText;
    }

    private string AddGpuData(JsonObject json)
    {
        var rawPercentText = _gpuPage.GetContentValue("gpuUsage");
        var percentText = FirstNonEmpty(rawPercentText, Resources.GetResource("GPU_Usage_Unknown"));
        var temperatureText = _gpuPage.GetContentValue("gpuTemp");

        json["gpuLabelText"] = Resources.GetResource("GPU_Usage_Subtitle");
        json["gpuAdapterName"] = _gpuPage.GetContentValue("gpuName");
        json["canSwitchGpu"] = _gpuPage.GetAdapterCount() > 1;
        json["gpuDetailText"] = temperatureText == "--"
            ? percentText
            : FormatPercentDetail(percentText, temperatureText);
        json["gpuPercent"] = PerformanceOverviewFormatter.ParsePercentText(rawPercentText);
        return percentText;
    }

    private string AddDiskData(JsonObject json, DateTimeOffset timestamp)
    {
        var rawPercentText = _diskPage.GetContentValue("diskUsage");
        var percentText = FirstNonEmpty(rawPercentText, Resources.GetResource("Disk_Usage_Unknown"));
        var readLabel = Resources.GetResource("Disk_Read_Subtitle");
        var writeLabel = Resources.GetResource("Disk_Write_Subtitle");
        var readText = _diskPage.GetContentValue("diskRead");
        var writeText = _diskPage.GetContentValue("diskWrite");
        var throughput = _diskPage.GetThroughputBytesPerSecond();
        var directionPercentages = _diskDirectionNormalizer.AddPairSample(
            throughput.Read,
            throughput.Write,
            timestamp);
        var diskLabel = Resources.GetResource("Disk_Usage_Subtitle");

        json["diskLabelText"] = diskLabel;
        json["diskDetailText"] = string.Format(
            CultureInfo.CurrentCulture,
            Resources.GetResource("Overview_Disk_Detail_Format"),
            readLabel,
            readText,
            writeLabel,
            writeText);
        json["diskReadLabelText"] = FormatDirectionalLabel(diskLabel, readLabel);
        json["diskWriteLabelText"] = FormatDirectionalLabel(diskLabel, writeLabel);
        json["diskReadText"] = FormatDirectionalValue(readLabel, readText);
        json["diskWriteText"] = FormatDirectionalValue(writeLabel, writeText);
        json["diskReadPercent"] = directionPercentages.FirstPercent;
        json["diskWritePercent"] = directionPercentages.SecondPercent;
        json["diskPercent"] = PerformanceOverviewFormatter.ParsePercentText(rawPercentText);
        return percentText;
    }

    private string AddNetworkData(JsonObject json, DateTimeOffset timestamp)
    {
        var throughput = _networkPage.GetThroughputBytesPerSecond();
        var networkPercent = _networkThroughputNormalizer.AddSample(
            throughput.Sent + throughput.Received,
            timestamp);
        var directionPercentages = _networkDirectionNormalizer.AddPairSample(
            throughput.Sent,
            throughput.Received,
            timestamp);
        var percentText = networkPercent.ToString(CultureInfo.InvariantCulture) + "%";
        var sendLabel = Resources.GetResource("Network_Send_Subtitle");
        var receiveLabel = Resources.GetResource("Network_Receive_Subtitle");
        var sendText = _networkPage.GetContentValue("netSent");
        var receiveText = _networkPage.GetContentValue("netReceived");
        var networkLabel = Resources.GetResource("Network_Usage_Subtitle");

        json["networkLabelText"] = networkLabel;
        json["networkAdapterName"] = _networkPage.GetContentValue("networkName");
        json["canSwitchNetwork"] = _networkPage.GetAdapterCount() > 1;
        json["networkDetailText"] = string.Format(
            CultureInfo.CurrentCulture,
            Resources.GetResource("Overview_Network_Detail_Format"),
            sendLabel,
            sendText,
            receiveLabel,
            receiveText);
        json["networkSendLabelText"] = FormatDirectionalLabel(networkLabel, sendLabel);
        json["networkReceiveLabelText"] = FormatDirectionalLabel(networkLabel, receiveLabel);
        json["networkSendText"] = FormatDirectionalValue(sendLabel, sendText);
        json["networkReceiveText"] = FormatDirectionalValue(receiveLabel, receiveText);
        json["networkSendPercent"] = directionPercentages.FirstPercent;
        json["networkReceivePercent"] = directionPercentages.SecondPercent;
        json["networkPercent"] = networkPercent;
        return percentText;
    }

    private static string FirstNonEmpty(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    private static string FormatDirectionalLabel(string metricLabel, string directionLabel) =>
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.GetResource("Overview_Directional_Label_Format"),
            metricLabel,
            directionLabel);

    private static string FormatDirectionalValue(string label, string value) =>
        string.Format(
            CultureInfo.CurrentCulture,
            Resources.GetResource("Overview_Directional_Value_Format"),
            label,
            value);

    private static string FormatPercentDetail(string percentText, string detailText) =>
        string.IsNullOrEmpty(detailText)
            ? percentText
            : string.Format(
                CultureInfo.CurrentCulture,
                Resources.GetResource("Overview_Percent_Detail_Format"),
                percentText,
                detailText);

    public void Dispose()
    {
        _cpuPage.Updated -= OnMetricUpdated;
        _memoryPage.Updated -= OnMetricUpdated;
        _diskPage.Updated -= OnMetricUpdated;
        _networkPage.Updated -= OnMetricUpdated;
        _gpuPage.Updated -= OnMetricUpdated;

        lock (_refreshLock)
        {
            _isLoaded = false;
            _disposed = true;
            _refreshTimer.Dispose();
        }
    }
}
