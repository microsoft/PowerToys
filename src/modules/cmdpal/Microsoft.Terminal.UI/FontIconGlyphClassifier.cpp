// ReSharper disable CppInconsistentNaming
#include "pch.h"
#include "FontIconGlyphClassifier.h"
#include "FontIconGlyphClassifier.g.cpp"

#include <icu.h>
#include <utility>

namespace winrt::Microsoft::Terminal::UI::implementation
{
    namespace
    {
        // Helper to determine if a value is in a given inclusive range.
        [[nodiscard]] constexpr inline bool _inRange(const UChar32 value, const UChar32 lo, const UChar32 hi) noexcept
        {
            return value >= lo && value <= hi;
        }

        // Check if the code point is in the Private Use Area range used by Fluent UI icons.
        [[nodiscard]] constexpr bool _isFluentIconPua(const UChar32 cp) noexcept
        {
            static constexpr UChar32 _fluentIconsPrivateUseAreaStart = 0xE700;
            static constexpr UChar32 _fluentIconsPrivateUseAreaEnd = 0xF8FF;
            return _inRange(cp, _fluentIconsPrivateUseAreaStart, _fluentIconsPrivateUseAreaEnd);
        }

        // Check if the code point is a Regional Indicator symbol (used in pairs for flag emojis).
        [[nodiscard]] constexpr bool _isRegionalIndicator(const UChar32 cp) noexcept
        {
            static constexpr UChar32 regionalIndicatorCodePointStart = 0x1F1E6;
            static constexpr UChar32 regionalIndicatorCodePointEnd = 0x1F1FF;
            return _inRange(cp, regionalIndicatorCodePointStart, regionalIndicatorCodePointEnd);
        }

        // Check if the code point is an Emoji Modifier (skin tone).
        [[nodiscard]] constexpr bool _isEmojiModifier(const UChar32 cp) noexcept
        {
            static constexpr UChar32 skinTonesCodePointStart = 0x1F3FB;
            static constexpr UChar32 skinTonesCodePointEnd = 0x1F3FF;
            return _inRange(cp, skinTonesCodePointStart, skinTonesCodePointEnd);
        }

        // Determine if the given text (as a sequence of UChar code units) is emoji
        [[nodiscard]] bool _isEmoji(const UChar* p, const int32_t length) noexcept
        {
            if (!p || length < 1)
            {
                return false;
            }

            constexpr UChar32 vs15CodePoint = 0xFE0E; // text presentation
            constexpr UChar32 vs16CodePoint = 0xFE0F; // emoji presentation
            constexpr UChar32 blackFlagCodePoint = 0x1F3F4; // base for tag flags
            constexpr UChar32 cancelTagCodePoint = 0xE007F; // end of tag sequences

            UChar32 previousNonVS = 0;
            UChar32 first = 0;
            UChar32 last = 0;
            bool sawBlackFlag = false;
            bool sawVS15 = false;
            bool wasRegionalIndicator = false;
            bool haveFirst = false;

            for (int32_t i = 0; i < length;)
            {
                UChar32 cp = 0;
                U16_NEXT(p, i, length, cp);

                if (!haveFirst)
                {
                    first = cp;
                    haveFirst = true;
                }
                last = cp;

                if (cp == vs16CodePoint)
                {
                    return true;
                }

                if (cp == vs15CodePoint)
                {
                    sawVS15 = true;
                    continue;
                }

                const bool isRegionalIndicator = _isRegionalIndicator(cp);
                if (isRegionalIndicator && wasRegionalIndicator)
                {
                    return true;
                }
                wasRegionalIndicator = isRegionalIndicator;

                if (cp == blackFlagCodePoint)
                {
                    sawBlackFlag = true;
                }

                // Emoji modifier sequence: <base> [VS]? <modifier>
                // If current cp is a modifier and previous non-VS was a valid base -> emoji.
                if (_isEmojiModifier(cp) && u_hasBinaryProperty(previousNonVS, UCHAR_EMOJI_MODIFIER_BASE))
                {
                    return true;
                }

                previousNonVS = cp;
            }

            // Tag flags: BLACK FLAG + TAG letters … + CANCEL TAG
            if (sawBlackFlag && last == cancelTagCodePoint)
            {
                return true;
            }

            // Presentation selectors decide explicitly
            // VS15 can be overridden by VS16, so check it last
            if (sawVS15)
            {
                return false; // force text
            }

            // Single-codepoint default: emoji by default iff Emoji_Presentation
            if (haveFirst && u_hasBinaryProperty(first, UCHAR_EMOJI_PRESENTATION))
            {
                return true;
            }

            /*
             * https://www.unicode.org/reports/tr51/#Emoji_Properties
             * This causes us to classify text-default symbols (©, ®, ™, ⌨, …) as emoji by default:
             *
             *  if (haveFirst && u_hasBinaryProperty(first, UCHAR_EXTENDED_PICTOGRAPHIC))
             *       return true;
             */

            // Ambiguous text-default symbols (©, ®, ™, ⌨, …) are NOT emoji without VS16
            return false;
        }
    }

    bool FontIconGlyphClassifier::IsLikelyToBeEmojiOrSymbolIcon(const hstring& text)
    {
        if (text.empty())
        {
            return false;
        }

        if (text.size() == 1 && !IS_HIGH_SURROGATE(text[0]))
        {
            // If it's a single code unit, it's definitely either zero or one grapheme clusters.
            // If it turns out to be illegal Unicode, we don't really care.
            return true;
        }

        if (text.size() >= 2 && text[0] <= 0x7F && text[1] <= 0x7F)
        {
            // Two adjacent ASCII characters (as seen in most file paths) aren't a single
            // grapheme cluster.
            return false;
        }

        // Use ICU to determine whether text is composed of a single grapheme cluster.
        int32_t off{ 0 };
        UErrorCode status{ U_ZERO_ERROR };

        UBreakIterator* const bi{ ubrk_open(UBRK_CHARACTER,
                                            nullptr,
                                            reinterpret_cast<const UChar*>(text.data()),
                                            static_cast<int>(text.size()),
                                            &status) };
        if (bi)
        {
            if (U_SUCCESS(status))
            {
                off = ubrk_next(bi);
            }
            ubrk_close(bi);
        }
        return std::cmp_equal(off, text.size());
    }

    FontIconGlyphKind FontIconGlyphClassifier::Classify(hstring const& text) noexcept
    {
        if (text.empty())
        {
            return FontIconGlyphKind::None;
        }

        const size_t textSize{ text.size() };
        const auto* buffer{ reinterpret_cast<const UChar*>(text.c_str()) };

        // Fast path 1: Single UTF-16 code unit (most common case)
        if (textSize == 1)
        {
            const UChar ch{ buffer[0] };

            if (IS_HIGH_SURROGATE(ch))
            {
                return FontIconGlyphKind::Invalid;
            }

            if (_isFluentIconPua(ch))
            {
                return FontIconGlyphKind::FluentSymbol;
            }

            if (_isEmoji(&ch, 1))
            {
                return FontIconGlyphKind::Emoji;
            }

            return FontIconGlyphKind::Other;
        }

        // Fast path 2: Common file path pattern - two ASCII printable characters
        if (textSize >= 2 && buffer[0] <= 0x7F && buffer[1] <= 0x7F)
        {
            // Definitely multiple graphemes
            return FontIconGlyphKind::Invalid;
        }

        // Expensive path: Use ICU to determine grapheme boundaries
        UErrorCode status{ U_ZERO_ERROR };

        UBreakIterator* bi{ ubrk_open(UBRK_CHARACTER,
                                      nullptr,
                                      buffer,
                                      static_cast<int32_t>(textSize),
                                      &status) };

        if (U_FAILURE(status) || !bi)
        {
            return FontIconGlyphKind::Invalid;
        }

        const int32_t start{ ubrk_first(bi) };
        const int32_t end{ ubrk_next(bi) }; // end of first grapheme
        ubrk_close(bi);

        // No graphemes found
        if (end == UBRK_DONE || end <= start)
        {
            return FontIconGlyphKind::None;
        }

        // If there's more than one grapheme, it's not a valid icon glyph
        if (std::cmp_not_equal(end, textSize))
        {
            return FontIconGlyphKind::Invalid;
        }

        // Exactly one grapheme: classify
        const UChar* grapheme = buffer + start;
        const int32_t graphemeLength = end - start;

        if (_isFluentIconPua(grapheme[0]))
        {
            return FontIconGlyphKind::FluentSymbol;
        }

        if (_isEmoji(grapheme, graphemeLength))
        {
            return FontIconGlyphKind::Emoji;
        }

        return FontIconGlyphKind::Other;
    }
}