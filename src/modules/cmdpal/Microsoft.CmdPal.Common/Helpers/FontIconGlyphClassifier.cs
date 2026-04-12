// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.CmdPal.Common.Helpers;

public static partial class FontIconGlyphClassifier
{
    private const int VariationSelector15 = 0xFE0E;
    private const int VariationSelector16 = 0xFE0F;
    private const int FluentIconsPrivateUseAreaStart = 0xE700;
    private const int FluentIconsPrivateUseAreaEnd = 0xF8FF;

    // ICU binary property constant for UCHAR_EMOJI_PRESENTATION.
    // See https://unicode-org.github.io/icu-docs/apidoc/released/icu4c/uchar_8h.html
    private const int UCharEmojiPresentation = 0x3A;

    public static bool IsLikelyToBeEmojiOrSymbolIcon(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.Length == 1 && !char.IsHighSurrogate(text[0]))
        {
            return true;
        }

        if (HasTwoAdjacentAsciiCodeUnits(text))
        {
            return false;
        }

        return TryGetSingleTextElementLength(text, out var textElementLength) &&
            textElementLength == text.Length;
    }

    public static FontIconGlyphKind Classify(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return FontIconGlyphKind.None;
        }

        if (text.Length == 1)
        {
            var ch = text[0];
            if (char.IsHighSurrogate(ch))
            {
                return FontIconGlyphKind.Invalid;
            }

            if (IsFluentIconPua(ch))
            {
                return FontIconGlyphKind.FluentSymbol;
            }

            if (IsEmoji(text))
            {
                return FontIconGlyphKind.Emoji;
            }

            return FontIconGlyphKind.Other;
        }

        if (HasTwoAdjacentAsciiCodeUnits(text))
        {
            return FontIconGlyphKind.Invalid;
        }

        if (!TryGetSingleTextElementLength(text, out var textElementLength))
        {
            return FontIconGlyphKind.Invalid;
        }

        if (textElementLength <= 0)
        {
            return FontIconGlyphKind.None;
        }

        if (textElementLength != text.Length)
        {
            return FontIconGlyphKind.Invalid;
        }

        if (textElementLength == 1 && IsFluentIconPua(text[0]))
        {
            return FontIconGlyphKind.FluentSymbol;
        }

        if (IsEmoji(text.AsSpan(0, textElementLength)))
        {
            return FontIconGlyphKind.Emoji;
        }

        return FontIconGlyphKind.Other;
    }

    private static bool HasTwoAdjacentAsciiCodeUnits(string text) =>
        text.Length >= 2 && text[0] <= 0x7F && text[1] <= 0x7F;

    private static bool IsFluentIconPua(int codePoint) =>
        codePoint is >= FluentIconsPrivateUseAreaStart and <= FluentIconsPrivateUseAreaEnd;

    private static bool TryGetSingleTextElementLength(string text, out int textElementLength)
    {
        // Avoid try/catch for flow control. GetNextTextElementLength throws
        // ArgumentException only for isolated surrogates, which we can
        // detect cheaply up front.
        textElementLength = 0;
        if (ContainsIsolatedSurrogate(text))
        {
            return false;
        }

        textElementLength = StringInfo.GetNextTextElementLength(text);
        return true;
    }

    private static bool ContainsIsolatedSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                {
                    return true;
                }

                i++; // skip the low surrogate
            }
            else if (char.IsLowSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEmoji(string text) => IsEmoji(text.AsSpan());

    private static bool IsEmoji(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return false;
        }

        if (Rune.DecodeFromUtf16(text, out var firstRune, out var consumed) != OperationStatus.Done || consumed < 1)
        {
            return false;
        }

        // Scan for variation selectors after the first codepoint.
        // VS15/VS16 are modifiers — they always follow a base character.
        for (var i = consumed; i < text.Length;)
        {
            if (Rune.DecodeFromUtf16(text[i..], out var rune, out var charsConsumed) != OperationStatus.Done || charsConsumed < 1)
            {
                i++;
                continue;
            }

            if (rune.Value == VariationSelector16)
            {
                return true;
            }

            if (rune.Value == VariationSelector15)
            {
                return false;
            }

            i += charsConsumed;
        }

        return IsEmojiPresentation(firstRune.Value);
    }

    private static bool _icuAvailable = true;

    // Query the Windows ICU library directly so emoji classification stays
    // current with OS updates, matching the native FontIconGlyphClassifier
    // which calls the same function.
    private static bool IsEmojiPresentation(int codePoint)
    {
        // All emoji presentation codepoints are >= 0x231A. Skip the P/Invoke
        // for the vast majority of BMP characters (ASCII, PUA Fluent icons, etc.).
        if (codePoint < 0x231A || !_icuAvailable)
        {
            return false;
        }

        try
        {
            return IcuInterop.UHasBinaryProperty(codePoint, UCharEmojiPresentation);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // icu.dll is missing or doesn't export u_hasBinaryProperty —
            // disable further attempts and fall back to treating all
            // codepoints as non-emoji.  Icons will render with Segoe UI
            // instead of Segoe UI Emoji, which is an acceptable degradation.
            _icuAvailable = false;
            return false;
        }
    }

    private static partial class IcuInterop
    {
        [LibraryImport("icu.dll", EntryPoint = "u_hasBinaryProperty")]
        [return: MarshalAs(UnmanagedType.U1)]
        public static partial bool UHasBinaryProperty(int codePoint, int which);
    }
}
