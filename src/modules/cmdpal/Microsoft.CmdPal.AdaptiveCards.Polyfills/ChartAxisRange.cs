// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

/// <summary>The value range of a chart axis and the spacing of its grid lines.</summary>
internal readonly record struct ChartAxisRange(double Min, double Max, double Step)
{
    /// <summary>Maps <paramref name="value"/> to 0 (minimum) through 1 (maximum), clamped.</summary>
    public double Normalize(double value) =>
        Max > Min ? Math.Clamp((value - Min) / (Max - Min), 0, 1) : 0;

    /// <summary>Returns the grid line values, from the minimum to the maximum.</summary>
    public IReadOnlyList<double> GetTicks()
    {
        var ticks = new List<double>();
        if (Step <= 0 || Max <= Min)
        {
            ticks.Add(Min);
            return ticks;
        }

        var first = Math.Ceiling((Min / Step) - 1e-9);
        var last = Math.Floor((Max / Step) + 1e-9);
        for (var i = first; i <= last && ticks.Count < 64; i++)
        {
            ticks.Add(Math.Round(i * Step, 10));
        }

        return ticks;
    }
}
