// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace SamplePagesExtension;

/// <summary>The Adaptive Card for <see cref="SampleChartsPage"/>.</summary>
internal static class SampleChartsTemplate
{
    internal const string Json = """
    {
      "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
      "type": "AdaptiveCard",
      "version": "1.6",
      "body": [
        {
          "type": "ColumnSet",
          "columns": [
            {
              "type": "Column",
              "width": "stretch",
              "items": [
                {
                  "type": "TextBlock",
                  "text": "Charts and visuals",
                  "size": "large",
                  "weight": "bolder"
                },
                {
                  "type": "TextBlock",
                  "text": "Standard Adaptive Cards elements that Command Palette renders natively. Each one has a fallback for hosts that don't support it.",
                  "isSubtle": true,
                  "wrap": true,
                  "spacing": "none"
                }
              ]
            },
            {
              "type": "Column",
              "width": "auto",
              "verticalContentAlignment": "center",
              "items": [
                {
                  "type": "Badge",
                  "text": "Live",
                  "style": "good",
                  "appearance": "tint",
                  "shape": "circular",
                  "fallback": "drop"
                }
              ]
            }
          ]
        },
        {
          "type": "ColumnSet",
          "spacing": "medium",
          "columns": [
            {
              "type": "Column",
              "width": 2,
              "items": [
                {
                  "type": "Container",
                  "style": "emphasis",
                  "roundedCorners": true,
                  "showBorder": true,
                  "height": "stretch",
                  "items": [
                    {
                      "type": "Chart.Line",
                      "title": "Requests per second",
                      "xAxisTitle": "Last 30 seconds",
                      "yAxisTitle": "Requests",
                      "yMin": 0,
                      "fill": "gradient",
                      "showLegend": true,
                      "data": "${traffic}",
                      "fallback": {
                        "type": "TextBlock",
                        "text": "This host can't show charts.",
                        "isSubtle": true
                      }
                    }
                  ]
                }
              ]
            },
            {
              "type": "Column",
              "width": 1,
              "items": [
                {
                  "type": "Container",
                  "style": "emphasis",
                  "roundedCorners": true,
                  "showBorder": true,
                  "height": "stretch",
                  "items": [
                    {
                      "type": "Chart.Gauge",
                      "title": "Server load",
                      "subLabel": "Load",
                      "value": "${load}",
                      "min": 0,
                      "max": 100,
                      "showMinMax": true,
                      "segments": [
                        { "legend": "Normal", "size": 60, "color": "good" },
                        { "legend": "Busy", "size": 25, "color": "warning" },
                        { "legend": "Overloaded", "size": 15, "color": "attention" }
                      ],
                      "fallback": "drop"
                    },
                    {
                      "type": "ProgressBar",
                      "spacing": "medium",
                      "value": "${load}",
                      "color": "${loadColor}",
                      "fallback": "drop"
                    }
                  ]
                }
              ]
            }
          ]
        },
        {
          "type": "ColumnSet",
          "spacing": "small",
          "columns": [
            {
              "type": "Column",
              "width": "stretch",
              "items": [
                {
                  "type": "Container",
                  "style": "emphasis",
                  "roundedCorners": true,
                  "showBorder": true,
                  "height": "stretch",
                  "items": [
                    {
                      "type": "Chart.Donut",
                      "title": "Storage",
                      "value": "512 GB",
                      "data": [
                        { "legend": "Apps", "value": 182 },
                        { "legend": "Documents", "value": 96 },
                        { "legend": "Photos", "value": 140 },
                        { "legend": "Free", "value": 94, "color": "neutral" }
                      ],
                      "fallback": "drop"
                    }
                  ]
                }
              ]
            },
            {
              "type": "Column",
              "width": "stretch",
              "items": [
                {
                  "type": "Container",
                  "style": "emphasis",
                  "roundedCorners": true,
                  "showBorder": true,
                  "height": "stretch",
                  "items": [
                    {
                      "type": "Chart.Pie",
                      "title": "Time by app",
                      "colorSet": "categorical",
                      "data": [
                        { "legend": "Editor", "value": 48 },
                        { "legend": "Browser", "value": 31 },
                        { "legend": "Terminal", "value": 14 },
                        { "legend": "Other", "value": 7 }
                      ],
                      "fallback": "drop"
                    }
                  ]
                }
              ]
            }
          ]
        },
        {
          "type": "ColumnSet",
          "spacing": "small",
          "columns": [
            {
              "type": "Column",
              "width": "stretch",
              "items": [
                {
                  "type": "Container",
                  "style": "emphasis",
                  "roundedCorners": true,
                  "showBorder": true,
                  "height": "stretch",
                  "items": [
                    {
                      "type": "Chart.VerticalBar",
                      "title": "Builds per day",
                      "showBarValues": true,
                      "color": "categoricalTeal",
                      "data": [
                        { "x": "Mon", "y": 12 },
                        { "x": "Tue", "y": 18 },
                        { "x": "Wed", "y": 9 },
                        { "x": "Thu", "y": 21 },
                        { "x": "Fri", "y": 15 }
                      ],
                      "fallback": "drop"
                    }
                  ]
                }
              ]
            },
            {
              "type": "Column",
              "width": "stretch",
              "items": [
                {
                  "type": "Container",
                  "style": "emphasis",
                  "roundedCorners": true,
                  "showBorder": true,
                  "height": "stretch",
                  "items": [
                    {
                      "type": "Chart.HorizontalBar",
                      "title": "Busiest processes",
                      "valueFormat": "percentage",
                      "showBarValues": true,
                      "color": "categoricalPurple",
                      "data": [
                        { "x": "Browser", "y": 34 },
                        { "x": "Compiler", "y": 22 },
                        { "x": "Teams", "y": 9 },
                        { "x": "Explorer", "y": 4 }
                      ],
                      "fallback": "drop"
                    }
                  ]
                }
              ]
            }
          ]
        },
        {
          "type": "Container",
          "spacing": "small",
          "style": "emphasis",
          "roundedCorners": true,
          "showBorder": true,
          "items": [
            {
              "type": "Chart.HorizontalBar.Stacked",
              "title": "Memory by device",
              "data": [
                {
                  "title": "Desktop",
                  "data": [
                    { "legend": "In use", "value": 18, "color": "categoricalPurple" },
                    { "legend": "Cached", "value": 9, "color": "categoricalLightBlue" },
                    { "legend": "Free", "value": 5, "color": "neutral" }
                  ]
                },
                {
                  "title": "Laptop",
                  "data": [
                    { "legend": "In use", "value": 11, "color": "categoricalPurple" },
                    { "legend": "Cached", "value": 3, "color": "categoricalLightBlue" },
                    { "legend": "Free", "value": 2, "color": "neutral" }
                  ]
                }
              ],
              "fallback": "drop"
            }
          ]
        },
        {
          "type": "ColumnSet",
          "spacing": "medium",
          "columns": [
            {
              "type": "Column",
              "width": "stretch",
              "items": [
                { "type": "TextBlock", "text": "Accent", "size": "small", "isSubtle": true },
                { "type": "ProgressBar", "value": 72, "fallback": "drop" }
              ]
            },
            {
              "type": "Column",
              "width": "stretch",
              "items": [
                { "type": "TextBlock", "text": "Good", "size": "small", "isSubtle": true },
                { "type": "ProgressBar", "value": 45, "color": "good", "fallback": "drop" }
              ]
            },
            {
              "type": "Column",
              "width": "stretch",
              "items": [
                { "type": "TextBlock", "text": "Warning", "size": "small", "isSubtle": true },
                { "type": "ProgressBar", "value": 88, "color": "warning", "fallback": "drop" }
              ]
            },
            {
              "type": "Column",
              "width": "stretch",
              "items": [
                { "type": "TextBlock", "text": "Indeterminate", "size": "small", "isSubtle": true },
                { "type": "ProgressBar", "fallback": "drop" }
              ]
            }
          ]
        },
        {
          "type": "ColumnSet",
          "spacing": "medium",
          "columns": [
            { "type": "Column", "width": "auto", "items": [ { "type": "Badge", "text": "Default", "fallback": "drop" } ] },
            { "type": "Column", "width": "auto", "items": [ { "type": "Badge", "text": "Accent", "style": "accent", "icon": "Info", "fallback": "drop" } ] },
            { "type": "Column", "width": "auto", "items": [ { "type": "Badge", "text": "Good", "style": "good", "appearance": "tint", "icon": "CheckmarkCircle,filled", "fallback": "drop" } ] },
            { "type": "Column", "width": "auto", "items": [ { "type": "Badge", "text": "Warning", "style": "warning", "appearance": "tint", "icon": "Warning", "fallback": "drop" } ] },
            { "type": "Column", "width": "auto", "items": [ { "type": "Badge", "text": "Attention", "style": "attention", "shape": "circular", "fallback": "drop" } ] },
            { "type": "Column", "width": "auto", "items": [ { "type": "Badge", "text": "Large", "size": "large", "appearance": "tint", "icon": "Wifi1", "iconPosition": "After", "fallback": "drop" } ] }
          ]
        },
        {
          "type": "ColumnSet",
          "spacing": "medium",
          "columns": [
            { "type": "Column", "width": "auto", "verticalContentAlignment": "center", "items": [ { "type": "Icon", "name": "DeveloperBoard", "size": "Small", "fallback": "drop" } ] },
            { "type": "Column", "width": "auto", "verticalContentAlignment": "center", "items": [ { "type": "Icon", "name": "Ram", "color": "Accent", "fallback": "drop" } ] },
            { "type": "Column", "width": "auto", "verticalContentAlignment": "center", "items": [ { "type": "Icon", "name": "HardDrive", "size": "Medium", "color": "Good", "fallback": "drop" } ] },
            { "type": "Column", "width": "auto", "verticalContentAlignment": "center", "items": [ { "type": "Icon", "name": "Wifi1", "size": "Large", "color": "Warning", "fallback": "drop" } ] },
            { "type": "Column", "width": "auto", "verticalContentAlignment": "center", "items": [ { "type": "Icon", "name": "BatteryCharge", "size": "xLarge", "color": "Attention", "fallback": "drop" } ] },
            { "type": "Column", "width": "auto", "verticalContentAlignment": "center", "items": [ { "type": "Icon", "name": "Calendar", "size": "xxLarge", "style": "Filled", "fallback": "drop" } ] }
          ]
        }
      ]
    }
    """;
}
