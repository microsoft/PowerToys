// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

/// <summary>An sRGB color with alpha that does not depend on a UI framework.</summary>
internal readonly record struct ChartColor(byte A, byte R, byte G, byte B)
{
    public static ChartColor FromRgb(uint rgb) =>
        new(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    /// <summary>Returns this color with its alpha multiplied by <paramref name="opacity"/>.</summary>
    public ChartColor WithOpacity(double opacity) =>
        this with { A = (byte)Math.Clamp(Math.Round(A * opacity), 0, 255) };

    /// <summary>Parses <c>#RGB</c>, <c>#RRGGBB</c>, or <c>#AARRGGBB</c>.</summary>
    public static bool TryParseHex(string? value, out ChartColor color)
    {
        color = default;
        if (string.IsNullOrEmpty(value) || value[0] != '#')
        {
            return false;
        }

        var hex = value.AsSpan(1);
        if (!uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        switch (hex.Length)
        {
            case 3:
                color = new ChartColor(
                    0xFF,
                    (byte)(((parsed >> 8) & 0xF) * 0x11),
                    (byte)(((parsed >> 4) & 0xF) * 0x11),
                    (byte)((parsed & 0xF) * 0x11));
                return true;
            case 6:
                color = FromRgb(parsed);
                return true;
            case 8:
                color = new ChartColor((byte)(parsed >> 24), (byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed);
                return true;
            default:
                return false;
        }
    }
}
