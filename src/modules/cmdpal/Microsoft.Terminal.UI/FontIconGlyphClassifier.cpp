#include "pch.h"
#include "FontIconGlyphClassifier.h"
#include "FontIconGlyphClassifier.g.cpp"

#include <icu.h>
#include <utility>

namespace winrt::Microsoft::Terminal::UI::implementation
{
    namespace
    {
        // Check if the code point is in the Private Use Area range used by Fluent UI icons.
        [[nodiscard]] constexpr bool _isFluentIconPua(const UChar32 cp) noexcept
        {
            constexpr UChar32 fluentIconsPrivateUseAreaStart = 0xE700;
            constexpr UChar32 fluentIconsPrivateUseAreaEnd = 0xF8FF;
            return cp >= fluentIconsPrivateUseAreaStart && cp <= fluentIconsPrivateUseAreaEnd;
        }

        // Determine if the given text (as a sequence of UChar code units) is emoji
        [[nodiscard]] bool _isEmoji(const UChar* p, const int32_t length) noexcept
        {
            if (!p || length < 1)
            {
                return false;
            }

            // https://www.unicode.org/reports/tr51/#Emoji_Variation_Selector_Notes
            constexpr UChar32 vs15CodePoint = 0xFE0E; // Variation Selectors 15: text variation selector
            constexpr UChar32 vs16CodePoint = 0xFE0F; // Variation Selectors: 16 emoji variation selector

            // Decode the first code point correctly (surrogate-safe)
            int32_t i0{ 0 };
            UChar32 first{ 0 };
            U16_NEXT(p, i0, length, first);

            for (int32_t i = 0; i < length;)
            {
                UChar32 cp{ 0 };
                U16_NEXT(p, i, length, cp);

                if (cp == vs16CodePoint) { return true; }
                if (cp == vs15CodePoint) { return false; }
            }

            return !U_IS_SURROGATE(first) && u_hasBinaryProperty(first, UCHAR_EMOJI_PRESENTATION);
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

        if (graphemeLength == 1 && _isFluentIconPua(grapheme[0]))
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