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
    private const char TextVariationSelector = '\uFE0E';
    private const char EmojiVariationSelector = '\uFE0F';

    /// <summary>
    /// Reports whether <paramref name="text" /> is a glyph candidate without resolving
    /// the font family needed to render it.
    /// </summary>
    public static bool IsGlyphCandidate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.Length == 1)
        {
            // Match Classify's compatibility behavior for isolated surrogates.
            return !char.IsHighSurrogate(text[0]);
        }

        if (text.Length == 2 && char.IsSurrogatePair(text[0], text[1]))
        {
            return true;
        }

        if (text[0] <= 0x7F && text[1] <= 0x7F)
        {
            return false;
        }

        var textElementLength = StringInfo.GetNextTextElementLength(text.AsSpan());
        return textElementLength != 0 && textElementLength == text.Length;
    }

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

            if (char.IsLowSurrogate(character))
            {
                return FontIconGlyphKind.Other;
            }

            // Preserve the native classifier's result for a degenerate standalone VS16.
            if (character == EmojiVariationSelector)
            {
                return FontIconGlyphKind.Emoji;
            }

            return IsEmojiPresentation(character) ? FontIconGlyphKind.Emoji : FontIconGlyphKind.Other;
        }

        // A valid surrogate pair is exactly one Unicode scalar and therefore one
        // grapheme. Avoid general grapheme segmentation and decoding it twice.
        if (text.Length == 2 && char.IsSurrogatePair(text[0], text[1]))
        {
            var codePoint = char.ConvertToUtf32(text[0], text[1]);
            return IsEmojiPresentation(codePoint) ? FontIconGlyphKind.Emoji : FontIconGlyphKind.Other;
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
        var selectorIndex = text.AsSpan().IndexOfAny(TextVariationSelector, EmojiVariationSelector);
        if (selectorIndex >= 0)
        {
            return text[selectorIndex] == EmojiVariationSelector;
        }

        var status = Rune.DecodeFromUtf16(text.AsSpan(), out var first, out _);
        return status == OperationStatus.Done && IsEmojiPresentation(first.Value);
    }

    private static bool IsEmojiPresentation(int codePoint) =>
        NativeMethods.HasBinaryProperty(codePoint, EmojiPresentationProperty) != 0;

    private static partial class NativeMethods
    {
        // ICU lazily loads emoji property data behind a one-time lock, so this call
        // cannot safely suppress its GC transition.
        [LibraryImport("icu.dll", EntryPoint = "u_hasBinaryProperty")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial byte HasBinaryProperty(int codePoint, int property);
    }
}
