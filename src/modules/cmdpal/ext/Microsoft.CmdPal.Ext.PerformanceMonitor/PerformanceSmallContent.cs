// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

internal sealed partial class PerformanceSmallContent : IFormContent
{
    private readonly WidgetPage[] _widgets;
    private readonly Lock _subscriptionLock = new();
    private TypedEventHandler<object, IPropChangedEventArgs>? _propChanged;

    public PerformanceSmallContent(WidgetPage widget, Size availableSpace)
        : this([widget], availableSpace)
    {
    }

    public PerformanceSmallContent(WidgetPage[] widgets, Size availableSpace)
    {
        ArgumentNullException.ThrowIfNull(widgets);
        if (widgets.Length == 0)
        {
            throw new ArgumentException("Small content requires at least one widget.", nameof(widgets));
        }

        _widgets = [.. widgets];
        TemplateJson = BuildTemplate(widgets.Length, availableSpace);
    }

    public string DataJson => new JsonObject
    {
        ["widgets"] = new JsonArray(_widgets.Select(widget => (JsonNode)widget.GetSmallContentData()).ToArray()),
    }.ToJsonString();

    public string StateJson => "{}";

    public string TemplateJson { get; }

    internal static JsonObject CreateData(PerformanceMetricKind metric, string title, JsonObject snapshot)
    {
        var (valueKey, detailKey, seriesKey) = metric switch
        {
            PerformanceMetricKind.Cpu => ("cpuUsage", "cpuSpeed", "cpuSeries"),
            PerformanceMetricKind.Memory => ("memUsage", "memUsedOfTotal", "memSeries"),
            PerformanceMetricKind.Network or PerformanceMetricKind.NetworkSpeed => ("netReceived", "netSent", "netSeries"),
            PerformanceMetricKind.Disk => ("diskUsage", "diskName", "diskActiveSeries"),
            PerformanceMetricKind.Gpu => ("gpuUsage", "gpuShortName", "gpuSeries"),
            PerformanceMetricKind.Battery => ("batteryCharge", "batteryStatus", string.Empty),
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unsupported performance metric."),
        };
        var series = snapshot[seriesKey] as JsonArray;
        var hasChart = series?.Any(item => item?["values"] is JsonArray { Count: > 0 }) == true;
        var chartMax = metric switch
        {
            PerformanceMetricKind.Memory => 100,
            PerformanceMetricKind.Network or PerformanceMetricKind.NetworkSpeed => Math.Max(1, GetSeriesMax(series)),
            _ => PerformanceChartData.GetSparklineMax(GetSeriesMax(series)),
        };
        var detail = snapshot[detailKey]?.GetValue<string>() ?? string.Empty;
        if (metric is PerformanceMetricKind.Network or PerformanceMetricKind.NetworkSpeed && detail.Length > 0)
        {
            detail = $"{Resources.GetResource("Network_Send_Subtitle")}: {detail}";
        }

        return new JsonObject
        {
            ["title"] = title,
            ["value"] = snapshot[valueKey]?.GetValue<string>() ?? string.Empty,
            ["detail"] = detail,
            ["series"] = series?.DeepClone() ?? new JsonArray(),
            ["hasChart"] = hasChart,
            ["chartMax"] = chartMax,
            ["isBattery"] = metric == PerformanceMetricKind.Battery,
            ["batteryPercent"] = snapshot["batteryPercent"]?.DeepClone() ?? JsonValue.Create(0),
            ["batteryColor"] = snapshot["batteryColor"]?.DeepClone() ?? JsonValue.Create("accent"),
            ["error"] = snapshot["errorMessage"]?.GetValue<string>() ?? string.Empty,
        };
    }

    private static double GetSeriesMax(JsonArray? series) =>
        series?.SelectMany(item => (item?["values"] as JsonArray)?.AsEnumerable() ?? [])
            .Select(point => point?["y"]?.GetValue<double>() ?? 0).DefaultIfEmpty().Max() ?? 0;

    private static string BuildTemplate(int count, Size availableSpace)
    {
        var body = new JsonArray();
        var columnsPerRow = count > 2 ? 3 : count;
        for (var first = 0; first < count; first += columnsPerRow)
        {
            var columns = new JsonArray();
            for (var index = first; index < Math.Min(first + columnsPerRow, count); index++)
            {
                var path = $"widgets[{index}]";
                var items = JsonNode.Parse($$"""
                    [
                      { "type": "TextBlock", "text": "${{{path}}.title}", "size": "small", "weight": "bolder" },
                      { "type": "TextBlock", "text": "${{{path}}.value}", "size": "{{(count == 1 ? "large" : "small")}}",
                        "weight": "bolder", "spacing": "none", "maxLines": 1 },
                      { "type": "TextBlock", "text": "${{{path}}.error}", "$when": "${{{path}}.error != ''}",
                        "size": "small", "color": "attention", "spacing": "none", "maxLines": 1 }
                    ]
                    """)!.AsArray();
                if (count == 1)
                {
                    items.Add(JsonNode.Parse($$"""
                        { "type": "TextBlock", "text": "${{{path}}.detail}", "size": "small",
                          "isSubtle": true, "spacing": "none", "maxLines": 1 }
                        """));
                }

                if (count <= 2)
                {
                    var chart = JsonNode.Parse($$"""
                        [
                          { "type": "Chart.Line", "id": "sparkline{{index}}", "$when": "${{{path}}.hasChart}",
                            "spacing": "none", "title": "${{{path}}.title}", "showTitle": false, "showLegend": false,
                            "yMin": 0, "yMax": "${{{path}}.chartMax}", "data": "${{{path}}.series}", "fallback": "drop" },
                          { "type": "ProgressBar", "$when": "${{{path}}.isBattery}", "spacing": "none",
                            "value": "${{{path}}.batteryPercent}", "color": "${{{path}}.batteryColor}", "fallback": "drop" }
                        ]
                        """)!.AsArray();
                    if (count == 1)
                    {
                        columns.Add((JsonNode)new JsonObject { ["type"] = "Column", ["width"] = "stretch", ["items"] = items });
                        columns.Add((JsonNode)new JsonObject
                        {
                            ["type"] = "Column",
                            ["width"] = Math.Clamp((availableSpace.Width - 16) / 2, 60, 160).ToString(CultureInfo.InvariantCulture),
                            ["verticalContentAlignment"] = "center",
                            ["items"] = chart,
                        });
                        continue;
                    }

                    foreach (var element in chart)
                    {
                        items.Add(element!.DeepClone());
                    }
                }

                columns.Add((JsonNode)new JsonObject { ["type"] = "Column", ["width"] = "stretch", ["items"] = items });
            }

            body.Add((JsonNode)new JsonObject { ["type"] = "ColumnSet", ["spacing"] = "none", ["columns"] = columns });
        }

        return new JsonObject
        {
            ["$schema"] = "https://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.5",
            ["body"] = body,
        }.ToJsonString();
    }

    public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
    {
        add
        {
            lock (_subscriptionLock)
            {
                var activate = _propChanged is null;
                _propChanged += value;
                if (activate)
                {
                    foreach (var widget in _widgets)
                    {
                        widget.Updated += OnUpdated;
                        widget.PushActivate();
                    }
                }
            }
        }

        remove
        {
            lock (_subscriptionLock)
            {
                var wasActive = _propChanged is not null;
                _propChanged -= value;
                if (wasActive && _propChanged is null)
                {
                    foreach (var widget in _widgets)
                    {
                        widget.Updated -= OnUpdated;
                        widget.PopActivate();
                    }
                }
            }
        }
    }

    public ICommandResult SubmitForm(string inputs, string data) => CommandResult.KeepOpen();

    private void OnUpdated(object? sender, EventArgs args)
    {
        TypedEventHandler<object, IPropChangedEventArgs>? handlers;
        lock (_subscriptionLock)
        {
            handlers = _propChanged;
        }

        EventHelpers.Raise(handlers, this, new PropChangedEventArgs(nameof(DataJson)), handler => PropChanged -= handler);
    }
}
