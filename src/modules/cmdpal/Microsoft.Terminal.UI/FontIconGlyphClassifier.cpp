#include "pch.h"
#include "FontIconGlyphClassifier.h"
#include "FontIconGlyphClassifier.g.cpp"

#include <icu.h>
#include <utility>

namespace winrt::Microsoft::Terminal::UI::implementation
{
    namespace FluentIconRanges
    {
        // Range bounds for Segoe Fluent Icons/Segoe MDL2 Assets Private Use Areas
        static constexpr uint32_t PRIVATE_USE_AREA_START = 0xE700;
        static constexpr uint32_t PRIVATE_USE_AREA_END = 0xF8FF;
    }

    namespace UnicodeChars
    {
        static constexpr int32_t VARIATION_SELECTOR_16 = 0xFE0F;
        static constexpr int32_t ZERO_WIDTH_JOINER = 0x200D;
        static constexpr int32_t COMBINING_ENCLOSING_KEYCAP = 0x20E3;

        static constexpr int32_t TONES_START = 0x1F3FB;
        static constexpr int32_t TONES_END = 0x1F3FF;

        static constexpr int32_t REGIONAL_INDICATOR_START = 0x1F1E6;
        static constexpr int32_t REGIONAL_INDICATOR_END = 0x1F1FF;
    }

    namespace
    {
        // Helper to check if a code point is within a range
        constexpr bool _inRange(const uint32_t value, const uint32_t lo, const uint32_t hi) noexcept
        {
            return value >= lo && value <= hi;
        }

        // Internal implementation: determine if a sequence is likely an emoji-like grapheme
        bool _isEmojiLike(const UChar* p, const int32_t length)
        {
            bool sawRegional = false;
            int32_t index = 0;
            UChar32 cp;
            while (index < length)
            {
                U16_NEXT(p, index, length, cp);

                // ICU properties
                if (u_hasBinaryProperty(cp, UCHAR_EXTENDED_PICTOGRAPHIC)) // will match ♡ or ⌨︎
                {
                    return true;
                }

                if (u_hasBinaryProperty(cp, UCHAR_EMOJI_PRESENTATION)) // matches emoji that default to emoji presentation
                {
                    return true;
                }

                if (u_hasBinaryProperty(cp, UCHAR_EMOJI_COMPONENT))
                {
                    return true;
                }

                // Please render me as emoji variation selector
                if (cp == UnicodeChars::VARIATION_SELECTOR_16)
                {
                    return true;
                }

                /*
                There seems to be a legitimate use case for ZWJ sequences that are not emoji, e.g. Bengali \u0995\u09CD\u200D  →  ক্‌
                if (cp == UnicodeChars::ZERO_WIDTH_JOINER)
                {
                    return true;
                }
                */

                // Skin tone modifiers
                if (_inRange(cp, UnicodeChars::TONES_START, UnicodeChars::TONES_END))
                {
                    return true;
                }

                // Regional indicator pairs -> flag
                if (_inRange(cp, UnicodeChars::REGIONAL_INDICATOR_START, UnicodeChars::REGIONAL_INDICATOR_END))
                {
                    if (sawRegional)
                        return true;
                    sawRegional = true;
                }
            }
            return false;
        }

        // Note: Fluent/MDL2 PUA lives in BMP. It's sufficient to check the first UTF-16 code unit.
        bool _isLikelyInFluentPUA(const UChar* p) noexcept
        {
            if (!p)
            {
                return false;
            }
            const auto cu = static_cast<uint32_t>(static_cast<uint16_t>(*p));
            return _inRange(cu, FluentIconRanges::PRIVATE_USE_AREA_START, FluentIconRanges::PRIVATE_USE_AREA_END);
        }
    }

    bool FontIconGlyphClassifier::IsEmojiLike(hstring const& text) noexcept
    {
        if (text.empty())
        {
            return false;
        }

        const auto* p = reinterpret_cast<const UChar*>(text.c_str());
        const auto length = static_cast<int32_t>(text.size());
        return _isEmojiLike(p, length);
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
            const UChar ch = buffer[0];

            // High surrogate without low surrogate = invalid UTF-16
            if (IS_HIGH_SURROGATE(ch))
            {
                return FontIconGlyphKind::Invalid;
            }

            // Check if it's in Fluent PUA range (always single grapheme)
            if (_inRange(ch, FluentIconRanges::PRIVATE_USE_AREA_START, FluentIconRanges::PRIVATE_USE_AREA_END))
            {
                return FontIconGlyphKind::FluentSymbol;
            }

            // Check basic emoji properties (Note: some emoji need variation selectors)
            if (u_hasBinaryProperty(ch, UCHAR_EXTENDED_PICTOGRAPHIC) || u_hasBinaryProperty(ch, UCHAR_EMOJI_PRESENTATION))
            {
                return FontIconGlyphKind::Emoji;
            }

            // Single BMP character that's not emoji/fluent
            return FontIconGlyphKind::Other;
        }

        // Fast path 2: Common file path pattern - two ASCII printable characters
        if (textSize >= 2 && buffer[0] <= 0x7E && buffer[1] <= 0x7E)
        {
            // Definitely multiple graphemes
            return FontIconGlyphKind::Invalid;
        }

        UErrorCode status{ U_ZERO_ERROR };

        UBreakIterator* bi{ ubrk_open(UBRK_CHARACTER,
                                      nullptr,
                                      buffer,
                                      static_cast<int32_t>(text.size()),
                                      &status) };

        if (U_FAILURE(status) || !bi)
        {
            return FontIconGlyphKind::None;
        }

        const int32_t start = ubrk_first(bi);
        const int32_t end1 = ubrk_next(bi); // end of first grapheme
        if (end1 == UBRK_DONE || end1 <= start)
        {
            ubrk_close(bi);
            return FontIconGlyphKind::None;
        }

        // See if there's more than one grapheme
        const int32_t end2 = ubrk_next(bi);
        ubrk_close(bi);
        if (end2 != UBRK_DONE)
        {
            return FontIconGlyphKind::Invalid;
        }

        // Exactly one grapheme: classify
        const UChar* g = buffer + start;
        const int32_t glen = end1 - start;

        if (_isLikelyInFluentPUA(g))
        {
            return FontIconGlyphKind::FluentSymbol;
        }

        if (_isEmojiLike(g, glen))
        {
            return FontIconGlyphKind::Emoji;
        }

        return FontIconGlyphKind::Other;
    }
}