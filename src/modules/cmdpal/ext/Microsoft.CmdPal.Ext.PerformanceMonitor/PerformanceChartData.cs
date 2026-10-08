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
}
