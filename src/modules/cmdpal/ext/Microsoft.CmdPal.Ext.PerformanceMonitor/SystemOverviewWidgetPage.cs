// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using CoreWidgetProvider.Helpers;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Timer = System.Timers.Timer;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

/// <summary>
/// Shows every metric at a glance: one tile per metric with its current value and the last
/// minute as a sparkline. Each tile shows the data of that metric's own page, so the values
/// match the detail cards, and those pages do the sampling.
/// </summary>
internal sealed partial class SystemOverviewWidgetPage : WidgetPage, IDisposable
{
    // The first update waits for the metric pages' first samples.
    private const double FirstUpdateDelayMilliseconds = 300;
    private const double UpdateIntervalMilliseconds = 1000;

    private readonly List<(string Key, WidgetPage Page)> _sources;
    private readonly Timer _timer = new(FirstUpdateDelayMilliseconds) { AutoReset = true };

    public SystemOverviewWidgetPage(
        SystemCPUUsageWidgetPage cpu,
        SystemMemoryUsageWidgetPage memory,
        SystemGPUUsageWidgetPage gpu,
        SystemDiskUsageWidgetPage disk,
        SystemNetworkUsageWidgetPage network,
        SystemBatteryUsageWidgetPage? battery)
    {
        _sources = [("cpu", cpu), ("memory", memory), ("gpu", gpu), ("disk", disk), ("network", network)];
        if (battery is not null)
        {
            _sources.Add(("battery", battery));
        }

        _timer.Elapsed += (_, _) =>
        {
            _timer.Interval = UpdateIntervalMilliseconds;
            UpdateWidget();
        };

        // Content pages can't navigate from inside a card, so the detail pages are commands.
        var commands = new List<CommandContextItem>();
        foreach (var (_, page) in _sources)
        {
            commands.Add(new CommandContextItem(page) { Title = page.Title });
        }

        commands.Add(new CommandContextItem(OpenTaskManagerCommand.Instance));
        Commands = [.. commands];
    }

    public override string Id => "com.microsoft.cmdpal.overview_widget";

    public override string Title => Resources.GetResource("Overview_Title");

    public override IconInfo Icon => Icons.PerformanceMonitorIcon;

    public override IContent[] GetContent()
    {
        // The metric pages usually have data already, so show it now instead of on the first tick.
        UpdateWidget();
        return base.GetContent();
    }

    protected override void LoadContentData()
    {
        ContentData.Clear();
        ContentJson.Clear();

        var hasBattery = false;
        var snapshots = new Dictionary<string, JsonObject>();
        foreach (var (key, page) in _sources)
        {
            var data = page.GetDataSnapshot();
            if (data.Count == 0)
            {
                // The page hasn't had its first sample yet; show its current values rather
                // than unbound template text.
                page.UpdateWidget();
                data = page.GetDataSnapshot();
            }

            snapshots[key] = data;
            ContentJson[key] = data;
            hasBattery |= key == "battery";
        }

        ContentJson["hasBattery"] = hasBattery;
        ContentJson["sparkMax"] = new JsonObject
        {
            ["cpu"] = GetSparklineMax(snapshots.GetValueOrDefault("cpu"), "cpuSeries"),
            ["gpu"] = GetSparklineMax(snapshots.GetValueOrDefault("gpu"), "gpuSeries"),
            ["disk"] = GetSparklineMax(snapshots.GetValueOrDefault("disk"), "diskActiveSeries"),
        };
    }

    /// <summary>Returns the top of a percentage sparkline, as <see cref="PerformanceChartData.GetSparklineMax"/> does.</summary>
    internal static double GetSparklineMax(JsonObject? data, string seriesKey)
    {
        var max = 0d;
        if (data?[seriesKey] is JsonArray series)
        {
            foreach (var item in series)
            {
                if (item?["values"] is not JsonArray values)
                {
                    continue;
                }

                foreach (var point in values)
                {
                    if (point?["y"] is JsonValue y && y.TryGetValue<double>(out var value))
                    {
                        max = Math.Max(max, value);
                    }
                }
            }
        }

        return PerformanceChartData.GetSparklineMax(max);
    }

    protected override void OnActivated()
    {
        foreach (var (_, page) in _sources)
        {
            page.PushActivate();
        }

        _timer.Interval = FirstUpdateDelayMilliseconds;
        _timer.Start();
    }

    protected override void OnDeactivated()
    {
        _timer.Stop();
        foreach (var (_, page) in _sources)
        {
            page.PopActivate();
        }
    }

    protected override string GetTemplatePath(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.Content => @"DevHome\Templates\SystemOverviewTemplate.json",
            WidgetPageState.Loading => @"DevHome\Templates\SystemOverviewTemplate.json",
            _ => throw new NotImplementedException(),
        };
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
