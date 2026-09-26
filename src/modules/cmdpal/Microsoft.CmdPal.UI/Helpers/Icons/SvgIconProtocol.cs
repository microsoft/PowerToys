// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Resolves plain <c>|Svg|payload</c> and theme-aware
/// <c>|ThemedSvg|[accent|]payload</c> icon strings. A payload is either inline SVG
/// or the path to an SVG file.
/// </summary>
/// <remarks>
/// Plain SVGs are passed through without placeholder expansion and share cache entries
/// across themes. Themed SVGs replace <c>{{ThemeColor}}</c> and <c>{{AccentColor}}</c>
/// and use distinct light- and dark-theme cache entries. Accent values may be opaque
/// SVG 1.1 hex colors or one of: danger, subtle, info, warning, success, neutral,
/// dark, or normal. Express transparency in the SVG template with <c>fill-opacity</c>,
/// <c>stroke-opacity</c>, or <c>opacity</c>.
/// SVG files are treated as immutable while cached.
/// </remarks>
internal static class SvgIconProtocol
{
    private const string PlainPrefix = "|Svg|";
    private const string ThemedPrefix = "|ThemedSvg|";
    private static readonly string[] ProtocolPrefixValues = [PlainPrefix, ThemedPrefix];
    private const string ThemeColorPlaceholder = "{{ThemeColor}}";
    private const string AccentColorPlaceholder = "{{AccentColor}}";
    private const string LightThemeColor = "#000000";
    private const string DarkThemeColor = "#FFFFFF";

    public static ReadOnlySpan<string> ProtocolPrefixes => ProtocolPrefixValues;

    public static string GetCacheIdentity(string value)
    {
        if (!value.StartsWith(ThemedPrefix, StringComparison.Ordinal))
        {
            return value;
        }

        var untrimmed = value.AsSpan(ThemedPrefix.Length);
        var remaining = untrimmed.TrimStart();
        if (remaining.IsEmpty || remaining[0] == '<')
        {
            return value;
        }

        var separator = remaining.IndexOf('|');
        if (separator < 0)
        {
            return value;
        }

        var accent = remaining[..separator];
        if (accent.IsEmpty)
        {
            return value;
        }

        var isHex = accent[0] == '#';

        // Canonical cache hits need only a casing scan. Validate an accent only
        // when rewriting it, so malformed requests keep their original identity.
        if (HasCanonicalAccentCasing(accent, isHex) || !IsSupportedSvgAccent(accent))
        {
            return value;
        }

        var accentStart = ThemedPrefix.Length + untrimmed.Length - remaining.Length;
        return string.Create(
            value.Length,
            (Value: value, AccentStart: accentStart, AccentLength: accent.Length, IsHex: isHex),
            static (destination, state) =>
            {
                state.Value.AsSpan().CopyTo(destination);
                var normalizedAccent = destination.Slice(state.AccentStart, state.AccentLength);
                for (var index = state.IsHex ? 1 : 0; index < normalizedAccent.Length; index++)
                {
                    if (state.IsHex)
                    {
                        if (normalizedAccent[index] is >= 'a' and <= 'f')
                        {
                            normalizedAccent[index] = (char)(normalizedAccent[index] - ('a' - 'A'));
                        }
                    }
                    else if (normalizedAccent[index] is >= 'A' and <= 'Z')
                    {
                        normalizedAccent[index] = (char)(normalizedAccent[index] + ('a' - 'A'));
                    }
                }
            });
    }

    private static bool HasCanonicalAccentCasing(ReadOnlySpan<char> accent, bool isHex)
    {
        foreach (var character in isHex ? accent[1..] : accent)
        {
            if (isHex
                ? character is >= 'a' and <= 'f'
                : character is >= 'A' and <= 'Z')
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsProtocol(string? value) =>
        value?.StartsWith(PlainPrefix, StringComparison.Ordinal) == true
        || value?.StartsWith(ThemedPrefix, StringComparison.Ordinal) == true;

    public static Kind Classify(string? value)
    {
        if (value?.StartsWith(PlainPrefix, StringComparison.Ordinal) == true)
        {
            return IsInline(value.AsSpan(PlainPrefix.Length)) ? Kind.PlainInline : Kind.PlainFile;
        }

        if (value?.StartsWith(ThemedPrefix, StringComparison.Ordinal) != true)
        {
            return Kind.None;
        }

        var payload = value.AsSpan(ThemedPrefix.Length).TrimStart();
        if (!payload.IsEmpty && payload[0] != '<')
        {
            var separator = payload.IndexOf('|');
            if (separator >= 0 && IsSupportedSvgAccent(payload[..separator]))
            {
                payload = payload[(separator + 1)..];
            }
        }

        return IsInline(payload) ? Kind.ThemedInline : Kind.ThemedFile;
    }

    public static ElementTheme GetCacheTheme(string? value, ElementTheme theme) =>
        value?.StartsWith(ThemedPrefix, StringComparison.Ordinal) == true
            ? theme == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light
            : ElementTheme.Default;

    public static bool TryCreateSvg(string? value, ElementTheme theme, out byte[] svg)
    {
        svg = [];

        try
        {
            switch (Classify(value))
            {
                case Kind.PlainFile:
                case Kind.PlainInline:
                    return TryCreatePlainSvg(value!, out svg);

                case Kind.ThemedFile:
                case Kind.ThemedInline:
                    return TryCreateThemedSvg(value!, theme, out svg);

                default:
                    return false;
            }
        }
        catch
        {
            svg = [];
            return false;
        }
    }

    private static bool TryCreatePlainSvg(string value, out byte[] svg)
    {
        svg = [];
        var payload = value[PlainPrefix.Length..];
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        if (IsInline(payload))
        {
            // Inline strings have no source encoding; UTF-8 is the protocol encoding.
            // Remove a declaration that would describe the caller's original bytes,
            // not the UTF-8 bytes emitted by this protocol.
            svg = Encoding.UTF8.GetBytes(RemoveXmlDeclaration(payload));
            return true;
        }

        if (!IsSvgPath(payload))
        {
            return false;
        }

        // IconPathConverter.Prepare invokes this on an icon-loader worker, so
        // filesystem access never blocks the WinUI STA thread. Reading bytes also
        // preserves the file's original encoding and XML declaration exactly.
        svg = File.ReadAllBytes(payload);
        return svg.Length > 0;
    }

    private static bool TryCreateThemedSvg(string value, ElementTheme theme, out byte[] svg)
    {
        svg = [];
        if (!TryParseThemedPayload(value, theme, out var payload, out var accentColor))
        {
            return false;
        }

        string template;
        if (IsInline(payload))
        {
            template = payload;
        }
        else
        {
            if (!IsSvgPath(payload))
            {
                return false;
            }

            // This path is reached only from an icon-loader worker; see the plain
            // SVG path above. Honor either a BOM or a BOM-less XML encoding
            // declaration before the expanded result is re-encoded as UTF-8.
            if (!SvgFileTextReader.TryRead(payload, out template))
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        // A source file may declare a different encoding. Drop that now-stale
        // declaration before emitting the expanded SVG as UTF-8.
        template = RemoveXmlDeclaration(template);
        var themeColor = theme == ElementTheme.Dark ? DarkThemeColor : LightThemeColor;
        var resolved = template
            .Replace(ThemeColorPlaceholder, themeColor, StringComparison.Ordinal)
            .Replace(AccentColorPlaceholder, accentColor, StringComparison.Ordinal);

        svg = Encoding.UTF8.GetBytes(resolved);
        return true;
    }

    private static bool TryParseThemedPayload(
        string value,
        ElementTheme theme,
        out string payload,
        out string accentColor)
    {
        payload = string.Empty;
        accentColor = SemanticIconColor.GetDefault(theme);

        var remaining = value.AsSpan(ThemedPrefix.Length).TrimStart();
        if (remaining.IsEmpty)
        {
            return false;
        }

        if (remaining[0] != '<')
        {
            var separator = remaining.IndexOf('|');
            if (separator >= 0)
            {
                if (!TryResolveAccent(remaining[..separator], theme, out accentColor))
                {
                    return false;
                }

                remaining = remaining[(separator + 1)..].TrimStart();
                if (remaining.IsEmpty)
                {
                    return false;
                }
            }
        }

        payload = remaining.ToString();
        return true;
    }

    private static bool TryResolveAccent(
        ReadOnlySpan<char> value,
        ElementTheme theme,
        out string accentColor)
    {
        if (!IsSupportedSvgAccent(value))
        {
            accentColor = string.Empty;
            return false;
        }

        if (IsOpaqueSvgHexColor(value))
        {
            accentColor = value.ToString();
            return true;
        }

        return SemanticIconColor.TryResolve(value, theme, out accentColor);
    }

    private static bool IsSupportedSvgAccent(ReadOnlySpan<char> value)
    {
        if (IsOpaqueSvgHexColor(value))
        {
            return true;
        }

        return SemanticIconColor.TryResolvePair(value, out var light, out var dark)
            && IsOpaqueSvgHexColor(light)
            && IsOpaqueSvgHexColor(dark);
    }

    private static bool IsOpaqueSvgHexColor(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || value[0] != '#' || value.Length is not (4 or 7))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!Uri.IsHexDigit(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSvgPath(string value) =>
        Path.GetExtension(value).Equals(".svg", StringComparison.OrdinalIgnoreCase);

    private static bool IsInline(string value) => IsInline(value.AsSpan());

    private static bool IsInline(ReadOnlySpan<char> value)
    {
        value = value.TrimStart();
        return !value.IsEmpty && value[0] == '<';
    }

    private static string RemoveXmlDeclaration(string template)
    {
        var firstNonWhitespace = 0;
        while (firstNonWhitespace < template.Length && char.IsWhiteSpace(template[firstNonWhitespace]))
        {
            firstNonWhitespace++;
        }

        var candidate = template.AsSpan(firstNonWhitespace);
        if (!candidate.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            || candidate.Length <= 5
            || (!char.IsWhiteSpace(candidate[5]) && candidate[5] != '?'))
        {
            return template;
        }

        var declarationEnd = template.IndexOf("?>", firstNonWhitespace + 5, StringComparison.Ordinal);
        return declarationEnd >= 0
            ? template.Remove(firstNonWhitespace, (declarationEnd + 2) - firstNonWhitespace)
            : template;
    }

    internal enum Kind
    {
        None,
        PlainFile,
        PlainInline,
        ThemedFile,
        ThemedInline,
    }
}
