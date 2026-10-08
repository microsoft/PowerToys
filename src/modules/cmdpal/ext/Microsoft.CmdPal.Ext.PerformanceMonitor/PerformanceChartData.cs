// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor;

/// <summary>Builds the <c>data</c> array of an Adaptive Cards <c>Chart.Line</c> element.</summary>
internal static class PerformanceChartData
{
    /// <summary>The number of one-second samples each history chart shows.</summary>
    public const int HistoryLength = 60;

    // Each metric has its own color, as in Task Manager. Charts of a flow use one pair of
    // colors on every card: teal for data coming in (read, receive) and marigold for data
    // going out (write, send).
    public const string CpuColor = "categoricalBlue";
    public const string MemoryColor = "categoricalPurple";
    public const string DiskColor = "categoricalLime";
    public const string GpuColor = "categoricalGreen";
    public const string InColor = "categoricalTeal";
    public const string OutColor = "categoricalMarigold";

    /// <summary>A series to plot: its legend, color name, and samples (oldest first).</summary>
    internal readonly record struct Series(string Legend, string Color, IReadOnlyList<float> Values);

    /// <summary>
    /// Creates the series array. Each series is padded at the start with empty samples, so the
    /// chart keeps a fixed time window and fills in from the right.
    /// </summary>
    public static JsonArray Create(params Series[] series)
    {
        var data = new JsonArray();
        foreach (var item in series)
        {
            var values = new JsonArray();
            var count = Math.Min(item.Values.Count, HistoryLength);
            for (var i = count; i < HistoryLength; i++)
            {
                values.Add((JsonNode)new JsonObject { ["y"] = null });
            }

            for (var i = item.Values.Count - count; i < item.Values.Count; i++)
            {
                values.Add((JsonNode)new JsonObject { ["y"] = Math.Round((double)item.Values[i], 1, MidpointRounding.AwayFromZero) });
            }

            data.Add((JsonNode)new JsonObject
            {
                ["legend"] = item.Legend,
                ["color"] = item.Color,
                ["values"] = values,
            });
        }

        return data;
    }

    /// <summary>Copies samples under the owner's lock, so the timer thread can keep appending.</summary>
    public static float[] Snapshot(List<float> values)
    {
        lock (values)
        {
            return values.ToArray();
        }
    }

    /// <summary>
    /// Picks one unit for a whole rate chart, with the same unit names as the value labels, so
    /// the axis reads naturally. Divide bytes per second by <c>Divisor</c> to get the unit.
    /// </summary>
    public static (float Divisor, string Unit) GetRateScale(SpeedUnit speedUnit, float maxBytesPerSecond)
    {
        var (multiplier, step, units) = speedUnit switch
        {
            SpeedUnit.BytesPerSecond => (1f, 1000f, BytesUnits),
            SpeedUnit.BinaryBytesPerSecond => (1f, 1024f, BinaryBytesUnits),
            _ => (8f, 1024f, BitsUnits),
        };

        var value = maxBytesPerSecond * multiplier / step;
        var divisor = step;
        var index = 0;
        while (index < units.Length - 1 && value >= step)
        {
            value /= step;
            divisor *= step;
            index++;
        }

        return (divisor / multiplier, units[index]);
    }

    public static float[] Scale(float[] values, float divisor)
    {
        var scaled = new float[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            scaled[i] = values[i] / divisor;
        }

        return scaled;
    }

    public static float Max(params float[][] series)
    {
        var max = 0f;
        foreach (var values in series)
        {
            foreach (var value in values)
            {
                max = Math.Max(max, value);
            }
        }

        return max;
    }

    private static readonly string[] BitsUnits = ["Kbps", "Mbps", "Gbps"];
    private static readonly string[] BytesUnits = ["KB/s", "MB/s", "GB/s"];
    private static readonly string[] BinaryBytesUnits = ["KiB/s", "MiB/s", "GiB/s"];
}
