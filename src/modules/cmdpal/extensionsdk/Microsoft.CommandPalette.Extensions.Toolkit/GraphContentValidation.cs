// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

internal static class GraphContentValidation
{
    internal static GraphValueScale[] CopyValueScales(GraphValueScale[] scales)
    {
        ArgumentNullException.ThrowIfNull(scales);
        GraphValueScale[] copy = [.. scales];
        var previousDivisor = 0d;
        foreach (var scale in copy)
        {
            if (scale is null || !double.IsFinite(scale.Divisor) || scale.Divisor <= previousDivisor || scale.Suffix is null)
            {
                throw new ArgumentException("Value scales require positive, finite, increasing divisors and non-null suffixes.", nameof(scales));
            }

            previousDivisor = scale.Divisor;
        }

        return copy;
    }

    internal static GraphSeriesInfo[] CopySeries(GraphSeriesInfo[] series)
    {
        ArgumentNullException.ThrowIfNull(series);
        GraphSeriesInfo[] copy = [.. series];
        foreach (var item in copy)
        {
            if (item is null || item.Name is null)
            {
                throw new ArgumentException("Series names must not be null.", nameof(series));
            }

            if (item.LineStyle is not (GraphLineStyle.Solid or GraphLineStyle.Dashed or GraphLineStyle.Dotted))
            {
                throw new ArgumentException("Series line styles must be solid, dashed, or dotted.", nameof(series));
            }
        }

        return copy;
    }

    internal static void ValidateRange(double minimum, double maximum)
    {
        if (!double.IsFinite(minimum))
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), "Minimum must be finite.");
        }

        if (!double.IsFinite(maximum) || maximum <= minimum || !double.IsFinite(maximum - minimum))
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum must be finite and define a positive, finite range.");
        }
    }

    internal static double[] CopyValues(double[] values, int seriesCount)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length != seriesCount)
        {
            throw new ArgumentException("Provide one value for each configured series.", nameof(values));
        }

        double[] copy = [.. values];
        foreach (var value in copy)
        {
            if (!double.IsFinite(value) || value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(values), "Contributions must be finite and non-negative.");
            }
        }

        return copy;
    }
}
