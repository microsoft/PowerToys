// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using System.Xml;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Parses <c>|Swatch|color[|dark]</c> and
/// <c>|Initials|text|color[|dark][|circle|rounded]</c> icon strings.
/// Initials accept one to three Unicode text elements. A literal percent sign in
/// the text token is encoded as <c>%25</c>, and a literal separator as <c>%7C</c>.
/// Percent encoding keeps this hand-authored protocol legible; the machine-generated
/// app-icon protocol uses length-prefixed fields instead.
/// Colors use the XAML #RGB, #ARGB, #RRGGBB, or #AARRGGBB forms.
/// </summary>
internal static class GeneratedIconProtocol
{
    private const int MaxEncodedInitialsLength = 96;
    private const int MaxInitialsLength = 32;
    private const int MaxInitialsTextElements = 3;
    private const string SwatchPrefix = "|Swatch|";
    private const string InitialsPrefix = "|Initials|";
    private static readonly string[] ProtocolPrefixValues = [SwatchPrefix, InitialsPrefix];
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static ReadOnlySpan<string> ProtocolPrefixes => ProtocolPrefixValues;

    public static string GetCacheIdentity(string value)
    {
        var kind = Classify(value);
        if (kind == Kind.None)
        {
            return value;
        }

        try
        {
            if (kind == Kind.Swatch)
            {
                var swatchPayload = value.AsSpan(SwatchPrefix.Length);
                if (HasCanonicalStyleTokenCasing(swatchPayload)
                    || !TryParseSwatch(swatchPayload, out _, out _, out _))
                {
                    return value;
                }

                return CanonicalizeStyleTokenCasing(value, SwatchPrefix.Length);
            }

            var payload = value.AsSpan(InitialsPrefix.Length);
            var separator = payload.IndexOf('|');
            if (separator <= 0)
            {
                return value;
            }

            var initialsToken = payload[..separator];
            var stylePayload = payload[(separator + 1)..];
            var hasCanonicalInitials = IsCanonicalAsciiInitials(initialsToken);
            var hasCanonicalStyle = HasCanonicalStyleTokenCasing(stylePayload);
            if (hasCanonicalInitials && hasCanonicalStyle)
            {
                return value;
            }

            if (!TryParseInitials(
                payload,
                out var initials,
                out _,
                out _,
                out _,
                out _))
            {
                return value;
            }

            if (hasCanonicalInitials)
            {
                return CanonicalizeStyleTokenCasing(
                    value,
                    InitialsPrefix.Length + separator + 1);
            }

            // Canonicalize both Unicode representation and token escaping before
            // cache lookup, so visually identical text cannot occupy two entries.
            var escapedInitials = EscapeInitialsToken(initials);
            var identity = InitialsPrefix
                + escapedInitials
                + payload[separator..].ToString();
            return hasCanonicalStyle
                ? identity
                : CanonicalizeStyleTokenCasing(
                    identity,
                    InitialsPrefix.Length + escapedInitials.Length + 1);
        }
        catch
        {
            return value;
        }
    }

    private static bool IsCanonicalAsciiInitials(ReadOnlySpan<char> value)
    {
        if (value.Length is < 1 or > MaxInitialsTextElements)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is not (>= 'A' and <= 'Z')
                and not (>= '0' and <= '9'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasCanonicalStyleTokenCasing(ReadOnlySpan<char> payload)
    {
        payload = TrimOptionalTrailingSeparator(payload);
        while (TryReadToken(ref payload, out var token))
        {
            if (token.IsEmpty)
            {
                continue;
            }

            if (token[0] == '#')
            {
                foreach (var character in token[1..])
                {
                    if (character is >= 'a' and <= 'f')
                    {
                        return false;
                    }
                }
            }
            else
            {
                foreach (var character in token)
                {
                    if (character is >= 'A' and <= 'Z')
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static string CanonicalizeStyleTokenCasing(string value, int styleStart) =>
        string.Create(
            value.Length,
            (Value: value, StyleStart: styleStart),
            static (destination, state) =>
            {
                state.Value.AsSpan().CopyTo(destination);
                var remaining = destination[state.StyleStart..];
                while (!remaining.IsEmpty)
                {
                    var separator = remaining.IndexOf('|');
                    var token = separator < 0 ? remaining : remaining[..separator];
                    if (!token.IsEmpty)
                    {
                        if (token[0] == '#')
                        {
                            for (var index = 1; index < token.Length; index++)
                            {
                                if (token[index] is >= 'a' and <= 'f')
                                {
                                    token[index] = (char)(token[index] - ('a' - 'A'));
                                }
                            }
                        }
                        else
                        {
                            for (var index = 0; index < token.Length; index++)
                            {
                                if (token[index] is >= 'A' and <= 'Z')
                                {
                                    token[index] = (char)(token[index] + ('a' - 'A'));
                                }
                            }
                        }
                    }

                    if (separator < 0)
                    {
                        break;
                    }

                    remaining = remaining[(separator + 1)..];
                }
            });

    public static Kind Classify(string? value)
    {
        if (value?.StartsWith(SwatchPrefix, StringComparison.Ordinal) == true)
        {
            return Kind.Swatch;
        }

        if (value?.StartsWith(InitialsPrefix, StringComparison.Ordinal) == true)
        {
            return Kind.Initials;
        }

        return Kind.None;
    }

    public static ElementTheme GetCacheTheme(string? value, ElementTheme theme)
    {
        if (!IsThemeDependent(value))
        {
            return ElementTheme.Default;
        }

        return theme == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
    }

    public static bool TryCreateSwatchSvg(string? value, ElementTheme theme, out byte[] svg)
    {
        svg = [];

        try
        {
            if (Classify(value) != Kind.Swatch
                || !TryParseSwatch(
                    value!.AsSpan(SwatchPrefix.Length),
                    out var light,
                    out var dark,
                    out _))
            {
                return false;
            }

            svg = CreateSwatchSvg(SelectColor(light, dark, theme));
            return true;
        }
        catch
        {
            svg = [];
            return false;
        }
    }

    public static bool TryCreateInitialsSvg(string? value, ElementTheme theme, out byte[] svg)
    {
        svg = [];

        try
        {
            if (Classify(value) != Kind.Initials
                || !TryParseInitials(
                    value!.AsSpan(InitialsPrefix.Length),
                    out var initials,
                    out var light,
                    out var dark,
                    out _,
                    out var shape))
            {
                return false;
            }

            var hasGlyph = InitialsTextRenderer.TryCreatePathData(
                initials,
                out var pathData,
                out var useEvenOddFill);
            svg = CreateInitialsSvg(
                hasGlyph ? pathData : null,
                useEvenOddFill,
                SelectColor(light, dark, theme),
                theme,
                shape);
            return true;
        }
        catch
        {
            svg = [];
            return false;
        }
    }

    private static bool IsThemeDependent(string? value)
    {
        switch (Classify(value))
        {
            case Kind.Swatch:
                return TryParseSwatch(value!.AsSpan(SwatchPrefix.Length), out _, out _, out var hasDark) && hasDark;

            case Kind.Initials:
                // Foreground contrast can depend on the surface theme when the
                // background is translucent. Keep every initials entry isolated
                // by theme so this cheap discriminator never has to parse it.
                return true;

            default:
                return false;
        }
    }

    private static bool TryParseSwatch(
        ReadOnlySpan<char> payload,
        out RgbaColor light,
        out RgbaColor dark,
        out bool hasDark)
    {
        light = default;
        dark = default;
        hasDark = false;
        payload = TrimOptionalTrailingSeparator(payload);
        if (!TryReadToken(ref payload, out var lightToken) || !TryParseColor(lightToken, out light))
        {
            return false;
        }

        dark = light;
        if (!payload.IsEmpty)
        {
            if (!TryReadToken(ref payload, out var darkToken) || !TryParseColor(darkToken, out dark))
            {
                return false;
            }

            hasDark = true;
        }

        return payload.IsEmpty;
    }

    private static bool TryParseInitials(
        ReadOnlySpan<char> payload,
        out string initials,
        out RgbaColor light,
        out RgbaColor dark,
        out bool hasDark,
        out InitialsShape shape)
    {
        initials = string.Empty;
        light = default;
        dark = default;
        hasDark = false;
        shape = InitialsShape.Circle;

        payload = TrimOptionalTrailingSeparator(payload);
        if (!TryReadToken(ref payload, out var initialsToken)
            || !TryNormalizeInitials(initialsToken, out initials)
            || !TryReadToken(ref payload, out var lightToken)
            || !TryParseColor(lightToken, out light))
        {
            return false;
        }

        dark = light;
        if (!payload.IsEmpty)
        {
            if (!TryReadToken(ref payload, out var nextToken))
            {
                return false;
            }

            if (TryParseColor(nextToken, out var darkColor))
            {
                dark = darkColor;
                hasDark = true;
                if (!payload.IsEmpty
                    && (!TryReadToken(ref payload, out var shapeToken) || !TryParseShape(shapeToken, out shape)))
                {
                    return false;
                }
            }
            else if (!TryParseShape(nextToken, out shape))
            {
                return false;
            }
        }

        if (!payload.IsEmpty)
        {
            return false;
        }

        return true;
    }

    private static bool TryNormalizeInitials(ReadOnlySpan<char> value, out string initials)
    {
        initials = string.Empty;
        if (value.IsEmpty
            || value.Length > MaxEncodedInitialsLength
            || !TryDecodeInitialsToken(value, out var decoded))
        {
            return false;
        }

        decoded = decoded.Trim();
        if (decoded.Length is < 1 or > MaxInitialsLength)
        {
            return false;
        }

        // Preserve the original ASCII behavior while making canonically equivalent
        // Unicode spellings share rendering and cache identity.
        var normalized = decoded
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant()
            .Normalize(NormalizationForm.FormC);
        if (normalized.Length is < 1 or > MaxInitialsLength)
        {
            return false;
        }

        var remaining = normalized.AsSpan();
        var textElementCount = 0;
        while (!remaining.IsEmpty)
        {
            var textElementLength = StringInfo.GetNextTextElementLength(remaining);
            if (textElementLength <= 0 || ++textElementCount > MaxInitialsTextElements)
            {
                return false;
            }

            remaining = remaining[textElementLength..];
        }

        foreach (var rune in normalized.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control
                or UnicodeCategory.LineSeparator
                or UnicodeCategory.ParagraphSeparator)
            {
                return false;
            }
        }

        initials = normalized;
        return true;
    }

    private static bool TryDecodeInitialsToken(ReadOnlySpan<char> value, out string decoded)
    {
        decoded = string.Empty;
        if (value.IndexOf('%') < 0)
        {
            decoded = value.ToString();
            return true;
        }

        var builder = new StringBuilder(value.Length);
        Span<byte> escapedBytes = stackalloc byte[MaxEncodedInitialsLength / 3];
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                builder.Append(value[index]);
                continue;
            }

            var byteCount = 0;
            while (index < value.Length && value[index] == '%')
            {
                if (index > value.Length - 3
                    || !TryParseHexByte(value.Slice(index + 1, 2), out var escapedByte))
                {
                    return false;
                }

                escapedBytes[byteCount++] = escapedByte;
                index += 3;
            }

            try
            {
                builder.Append(StrictUtf8.GetString(escapedBytes[..byteCount]));
            }
            catch (DecoderFallbackException)
            {
                return false;
            }

            index--;
        }

        decoded = builder.ToString();
        return true;
    }

    private static string EscapeInitialsToken(string value)
    {
        if (value.AsSpan().IndexOfAny('%', '|') < 0)
        {
            return value;
        }

        // Escape '%' first. Reversing these calls would escape the '%' introduced
        // for a literal separator and make the canonical token decode incorrectly.
        return value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace("|", "%7C", StringComparison.Ordinal);
    }

    private static bool TryParseShape(ReadOnlySpan<char> value, out InitialsShape shape)
    {
        if (value.Equals("circle", StringComparison.OrdinalIgnoreCase))
        {
            shape = InitialsShape.Circle;
            return true;
        }

        if (value.Equals("rounded", StringComparison.OrdinalIgnoreCase))
        {
            shape = InitialsShape.RoundedSquare;
            return true;
        }

        shape = default;
        return false;
    }

    private static bool TryParseColor(ReadOnlySpan<char> value, out RgbaColor color)
    {
        color = default;
        if (value.IsEmpty || value[0] != '#')
        {
            return false;
        }

        value = value[1..];
        switch (value.Length)
        {
            case 3:
                if (!TryParseHexDigit(value[0], out var shortRed)
                    || !TryParseHexDigit(value[1], out var shortGreen)
                    || !TryParseHexDigit(value[2], out var shortBlue))
                {
                    return false;
                }

                color = new RgbaColor(255, ExpandHexDigit(shortRed), ExpandHexDigit(shortGreen), ExpandHexDigit(shortBlue));
                return true;

            case 4:
                if (!TryParseHexDigit(value[0], out var shortAlpha)
                    || !TryParseHexDigit(value[1], out shortRed)
                    || !TryParseHexDigit(value[2], out shortGreen)
                    || !TryParseHexDigit(value[3], out shortBlue))
                {
                    return false;
                }

                color = new RgbaColor(
                    ExpandHexDigit(shortAlpha),
                    ExpandHexDigit(shortRed),
                    ExpandHexDigit(shortGreen),
                    ExpandHexDigit(shortBlue));
                return true;

            case 6:
                if (!TryParseHexByte(value[..2], out var red)
                    || !TryParseHexByte(value.Slice(2, 2), out var green)
                    || !TryParseHexByte(value.Slice(4, 2), out var blue))
                {
                    return false;
                }

                color = new RgbaColor(255, red, green, blue);
                return true;

            case 8:
                if (!TryParseHexByte(value[..2], out var alpha)
                    || !TryParseHexByte(value.Slice(2, 2), out red)
                    || !TryParseHexByte(value.Slice(4, 2), out green)
                    || !TryParseHexByte(value.Slice(6, 2), out blue))
                {
                    return false;
                }

                color = new RgbaColor(alpha, red, green, blue);
                return true;

            default:
                return false;
        }
    }

    private static bool TryParseHexByte(ReadOnlySpan<char> value, out byte result)
    {
        if (!TryParseHexDigit(value[0], out var high) || !TryParseHexDigit(value[1], out var low))
        {
            result = 0;
            return false;
        }

        result = (byte)((high << 4) | low);
        return true;
    }

    private static bool TryParseHexDigit(char value, out byte result)
    {
        if (value is >= '0' and <= '9')
        {
            result = (byte)(value - '0');
            return true;
        }

        if (value is >= 'A' and <= 'F')
        {
            result = (byte)(value - 'A' + 10);
            return true;
        }

        if (value is >= 'a' and <= 'f')
        {
            result = (byte)(value - 'a' + 10);
            return true;
        }

        result = 0;
        return false;
    }

    private static byte ExpandHexDigit(byte value) => (byte)((value << 4) | value);

    private static ReadOnlySpan<char> TrimOptionalTrailingSeparator(ReadOnlySpan<char> value) =>
        !value.IsEmpty && value[^1] == '|' ? value[..^1] : value;

    private static bool TryReadToken(ref ReadOnlySpan<char> remaining, out ReadOnlySpan<char> token)
    {
        if (remaining.IsEmpty)
        {
            token = default;
            return false;
        }

        var separator = remaining.IndexOf('|');
        if (separator < 0)
        {
            token = remaining;
            remaining = [];
        }
        else
        {
            token = remaining[..separator];
            remaining = remaining[(separator + 1)..];
        }

        return !token.IsEmpty;
    }

    private static RgbaColor SelectColor(RgbaColor light, RgbaColor dark, ElementTheme theme) =>
        theme == ElementTheme.Dark ? dark : light;

    private static byte[] CreateSwatchSvg(RgbaColor color)
    {
        using var stream = new MemoryStream();
        using (var writer = CreateSvgWriter(stream))
        {
            WriteSvgStart(writer);
            writer.WriteStartElement("circle");
            writer.WriteAttributeString("cx", "16");
            writer.WriteAttributeString("cy", "16");
            writer.WriteAttributeString("r", "12");
            WriteFill(writer, color);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        return stream.ToArray();
    }

    private static byte[] CreateInitialsSvg(
        string? pathData,
        bool useEvenOddFill,
        RgbaColor background,
        ElementTheme theme,
        InitialsShape shape)
    {
        using var stream = new MemoryStream();
        using (var writer = CreateSvgWriter(stream))
        {
            WriteSvgStart(writer);
            if (shape == InitialsShape.Circle)
            {
                writer.WriteStartElement("circle");
                writer.WriteAttributeString("cx", "16");
                writer.WriteAttributeString("cy", "16");
                writer.WriteAttributeString("r", "15.5");
            }
            else
            {
                writer.WriteStartElement("rect");
                writer.WriteAttributeString("x", "0.5");
                writer.WriteAttributeString("y", "0.5");
                writer.WriteAttributeString("width", "31");
                writer.WriteAttributeString("height", "31");
                writer.WriteAttributeString("rx", "7");
            }

            WriteFill(writer, background);
            writer.WriteEndElement();

            if (!string.IsNullOrEmpty(pathData))
            {
                writer.WriteStartElement("path");
                writer.WriteAttributeString("d", pathData);
                if (useEvenOddFill)
                {
                    writer.WriteAttributeString("fill-rule", "evenodd");
                }

                WriteFill(writer, GetContrastingForeground(background, theme));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        return stream.ToArray();
    }

    private static XmlWriter CreateSvgWriter(Stream stream) =>
        XmlWriter.Create(
            stream,
            new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                OmitXmlDeclaration = true,
                Indent = false,
                CloseOutput = false,
            });

    private static void WriteSvgStart(XmlWriter writer)
    {
        writer.WriteStartElement("svg", "http://www.w3.org/2000/svg");
        writer.WriteAttributeString("viewBox", "0 0 32 32");
    }

    private static void WriteFill(XmlWriter writer, RgbaColor color)
    {
        writer.WriteAttributeString("fill", FormattableString.Invariant($"#{color.R:X2}{color.G:X2}{color.B:X2}"));
        if (color.A != byte.MaxValue)
        {
            writer.WriteAttributeString(
                "fill-opacity",
                (color.A / 255d).ToString("0.###", CultureInfo.InvariantCulture));
        }
    }

    private static RgbaColor GetContrastingForeground(RgbaColor background, ElementTheme theme)
    {
        var surface = theme == ElementTheme.Dark ? (byte)32 : byte.MaxValue;
        var red = Composite(background.R, background.A, surface);
        var green = Composite(background.G, background.A, surface);
        var blue = Composite(background.B, background.A, surface);
        var luminance = (0.2126 * ToLinear(red)) + (0.7152 * ToLinear(green)) + (0.0722 * ToLinear(blue));
        return luminance > 0.179
            ? new RgbaColor(255, 0, 0, 0)
            : new RgbaColor(255, 255, 255, 255);
    }

    private static byte Composite(byte foreground, byte alpha, byte background) =>
        (byte)(((foreground * alpha) + (background * (byte.MaxValue - alpha)) + 127) / byte.MaxValue);

    private static double ToLinear(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    internal enum Kind
    {
        None,
        Swatch,
        Initials,
    }

    private enum InitialsShape
    {
        Circle,
        RoundedSquare,
    }

    private readonly record struct RgbaColor(byte A, byte R, byte G, byte B);
}
