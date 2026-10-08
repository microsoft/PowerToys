// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>
/// Resolves the Adaptive Cards chart color names (<c>categoricalBlue</c>, <c>good</c>,
/// <c>sequential1</c>, and so on) and color sets to theme-aware colors.
/// </summary>
internal static class ChartPalette
{
    public const string AccentColorName = "accent";

    // Each entry is (light theme, dark theme). Semantic colors follow the Fluent system fill
    // colors. The other colors are tuned to keep contrast on both Mica and Acrylic surfaces.
    private static readonly Dictionary<string, (uint Light, uint Dark)> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["good"] = (0x0F7B0F, 0x6CCB5F),
        ["warning"] = (0x9D5D00, 0xFCE100),
        ["attention"] = (0xC42B1C, 0xFF99A4),
        ["neutral"] = (0x8A8A8A, 0x9E9E9E),

        ["categoricalRed"] = (0xD13438, 0xFF6B6B),
        ["categoricalPurple"] = (0x8764B8, 0xB48CF2),
        ["categoricalLavender"] = (0x6B69D6, 0xA8A6FF),
        ["categoricalBlue"] = (0x2F6FE4, 0x6E9BFF),
        ["categoricalLightBlue"] = (0x0F86C4, 0x5CC8FF),
        ["categoricalTeal"] = (0x038387, 0x3DD6C6),
        ["categoricalGreen"] = (0x107C41, 0x4CD07D),
        ["categoricalLime"] = (0x5B8A00, 0xB5E34C),
        ["categoricalMarigold"] = (0xB86E00, 0xF5B33C),

        // Sequential colors grow more prominent with the number: darker on light surfaces,
        // brighter on dark surfaces.
        ["sequential1"] = (0xC7E0F4, 0x0B3A5E),
        ["sequential2"] = (0xA0CBEF, 0x0E4C7A),
        ["sequential3"] = (0x71AFE5, 0x115E96),
        ["sequential4"] = (0x4A97DD, 0x1A73B3),
        ["sequential5"] = (0x2B88D8, 0x2B88D8),
        ["sequential6"] = (0x106EBE, 0x5AA3E6),
        ["sequential7"] = (0x005A9E, 0x8BBFF0),
        ["sequential8"] = (0x004578, 0xBBD9F7),

        ["divergingBlue"] = (0x1F5FBF, 0x5B8DEF),
        ["divergingLightBlue"] = (0x3F8FD9, 0x7DB3F2),
        ["divergingCyan"] = (0x2AA7C9, 0x6ED0E8),
        ["divergingTeal"] = (0x2E9E8C, 0x5FD0BD),
        ["divergingYellow"] = (0xC9A100, 0xF2D45C),
        ["divergingPeach"] = (0xD9822B, 0xF5A86B),
        ["divergingLightRed"] = (0xD9534F, 0xF27A73),
        ["divergingRed"] = (0xB32A2A, 0xE0504F),
        ["divergingMaroon"] = (0x7A1F2E, 0xB8475A),
        ["divergingGray"] = (0x7A7A7A, 0xA0A0A0),
    };

    private static readonly string[] CategoricalSet =
    [
        "categoricalBlue",
        "categoricalTeal",
        "categoricalPurple",
        "categoricalMarigold",
        "categoricalRed",
        "categoricalGreen",
        "categoricalLightBlue",
        "categoricalLavender",
        "categoricalLime",
    ];

    // Most prominent first, so a chart with one series is still easy to read.
    private static readonly string[] SequentialSet =
    [
        "sequential8",
        "sequential7",
        "sequential6",
        "sequential5",
        "sequential4",
        "sequential3",
        "sequential2",
        "sequential1",
    ];

    private static readonly string[] DivergingSet =
    [
        "divergingBlue",
        "divergingLightBlue",
        "divergingCyan",
        "divergingTeal",
        "divergingYellow",
        "divergingPeach",
        "divergingLightRed",
        "divergingRed",
        "divergingMaroon",
        "divergingGray",
    ];

    /// <summary>Returns the color names in <paramref name="colorSet"/>; unknown sets use categorical.</summary>
    public static IReadOnlyList<string> GetColorSet(string? colorSet)
    {
        if (string.Equals(colorSet, "sequential", StringComparison.OrdinalIgnoreCase))
        {
            return SequentialSet;
        }

        if (string.Equals(colorSet, "diverging", StringComparison.OrdinalIgnoreCase))
        {
            return DivergingSet;
        }

        return CategoricalSet;
    }

    /// <summary>
    /// Resolves a color name, <c>accent</c>, or a <c>#RRGGBB</c> value for the current theme.
    /// </summary>
    public static bool TryResolve(string? color, bool isDarkTheme, ChartColor accent, out ChartColor resolved)
    {
        resolved = default;
        if (string.IsNullOrWhiteSpace(color))
        {
            return false;
        }

        if (string.Equals(color, AccentColorName, StringComparison.OrdinalIgnoreCase))
        {
            resolved = accent;
            return true;
        }

        if (NamedColors.TryGetValue(color, out var pair))
        {
            resolved = ChartColor.FromRgb(isDarkTheme ? pair.Dark : pair.Light);
            return true;
        }

        return ChartColor.TryParseHex(color, out resolved);
    }

    /// <summary>
    /// Picks the color for one series or data point. An explicit item color wins, then the
    /// chart-wide color, then the color set in order.
    /// </summary>
    public static ChartColor ResolveSeriesColor(
        string? itemColor,
        string? chartColor,
        string? colorSet,
        int index,
        bool isDarkTheme,
        ChartColor accent)
    {
        if (TryResolve(itemColor, isDarkTheme, accent, out var color)
            || TryResolve(chartColor, isDarkTheme, accent, out color))
        {
            return color;
        }

        var set = GetColorSet(colorSet);
        TryResolve(set[Math.Abs(index) % set.Count], isDarkTheme, accent, out color);
        return color;
    }
}
