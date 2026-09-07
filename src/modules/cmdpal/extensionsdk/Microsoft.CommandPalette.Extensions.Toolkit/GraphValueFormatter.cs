// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Formats graph measurements and matching extension readouts using caller-defined scales.
/// </summary>
public static class GraphValueFormatter
{
    /// <summary>
    /// Applies the largest divisor not exceeding the absolute value, or the first
    /// scale for smaller values. Scales must have positive, finite, increasing
    /// divisors and non-null suffixes. Empty scales use the value and suffix directly.
    /// </summary>
    public static string Format(double value, string valueFormat = "0.0", string valueSuffix = "", ReadOnlySpan<GraphValueScale> valueScales = default)
    {
        if (!valueScales.IsEmpty)
        {
            var index = 0;
            while (index < valueScales.Length - 1 && Math.Abs(value) >= valueScales[index + 1].Divisor)
            {
                index++;
            }

            value /= valueScales[index].Divisor;
            valueSuffix = valueScales[index].Suffix;
        }

        Span<char> buffer = stackalloc char[128];
        try
        {
            if (value.TryFormat(buffer, out var written, valueFormat, CultureInfo.CurrentCulture))
            {
                return string.Concat(buffer[..written], valueSuffix);
            }
        }
        catch (FormatException)
        {
        }

        return value.ToString("0.0", CultureInfo.CurrentCulture) + valueSuffix;
    }
}
