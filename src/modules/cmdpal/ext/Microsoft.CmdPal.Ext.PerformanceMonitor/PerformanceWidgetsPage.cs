// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CoreWidgetProvider.Helpers;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
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
internal sealed partial class PerformanceWidgetsPage : OnLoadStaticPage, IDisposable
{
    private int _loadCount;

    private bool IsActive => _loadCount > 0;

    public override string Id => "com.microsoft.cmdpal.performanceWidget";

    public override string Title => "Performance monitor";

    // public override string PlaceholderText => "Performance monitor";
    public override IconInfo Icon => Icons.StackedAreaIcon;

    // private Task? _updateTask;
    private readonly bool _isBandPage;

    private readonly SystemCPUUsageWidgetPage _cpuPage = new();
    private readonly ListItem _cpuItem;

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
    }

    protected override void Loaded()
    {
        _loadCount++;

        _cpuPage.Activate();

        // _updateTask ??= Task.Run(() =>
        // {
        //    UpdateValues();
        // });
    }

    protected override void Unloaded()
    {
        _loadCount--;

        _cpuPage.Deactivate();

        // TODO! cancel the update task
    }

    private async void UpdateValues()
    {
        // Update interval in milliseconds
        const int updateInterval = 1000;

        // TODO: Fix this behaviour which is needed cause of a bug
        while (_loadCount > 0)
        {
            // Record start time of update cycle
            var startTime = DateTime.Now;

            var tasks = new List<Task>();

            // // Start all update tasks in parallel
            // if (_cpuItem != null)
            // {
            //     tasks.Add(Task.Run(() => UpdateCpuValues()));
            // }

            // if (_memoryItem != null)
            // {
            //     tasks.Add(Task.Run(() => UpdateMemoryValues()));
            // }

            // if (_diskCounters?.Length > 0 && _diskItem != null)
            // {
            //     tasks.Add(Task.Run(() => UpdateDiskValues()));
            // }

            // if (_networkItem != null)
            // {
            //     tasks.Add(Task.Run(() => UpdateNetworkValues()));
            // }

            // if (!_isBandPage)
            // {
            //     // TODO!: This is unbelievably loud
            //     tasks.Add(GetProcessInfo());
            // }

            // // Wait for all tasks to complete
            // await Task.WhenAll(tasks);

            // Calculate how much time has passed
            var elapsedTime = (DateTime.Now - startTime).TotalMilliseconds;

            // If we completed faster than our desired interval, wait the remaining time
            if (elapsedTime < updateInterval)
            {
                await Task.Delay((int)(updateInterval - elapsedTime));
            }
        }
    }

    public override IListItem[] GetItems()
    {
        if (!_isBandPage)
        {
            // TODO! add details
        }

        return new[] { _cpuItem };
    }

    public void Dispose()
    {
        _cpuPage.Dispose();
    }
}

internal abstract partial class WidgetPage : ContentPage
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
                json[kvp.Key] = kvp.Value;
            }

            return json;
        }
    }

    public void UpdateWidget()
    {
        LoadContentData();

        Updated?.Invoke(this, EventArgs.Empty);
    }

    protected abstract void LoadContentData();

    protected abstract string GetTemplatePath(WidgetPageState page);

    protected string GetTemplateForPage(WidgetPageState page)
    {
        if (Template.TryGetValue(page, out var value))
        {
            // Log.Debug($"Using cached template for {page}");
            return value;
        }

        try
        {
            var path = Path.Combine(Package.Current.EffectivePath, GetTemplatePath(page));
            var template = File.ReadAllText(path, Encoding.Default) ?? throw new FileNotFoundException(path);

            // template = Resources.ReplaceIdentifers(template, Resources.GetWidgetResourceIdentifiers(), Log);

            // Log.Debug($"Caching template for {page}");
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
        FormContent f = new();
        f.TemplateJson = GetTemplateForPage(WidgetPageState.Content);
        f.DataJson = ContentDataJson.ToJsonString();

        return [f];
    }

    internal abstract void Activate();

    internal abstract void Deactivate();
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
        // Log.Debug("Getting CPU stats");
        try
        {
            // var cpuData = new JsonObject();
            ContentData.Clear();
            var currentData = _dataManager.GetCPUStats();

            ContentData["cpuUsage"] = FloatToPercentString(currentData.CpuUsage);
            ContentData["cpuSpeed"] = SpeedToString(currentData.CpuSpeed);
            ContentData["cpuGraphUrl"] = currentData.CreateCPUImageUrl();
            ContentData["chartHeight"] = ChartHelper.ChartHeight + "px";
            ContentData["chartWidth"] = ChartHelper.ChartWidth + "px";

            ContentData["cpuProc1"] = currentData.GetCpuProcessText(0);
            ContentData["cpuProc2"] = currentData.GetCpuProcessText(1);
            ContentData["cpuProc3"] = currentData.GetCpuProcessText(2);

            // DataState = WidgetDataState.Okay;
            // ContentData = cpuData.ToJsonString();
        }
        catch (Exception e)
        {
            // Log.Error(e, "Error retrieving stats.");

            // var content = new JsonObject
            // {
            //     { "errorMessage", e.Message },
            // };
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

    internal override void Activate()
    {
        _dataManager.Start();
    }

    internal override void Deactivate()
    {
        _dataManager.Stop();
    }

    public void Dispose()
    {
        _dataManager.Dispose();
    }
}
