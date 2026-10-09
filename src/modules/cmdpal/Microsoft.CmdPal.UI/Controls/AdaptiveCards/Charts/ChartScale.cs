// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Computes readable axis ranges ("nice numbers").</summary>
internal static class ChartScale
{
    /// <summary>
    /// Computes the axis range: fixed ends are kept, and open ends round outward to a grid line. The
    /// axis starts at zero unless the minimum is fixed or the data is negative.
    /// </summary>
    public static ChartAxisRange Compute(
        double? fixedMin,
        double? fixedMax,
        double dataMin,
        double dataMax,
        int targetTickCount = 5)
    {
        var hasData = double.IsFinite(dataMin) && double.IsFinite(dataMax);
        var min = fixedMin ?? (hasData ? Math.Min(0, dataMin) : 0);
        var max = fixedMax ?? (hasData ? dataMax : 1);

        if (max <= min)
        {
            if (fixedMax is null)
            {
                max = min + (Math.Abs(min) > 0 ? Math.Abs(min) : 1);
            }
            else if (fixedMin is null)
            {
                min = max - (Math.Abs(max) > 0 ? Math.Abs(max) : 1);
            }
            else
            {
                (min, max) = max < min ? (max, min) : (min, min + 1);
            }
        }

        var intervals = Math.Max(1, targetTickCount - 1);
        var step = NiceNumber((max - min) / intervals, round: true);
        if (fixedMax is null)
        {
            max = Math.Ceiling((max / step) - 1e-9) * step;
        }

        if (fixedMin is null)
        {
            min = Math.Floor((min / step) + 1e-9) * step;
        }

        return new ChartAxisRange(min, max, step);
    }

    /// <summary>Returns a number near <paramref name="value"/> that's 1, 2, 2.5, or 5 times a power of ten.</summary>
    public static double NiceNumber(double value, bool round)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            return 1;
        }

        var exponent = Math.Floor(Math.Log10(value));
        var magnitude = Math.Pow(10, exponent);
        var fraction = value / magnitude;
        double nice;
        if (round)
        {
            nice = fraction < 1.5 ? 1 : fraction < 2.25 ? 2 : fraction < 3.5 ? 2.5 : fraction < 7.5 ? 5 : 10;
        }
        else
        {
            nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 2.5 ? 2.5 : fraction <= 5 ? 5 : 10;
        }

        return nice * magnitude;
    }
}
