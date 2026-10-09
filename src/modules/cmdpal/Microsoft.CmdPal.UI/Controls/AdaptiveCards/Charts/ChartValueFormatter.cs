// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Formats chart values for axis labels, tooltips, and accessible summaries.</summary>
internal static partial class ChartValueFormatter
{
    /// <summary>Formats a value, shortening numbers from 10,000 up with <paramref name="formats"/>.</summary>
    public static string FormatCompact(double value, IFormatProvider culture, CompactNumberFormats formats)
    {
        var magnitude = Math.Abs(value);
        return magnitude switch
        {
            >= 1e12 => Shorten(culture, formats.Trillions, value / 1e12),
            >= 1e9 => Shorten(culture, formats.Billions, value / 1e9),
            >= 1e6 => Shorten(culture, formats.Millions, value / 1e6),
            >= 1e4 => Shorten(culture, formats.Thousands, value / 1e3),
            _ => value.ToString(magnitude >= 100 || value == Math.Floor(value) ? "N0" : "0.##", culture),
        };
    }

    private static string Shorten(IFormatProvider culture, CompositeFormat format, double scaled) =>
        string.Format(culture, format, scaled.ToString("0.#", culture));
}
