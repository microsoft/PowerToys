// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.CmdPal.UI.Helpers;

internal static partial class FontIconGlyphClassifier
{
    private const string FluentIconFontFamily = "Segoe Fluent Icons, Segoe MDL2 Assets";
    private const string EmojiFontFamily = "Segoe UI Emoji, Segoe UI";
    private const string GeneralFontFamily = "Segoe UI";
    private const int EmojiPresentationProperty = 58;
    private const int FluentIconsPrivateUseAreaStart = 0xE700;
    private const int FluentIconsPrivateUseAreaEnd = 0xF8FF;
    private const int TextVariationSelector = 0xFE0E;
    private const int EmojiVariationSelector = 0xFE0F;

    public static FontIconGlyphKind Classify(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return FontIconGlyphKind.None;
        }

        // Most CmdPal glyphs are one UTF-16 code unit in the Fluent icon PUA.
        if (text.Length == 1)
        {
            var character = text[0];
            if (char.IsHighSurrogate(character))
            {
                return FontIconGlyphKind.Invalid;
            }

            if (IsFluentIconPua(character))
            {
                return FontIconGlyphKind.FluentSymbol;
            }

            return IsEmoji(text) ? FontIconGlyphKind.Emoji : FontIconGlyphKind.Other;
        }

        // Two adjacent ASCII characters cannot be the single glyph expected here. This
        // rejects common paths without paying for Unicode grapheme segmentation.
        if (text[0] <= 0x7F && text[1] <= 0x7F)
        {
            return FontIconGlyphKind.Invalid;
        }

        var textElementLength = StringInfo.GetNextTextElementLength(text.AsSpan());
        if (textElementLength == 0)
        {
            return FontIconGlyphKind.None;
        }

        if (textElementLength != text.Length)
        {
            return FontIconGlyphKind.Invalid;
        }

        return IsEmoji(text) ? FontIconGlyphKind.Emoji : FontIconGlyphKind.Other;
    }

    public static string GetFontFamily(FontIconGlyphKind glyphKind, string? requestedFontFamily)
    {
        if (glyphKind == FontIconGlyphKind.Invalid)
        {
            return GeneralFontFamily;
        }

        if (!string.IsNullOrEmpty(requestedFontFamily))
        {
            return requestedFontFamily;
        }

        return glyphKind switch
        {
            FontIconGlyphKind.FluentSymbol => FluentIconFontFamily,
            FontIconGlyphKind.Emoji => EmojiFontFamily,
            _ => GeneralFontFamily,
        };
    }

    private static bool IsFluentIconPua(int codePoint) =>
        codePoint is >= FluentIconsPrivateUseAreaStart and <= FluentIconsPrivateUseAreaEnd;

    private static bool IsEmoji(string text)
    {
        foreach (var codePoint in text.EnumerateRunes())
        {
            if (codePoint.Value == EmojiVariationSelector)
            {
                return true;
            }

            if (codePoint.Value == TextVariationSelector)
            {
                return false;
            }
        }

        var status = Rune.DecodeFromUtf16(text.AsSpan(), out var first, out _);
        return status == OperationStatus.Done && NativeMethods.HasBinaryProperty(first.Value, EmojiPresentationProperty) != 0;
    }

    private static partial class NativeMethods
    {
        // ICU lazily loads emoji property data behind a one-time lock, so this call
        // cannot safely suppress its GC transition.
        [LibraryImport("icu.dll", EntryPoint = "u_hasBinaryProperty")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial byte HasBinaryProperty(int codePoint, int property);
    }
}
