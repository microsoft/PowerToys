// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// The colors of one Adaptive Card theme as <c>#AARRGGBB</c> strings. The light and dark values
/// follow the Fluent theme resources (text, system fill, card, and divider colors).
/// </summary>
internal sealed record AdaptiveCardThemeTokens
{
    public required string Foreground { get; init; }

    public required string ForegroundSubtle { get; init; }

    public required string Accent { get; init; }

    public required string Good { get; init; }

    public required string Warning { get; init; }

    public required string Attention { get; init; }

    public required string EmphasisBackground { get; init; }

    public required string AccentBackground { get; init; }

    public required string GoodBackground { get; init; }

    public required string WarningBackground { get; init; }

    public required string AttentionBackground { get; init; }

    public required string Separator { get; init; }

    /// <param name="accent">The accent text color, such as SystemAccentColorDark2.</param>
    public static AdaptiveCardThemeTokens Light(string accent) => new()
    {
        Foreground = "#E4000000",
        ForegroundSubtle = "#9E000000",
        Accent = accent,
        Good = "#FF0F7B0F",
        Warning = "#FF9D5D00",
        Attention = "#FFC42B1C",
        EmphasisBackground = "#B3FFFFFF",
        AccentBackground = WithAlpha(accent, 0x1A),
        GoodBackground = "#FFDFF6DD",
        WarningBackground = "#FFFFF4CE",
        AttentionBackground = "#FFFDE7E9",
        Separator = "#0F000000",
    };

    /// <param name="accent">The accent text color, such as SystemAccentColorLight3.</param>
    public static AdaptiveCardThemeTokens Dark(string accent) => new()
    {
        Foreground = "#FFFFFFFF",
        ForegroundSubtle = "#C5FFFFFF",
        Accent = accent,
        Good = "#FF6CCB5F",
        Warning = "#FFFCE100",
        Attention = "#FFFF99A4",
        EmphasisBackground = "#0DFFFFFF",
        AccentBackground = WithAlpha(accent, 0x26),
        GoodBackground = "#FF393D1B",
        WarningBackground = "#FF433519",
        AttentionBackground = "#FF442726",
        Separator = "#15FFFFFF",
    };

    /// <summary>High contrast uses the user's system colors for everything.</summary>
    public static AdaptiveCardThemeTokens HighContrast(string text, string grayText, string highlight, string window) => new()
    {
        Foreground = text,
        ForegroundSubtle = grayText,
        Accent = highlight,
        Good = text,
        Warning = text,
        Attention = text,
        EmphasisBackground = window,
        AccentBackground = window,
        GoodBackground = window,
        WarningBackground = window,
        AttentionBackground = window,
        Separator = text,
    };

    /// <summary>Formats an opaque color as <c>#AARRGGBB</c>.</summary>
    public static string ToHex(byte a, byte r, byte g, byte b) => $"#{a:X2}{r:X2}{g:X2}{b:X2}";

    /// <summary>Replaces the alpha of a <c>#AARRGGBB</c> or <c>#RRGGBB</c> color.</summary>
    public static string WithAlpha(string color, byte alpha)
    {
        var rgb = color.Length == 9 ? color[3..] : color.TrimStart('#');
        return $"#{alpha:X2}{rgb}";
    }
}
