// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

/// <summary>
/// Pure formatting and metric-selection helpers used by the compact
/// <see cref="PerformanceOverviewPage"/> dashboard. These are intentionally
/// free of I/O, WinRT, and UI state so they can be unit tested directly.
/// </summary>
internal static class PerformanceOverviewFormatter
{
    /// <summary>
    /// Parses a formatted percentage string (e.g. "42%", as produced by
    /// <see cref="WidgetPage.FloatToPercentString(float)"/>) back into a
    /// clamped 0-100 integer. Returns 0 for null, empty, or unparsable input
    /// so a missing or errored metric renders as an empty bar instead of
    /// throwing.
    /// </summary>
    internal static int ParsePercentText(string? percentText)
    {
        if (string.IsNullOrWhiteSpace(percentText))
        {
            return 0;
        }

        var trimmed = percentText.Trim().TrimEnd('%').Trim();

        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? ClampPercent(value)
            : 0;
    }

    /// <summary>Clamps a percentage to the valid 0-100 range.</summary>
    internal static int ClampPercent(int percent) => Math.Clamp(percent, 0, 100);

    internal static string GetMetricKey(PerformanceMetricKind metric) => metric switch
    {
        PerformanceMetricKind.Cpu => "cpu",
        PerformanceMetricKind.Memory => "memory",
        PerformanceMetricKind.Network => "network",
        PerformanceMetricKind.Disk => "disk",
        PerformanceMetricKind.Gpu => "gpu",
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "The metric is not available in the performance overview."),
    };

    internal static string SelectMetricValue(
        PerformanceMetricKind metric,
        string cpuValue,
        string memoryValue,
        string networkValue,
        string diskValue,
        string gpuValue)
    {
        return metric switch
        {
            PerformanceMetricKind.Cpu => cpuValue,
            PerformanceMetricKind.Memory => memoryValue,
            PerformanceMetricKind.Network => networkValue,
            PerformanceMetricKind.Disk => diskValue,
            PerformanceMetricKind.Gpu => gpuValue,
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "The metric is not available in the performance overview."),
        };
    }
}
