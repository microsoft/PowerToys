// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Formats chart values in the user's language.</summary>
internal static partial class ChartValueFormatter
{
    private static readonly Lazy<CompactNumberFormats> LocalizedFormats = new(() => new CompactNumberFormats(
        Load("AdaptiveChart_CompactThousands", CompactNumberFormats.English.Thousands),
        Load("AdaptiveChart_CompactMillions", CompactNumberFormats.English.Millions),
        Load("AdaptiveChart_CompactBillions", CompactNumberFormats.English.Billions),
        Load("AdaptiveChart_CompactTrillions", CompactNumberFormats.English.Trillions)));

    /// <summary>Formats a value in the user's language, shortening large numbers such as 12,345 to 12.3K.</summary>
    public static string FormatCompact(double value) =>
        FormatCompact(value, CultureInfo.CurrentCulture, LocalizedFormats.Value);

    // A missing or broken translation falls back to English rather than losing the number.
    private static CompositeFormat Load(string resourceId, CompositeFormat english)
    {
        var format = RS_.GetString(resourceId);
        if (string.IsNullOrWhiteSpace(format) || !format.Contains("{0}", StringComparison.Ordinal))
        {
            return english;
        }

        try
        {
            return CompositeFormat.Parse(format);
        }
        catch (FormatException)
        {
            return english;
        }
    }
}
