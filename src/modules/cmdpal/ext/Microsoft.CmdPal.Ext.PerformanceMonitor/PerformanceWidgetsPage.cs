// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using CoreWidgetProvider.Helpers;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.CmdPal.Core.Common;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Windows.Widgets.Providers;
using Windows.ApplicationModel;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
///  asdfasdf
/// </summary>
/// <remarks>
/// Intentionally, we're using IListPage rather than ListPage. This is so we
/// can get the onload/onunload
/// </remarks>
internal sealed partial class PerformanceWidgetsPage : OnLoadStaticListPage, IDisposable
{
    public override string Id => "com.microsoft.cmdpal.performanceWidget";

    public override string Title => "Performance monitor";

    public override IconInfo Icon => Icons.StackedAreaIcon;

    private readonly bool _isBandPage;

    private readonly SystemCPUUsageWidgetPage _cpuPage = new();
    private readonly ListItem _cpuItem;

    private readonly SystemMemoryUsageWidgetPage _memoryPage = new();
    private readonly ListItem _memoryItem;

    private readonly SystemNetworkUsageWidgetPage _networkPage = new();
    private readonly ListItem _networkItem;

    private readonly SystemGPUUsageWidgetPage _gpuPage = new();
    private readonly ListItem _gpuItem;

    public PerformanceWidgetsPage(bool isBandPage = false)
    {
        _isBandPage = isBandPage;
        _cpuItem = new ListItem(_cpuPage)
        {
            Title = _cpuPage.GetItemTitle(),
        };

        _cpuPage.Updated += (s, e) =>
        {
            _cpuItem.Title = _cpuPage.GetItemTitle();
        };

        _memoryItem = new ListItem(_memoryPage)
        {
            Title = _memoryPage.GetItemTitle(),
        };

        _memoryPage.Updated += (s, e) =>
        {
            _memoryItem.Title = _memoryPage.GetItemTitle();
        };

        _networkItem = new ListItem(_networkPage)
        {
            Title = _networkPage.GetItemTitle(),
        };

        _networkPage.Updated += (s, e) =>
        {
            _networkItem.Title = _networkPage.GetItemTitle();
        };

        _gpuItem = new ListItem(_gpuPage)
        {
            Title = _gpuPage.GetItemTitle(),
        };

        _gpuPage.Updated += (s, e) =>
        {
            _gpuItem.Title = _gpuPage.GetItemTitle();
        };
    }

    protected override void Loaded()
    {
        _cpuPage.PushActivate();
        _memoryPage.PushActivate();
        _networkPage.PushActivate();
        _gpuPage.PushActivate();
    }

    protected override void Unloaded()
    {
        _cpuPage.PopActivate();
        _memoryPage.PopActivate();
        _networkPage.PopActivate();
        _gpuPage.PopActivate();
    }

    public override IListItem[] GetItems()
    {
        if (!_isBandPage)
        {
            // TODO! add details
        }

        return new[] { _cpuItem, _memoryItem, _networkItem, _gpuItem };
    }

    public void Dispose()
    {
        _cpuPage.Dispose();
        _memoryPage.Dispose();
        _networkPage.Dispose();
        _gpuPage.Dispose();
    }
}

internal abstract partial class WidgetPage : OnLoadContentPage
{
    internal event EventHandler? Updated;

    protected Dictionary<string, string> ContentData { get; } = new();

    protected WidgetPageState Page { get; set; } = WidgetPageState.Unknown;

    protected Dictionary<WidgetPageState, string> Template { get; set; } = new();

    protected JsonObject ContentDataJson
    {
        get
        {
            var json = new JsonObject();
            foreach (var kvp in ContentData)
            {
                if (kvp.Value is not null)
                {
                    json[kvp.Key] = kvp.Value;
                }
            }

            return json;
        }
    }

    private readonly FormContent _formContent = new();

    public void UpdateWidget()
    {
        LoadContentData();

        _formContent.DataJson = ContentDataJson.ToJsonString();

        Updated?.Invoke(this, EventArgs.Empty);

        // RaiseItemsChanged();
    }

    protected abstract void LoadContentData();

    protected abstract string GetTemplatePath(WidgetPageState page);

    protected string GetTemplateForPage(WidgetPageState page)
    {
        if (Template.TryGetValue(page, out var value))
        {
            CoreLogger.LogDebug($"Using cached template for {page}");
            return value;
        }

        try
        {
            var path = Path.Combine(Package.Current.EffectivePath, GetTemplatePath(page));
            var template = File.ReadAllText(path, Encoding.Default) ?? throw new FileNotFoundException(path);

            // template = Resources.ReplaceIdentifers(template, Resources.GetWidgetResourceIdentifiers(), Log);
            CoreLogger.LogDebug($"Caching template for {page}");
            Template[page] = template;
            return template;
        }
        catch (Exception)
        {
            // Log.Error(e, "Error getting template.");
            return string.Empty;
        }
    }

    public override IContent[] GetContent()
    {
        _formContent.TemplateJson = GetTemplateForPage(WidgetPageState.Content);

        return [_formContent];
    }

    internal virtual void PushActivate()
    {
        _loadCount++;
    }

    internal virtual void PopActivate()
    {
        _loadCount--;
    }

    private int _loadCount;

    protected bool IsActive => _loadCount > 0;

    protected override void Loaded()
    {
        PushActivate();
    }

    protected override void Unloaded()
    {
        PopActivate();
    }
}

internal sealed partial class SystemCPUUsageWidgetPage : WidgetPage, IDisposable
{
    public override string Id => "com.microsoft.cmdpal.systemcpuusagewidget";

    public override string Title => "CPU Usage";

    public override IconInfo Icon => Icons.CpuIcon;

    private readonly DataManager _dataManager;

    public SystemCPUUsageWidgetPage()
    {
        _dataManager = new(DataType.CPU, () => UpdateWidget());
    }

    protected override void LoadContentData()
    {
        CoreLogger.LogDebug("Getting CPU stats");
        try
        {
            ContentData.Clear();

            var timer = Stopwatch.StartNew();

            var currentData = _dataManager.GetCPUStats();

            var dataDuration = timer.ElapsedMilliseconds;

            ContentData["cpuUsage"] = FloatToPercentString(currentData.CpuUsage);
            ContentData["cpuSpeed"] = SpeedToString(currentData.CpuSpeed);
            ContentData["cpuGraphUrl"] = currentData.CreateCPUImageUrl();
            ContentData["chartHeight"] = ChartHelper.ChartHeight + "px";
            ContentData["chartWidth"] = ChartHelper.ChartWidth + "px";

            // ContentData["cpuProc1"] = currentData.GetCpuProcessText(0);
            // ContentData["cpuProc2"] = currentData.GetCpuProcessText(1);
            // ContentData["cpuProc3"] = currentData.GetCpuProcessText(2);
            var contentDuration = timer.ElapsedMilliseconds - dataDuration;

            CoreLogger.LogDebug($"CPU stats retrieved in {dataDuration} ms, content prepared in {contentDuration} ms. (Total {timer.ElapsedMilliseconds} ms)");

            // DataState = WidgetDataState.Okay;
        }
        catch (Exception e)
        {
            // Log.Error(e, "Error retrieving stats.");
            ContentData.Clear();
            ContentData["errorMessage"] = e.Message;

            // ContentData = content.ToJsonString();
            // DataState = WidgetDataState.Failed;
            return;
        }
    }

    protected override string GetTemplatePath(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.Content => @"DevHome\Templates\SystemCPUUsageTemplate.json",
            WidgetPageState.Loading => @"DevHome\Templates\SystemCPUUsageTemplate.json",
            _ => throw new NotImplementedException(),
        };
    }

    public string GetItemTitle()
    {
        if (ContentData.TryGetValue("cpuUsage", out var usage))
        {
            return $"CPU Usage: {usage}";
        }
        else
        {
            return "CPU Usage: ???";
        }
    }

    private string SpeedToString(float cpuSpeed)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:0.00} GHz", cpuSpeed / 1000);
    }

    private string FloatToPercentString(float value)
    {
        return ((int)(value * 100)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    internal override void PushActivate()
    {
        base.PushActivate();
        if (IsActive)
        {
            _dataManager.Start();
        }
    }

    internal override void PopActivate()
    {
        base.PopActivate();
        if (!IsActive)
        {
            _dataManager.Stop();
        }
    }

    public void Dispose()
    {
        _dataManager.Dispose();
    }
}

internal sealed partial class SystemMemoryUsageWidgetPage : WidgetPage, IDisposable
{
    public override string Id => "com.microsoft.cmdpal.systemmemoryusagewidget";

    public override string Title => "Memory Usage";

    public override IconInfo Icon => Icons.MemoryIcon;

    private readonly DataManager _dataManager;

    public SystemMemoryUsageWidgetPage()
    {
        _dataManager = new(DataType.Memory, () => UpdateWidget());
    }

    protected override void LoadContentData()
    {
        CoreLogger.LogDebug("Getting Memory stats");
        try
        {
            ContentData.Clear();

            var timer = Stopwatch.StartNew();

            var currentData = _dataManager.GetMemoryStats();

            var dataDuration = timer.ElapsedMilliseconds;

            ContentData["allMem"] = MemUlongToString(currentData.AllMem);
            ContentData["usedMem"] = MemUlongToString(currentData.UsedMem);
            ContentData["memUsage"] = FloatToPercentString(currentData.MemUsage);
            ContentData["committedMem"] = MemUlongToString(currentData.MemCommitted);
            ContentData["committedLimitMem"] = MemUlongToString(currentData.MemCommitLimit);
            ContentData["cachedMem"] = MemUlongToString(currentData.MemCached);
            ContentData["pagedPoolMem"] = MemUlongToString(currentData.MemPagedPool);
            ContentData["nonPagedPoolMem"] = MemUlongToString(currentData.MemNonPagedPool);
            ContentData["memGraphUrl"] = currentData.CreateMemImageUrl();
            ContentData["chartHeight"] = ChartHelper.ChartHeight + "px";
            ContentData["chartWidth"] = ChartHelper.ChartWidth + "px";

            var contentDuration = timer.ElapsedMilliseconds - dataDuration;

            CoreLogger.LogDebug($"Memory stats retrieved in {dataDuration} ms, content prepared in {contentDuration} ms. (Total {timer.ElapsedMilliseconds} ms)");
        }
        catch (Exception e)
        {
            ContentData.Clear();
            ContentData["errorMessage"] = e.Message;
            return;
        }
    }

    protected override string GetTemplatePath(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.Content => @"DevHome\Templates\SystemMemoryTemplate.json",
            WidgetPageState.Loading => @"DevHome\Templates\SystemMemoryTemplate.json",
            _ => throw new NotImplementedException(),
        };
    }

    public string GetItemTitle()
    {
        if (ContentData.TryGetValue("memUsage", out var usage))
        {
            return $"Memory Usage: {usage}";
        }
        else
        {
            return "Memory Usage: ???";
        }
    }

    private string FloatToPercentString(float value)
    {
        return ((int)(value * 100)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    private string MemUlongToString(ulong memBytes)
    {
        if (memBytes < 1024)
        {
            return memBytes.ToString(CultureInfo.InvariantCulture) + " B";
        }

        var memSize = memBytes / 1024.0;
        if (memSize < 1024)
        {
            return memSize.ToString("0.00", CultureInfo.InvariantCulture) + " kB";
        }

        memSize /= 1024;
        if (memSize < 1024)
        {
            return memSize.ToString("0.00", CultureInfo.InvariantCulture) + " MB";
        }

        memSize /= 1024;
        return memSize.ToString("0.00", CultureInfo.InvariantCulture) + " GB";
    }

    internal override void PushActivate()
    {
        base.PushActivate();
        if (IsActive)
        {
            _dataManager.Start();
        }
    }

    internal override void PopActivate()
    {
        base.PopActivate();
        if (!IsActive)
        {
            _dataManager.Stop();
        }
    }

    public void Dispose()
    {
        _dataManager.Dispose();
    }
}

internal sealed partial class SystemNetworkUsageWidgetPage : WidgetPage, IDisposable
{
    public override string Id => "com.microsoft.cmdpal.systemnetworkusagewidget";

    public override string Title => "Network Usage";

    public override IconInfo Icon => Icons.NetworkIcon;

    private readonly DataManager _dataManager;
    private int _networkIndex;

    public SystemNetworkUsageWidgetPage()
    {
        _dataManager = new(DataType.Network, () => UpdateWidget());
    }

    protected override void LoadContentData()
    {
        CoreLogger.LogDebug("Getting Network stats");
        try
        {
            ContentData.Clear();

            var timer = Stopwatch.StartNew();

            var currentData = _dataManager.GetNetworkStats();

            var dataDuration = timer.ElapsedMilliseconds;

            var netName = currentData.GetNetworkName(_networkIndex);
            var networkStats = currentData.GetNetworkUsage(_networkIndex);

            ContentData["networkUsage"] = FloatToPercentString(networkStats.Usage);
            ContentData["netSent"] = BytesToBitsPerSecString(networkStats.Sent);
            ContentData["netReceived"] = BytesToBitsPerSecString(networkStats.Received);
            ContentData["networkName"] = netName;
            ContentData["netGraphUrl"] = currentData.CreateNetImageUrl(_networkIndex);
            ContentData["chartHeight"] = ChartHelper.ChartHeight + "px";
            ContentData["chartWidth"] = ChartHelper.ChartWidth + "px";

            var contentDuration = timer.ElapsedMilliseconds - dataDuration;

            CoreLogger.LogDebug($"Network stats retrieved in {dataDuration} ms, content prepared in {contentDuration} ms. (Total {timer.ElapsedMilliseconds} ms)");
        }
        catch (Exception e)
        {
            ContentData.Clear();
            ContentData["errorMessage"] = e.Message;
            return;
        }
    }

    protected override string GetTemplatePath(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.Content => @"DevHome\Templates\SystemNetworkUsageTemplate.json",
            WidgetPageState.Loading => @"DevHome\Templates\SystemNetworkUsageTemplate.json",
            _ => throw new NotImplementedException(),
        };
    }

    public string GetItemTitle()
    {
        if (ContentData.TryGetValue("networkName", out var name) && ContentData.TryGetValue("networkUsage", out var usage))
        {
            return $"Network ({name}): {usage}";
        }
        else
        {
            return "Network Usage: ???";
        }
    }

    private string FloatToPercentString(float value)
    {
        return ((int)(value * 100)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    private string BytesToBitsPerSecString(float value)
    {
        // Bytes to bits
        value *= 8;

        // bits to Kbits
        value /= 1024;
        if (value < 1024)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} Kbps", value);
        }

        // Kbits to Mbits
        value /= 1024;
        return string.Format(CultureInfo.InvariantCulture, "{0:0.0} Mbps", value);
    }

    internal override void PushActivate()
    {
        base.PushActivate();
        if (IsActive)
        {
            _dataManager.Start();
        }
    }

    internal override void PopActivate()
    {
        base.PopActivate();
        if (!IsActive)
        {
            _dataManager.Stop();
        }
    }

    private void HandlePrevNetwork(WidgetActionInvokedArgs args)
    {
        _networkIndex = _dataManager.GetNetworkStats().GetPrevNetworkIndex(_networkIndex);
        UpdateWidget();
    }

    private void HandleNextNetwork(WidgetActionInvokedArgs args)
    {
        _networkIndex = _dataManager.GetNetworkStats().GetNextNetworkIndex(_networkIndex);
        UpdateWidget();
    }

    public void Dispose()
    {
        _dataManager.Dispose();
    }
}

internal sealed partial class SystemGPUUsageWidgetPage : WidgetPage, IDisposable
{
    public override string Id => "com.microsoft.cmdpal.systemgpuusagewidget";

    public override string Title => "GPU Usage";

    public override IconInfo Icon => Icons.GpuIcon;

    private readonly DataManager _dataManager;
    private readonly string _gpuActiveEngType = "3D";
    private int _gpuActiveIndex;

    public SystemGPUUsageWidgetPage()
    {
        _dataManager = new(DataType.GPU, () => UpdateWidget());
    }

    protected override void LoadContentData()
    {
        CoreLogger.LogDebug("Getting GPU stats");
        try
        {
            ContentData.Clear();

            var timer = Stopwatch.StartNew();

            var stats = _dataManager.GetGPUStats();

            var dataDuration = timer.ElapsedMilliseconds;

            var gpuName = stats.GetGPUName(_gpuActiveIndex);

            ContentData["gpuUsage"] = FloatToPercentString(stats.GetGPUUsage(_gpuActiveIndex, _gpuActiveEngType));
            ContentData["gpuName"] = gpuName;
            ContentData["gpuTemp"] = stats.GetGPUTemperature(_gpuActiveIndex);
            ContentData["gpuGraphUrl"] = stats.CreateGPUImageUrl(_gpuActiveIndex);
            ContentData["chartHeight"] = ChartHelper.ChartHeight + "px";
            ContentData["chartWidth"] = ChartHelper.ChartWidth + "px";

            var contentDuration = timer.ElapsedMilliseconds - dataDuration;

            CoreLogger.LogDebug($"GPU stats retrieved in {dataDuration} ms, content prepared in {contentDuration} ms. (Total {timer.ElapsedMilliseconds} ms)");
        }
        catch (Exception e)
        {
            ContentData.Clear();
            ContentData["errorMessage"] = e.Message;
            return;
        }
    }

    protected override string GetTemplatePath(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.Content => @"DevHome\Templates\SystemGPUUsageTemplate.json",
            WidgetPageState.Loading => @"DevHome\Templates\SystemGPUUsageTemplate.json",
            _ => throw new NotImplementedException(),
        };
    }

    public string GetItemTitle()
    {
        if (ContentData.TryGetValue("gpuName", out var name) && ContentData.TryGetValue("gpuUsage", out var usage))
        {
            return $"GPU ({name}): {usage}";
        }
        else
        {
            return "GPU Usage: ???";
        }
    }

    private string FloatToPercentString(float value)
    {
        return ((int)(value * 100)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    internal override void PushActivate()
    {
        base.PushActivate();
        if (IsActive)
        {
            _dataManager.Start();
        }
    }

    internal override void PopActivate()
    {
        base.PopActivate();
        if (!IsActive)
        {
            _dataManager.Stop();
        }
    }

    private void HandlePrevGPU(WidgetActionInvokedArgs args)
    {
        _gpuActiveIndex = _dataManager.GetGPUStats().GetPrevGPUIndex(_gpuActiveIndex);
        UpdateWidget();
    }

    private void HandleNextGPU(WidgetActionInvokedArgs args)
    {
        _gpuActiveIndex = _dataManager.GetGPUStats().GetNextGPUIndex(_gpuActiveIndex);
        UpdateWidget();
    }

    public void Dispose()
    {
        _dataManager.Dispose();
    }
}
