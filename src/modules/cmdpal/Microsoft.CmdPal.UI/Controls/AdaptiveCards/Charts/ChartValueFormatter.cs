// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Formats chart values for axis labels and accessible summaries.</summary>
internal static class ChartValueFormatter
{
    /// <summary>Formats large numbers with a K, M, B, or T suffix so labels stay short.</summary>
    public static string FormatCompact(double value, IFormatProvider culture)
    {
        var magnitude = Math.Abs(value);
        return magnitude switch
        {
            >= 1e12 => string.Format(culture, "{0:0.#}T", value / 1e12),
            >= 1e9 => string.Format(culture, "{0:0.#}B", value / 1e9),
            >= 1e6 => string.Format(culture, "{0:0.#}M", value / 1e6),
            >= 1e4 => string.Format(culture, "{0:0.#}K", value / 1e3),
            _ => value.ToString(magnitude >= 100 || value == Math.Floor(value) ? "N0" : "0.##", culture),
        };
    }
}
