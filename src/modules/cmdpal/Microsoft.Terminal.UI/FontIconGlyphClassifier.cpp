// ReSharper disable CppInconsistentNaming
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

    namespace
    {
        // Helper to check if a code point is within a range
        constexpr bool _inRange(const uint32_t value, const uint32_t lo, const uint32_t hi) noexcept
        {
            return value >= lo && value <= hi;
        }

        constexpr bool _isRegionalIndicator(uint32_t cp) noexcept
        {
            static constexpr int32_t REGIONAL_INDICATOR_START = 0x1F1E6;
            static constexpr int32_t REGIONAL_INDICATOR_END = 0x1F1FF;
            return cp >= REGIONAL_INDICATOR_START && cp <= REGIONAL_INDICATOR_END;
        }

        constexpr bool _isEmojiModifier(uint32_t cp) noexcept
        {
            static constexpr int32_t TONES_START = 0x1F3FB;
            static constexpr int32_t TONES_END = 0x1F3FF;
            return cp >= TONES_START && cp <= TONES_END; // skin tones
        }

        bool _isEmojiLike(const UChar* p, const int32_t length) noexcept
        {
            if (length < 1)
            {
                return false;
            }

            constexpr uint32_t VS15 = 0xFE0E; // text presentation
            constexpr uint32_t VS16 = 0xFE0F; // emoji presentation
            constexpr uint32_t BLACKFLAG = 0x1F3F4; // base for tag flags
            constexpr uint32_t CANCELTAG = 0xE007F; // end of tag sequences

            bool hasVS15 = false;
            bool hasVS16 = false;
            bool endsWithCancelTag = false;
            int regionalCount = 0;

            uint32_t first = 0;
            bool haveFirst = false;

            for (int32_t i = 0; i < length;)
            {
                uint32_t cp = 0;
                U16_NEXT(p, i, length, cp);

                if (!haveFirst)
                {
                    first = cp;
                    haveFirst = true;
                }

                if (cp == VS15)
                {
                    hasVS15 = true;
                }
                else if (cp == VS16)
                {
                    hasVS16 = true;
                }
                else if (_isRegionalIndicator(cp))
                {
                    ++regionalCount;
                }
                else if (cp == CANCELTAG)
                {
                    endsWithCancelTag = true;
                }
            }

            // Regional-indicator flags require at least a pair within this grapheme
            if (regionalCount >= 2)
            {
                return true;
            }

            // Tag flags: U+1F3F4 + TAG letters … + CANCEL TAG
            if (haveFirst && first == BLACKFLAG && endsWithCancelTag)
            {
                return true;
            }

            // Emoji modifier sequences: base + skin tone (stick to ICU property)
            {
                for (int32_t i = 0; i < length;)
                {
                    uint32_t base = 0;
                    const int32_t start = i;
                    U16_NEXT(p, i, length, base);

                    // Skip immediate variation selectors after base
                    int32_t j = i;
                    while (j < length)
                    {
                        uint32_t v = 0;
                        int32_t k = j;
                        U16_NEXT(p, k, length, v);
                        if (v == VS15 || v == VS16)
                        {
                            j = k;
                            continue;
                        }
                        break;
                    }

                    if (j < length)
                    {
                        uint32_t mod = 0;
                        U16_NEXT(p, j, length, mod);
                        if (_isEmojiModifier(mod) && u_hasBinaryProperty(base, UCHAR_EMOJI_MODIFIER_BASE))
                            return true;
                    }

                    if (i <= start)
                    {
                        break;
                    }
                }
            }

            // Presentation selectors decide explicitly
            if (hasVS16)
            {
                return true; // force emoji
            }

            if (hasVS15)
            {
                return false; // force text
            }

            // Single-codepoint default: emoji by default iff Emoji_Presentation
            if (haveFirst && u_hasBinaryProperty(first, UCHAR_EMOJI_PRESENTATION))
                return true;

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

        // Note: Fluent/MDL2 PUA lives in BMP. It's sufficient to check the first UTF-16 code unit.
        bool _isLikelyInFluentPUA(const UChar* p) noexcept
        {
            return p
                && p[0] >= FluentIconRanges::PRIVATE_USE_AREA_START
                && p[0] <= FluentIconRanges::PRIVATE_USE_AREA_END;
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
            const UChar ch{ buffer[0] };

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
            if (_isEmojiLike(&ch, 1))
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
                                      static_cast<int32_t>(textSize),
                                      &status) };

        if (U_FAILURE(status) || !bi)
        {
            return FontIconGlyphKind::None;
        }

        const int32_t start{ ubrk_first(bi) };
        const int32_t end1{ ubrk_next(bi) }; // end of first grapheme
        ubrk_close(bi);

        // No graphemes found
        if (end1 == UBRK_DONE || end1 <= start)
        {
            return FontIconGlyphKind::None;
        }

        // If there's more than one grapheme, it's not a valid icon glyph
        if (std::cmp_not_equal(end1, textSize))
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