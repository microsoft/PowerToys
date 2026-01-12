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
        _cpuPage.PushActivate();
    }

    protected override void Unloaded()
    {
        _cpuPage.PopActivate();
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
