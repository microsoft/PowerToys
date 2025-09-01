#pragma once

#include "FontIconGlyphClassifier.g.h"

namespace winrt::Microsoft::Terminal::UI::implementation
{
    struct FontIconGlyphClassifier
    {
        static bool IsLikelyToBeEmojiOrSymbolIcon(const winrt::hstring& text);

        static FontIconGlyphKind Classify(winrt::hstring const& text) noexcept;

        static bool IsEmojiLike(winrt::hstring const& text) noexcept;
    };
}

namespace winrt::Microsoft::Terminal::UI::factory_implementation
{
    BASIC_FACTORY(FontIconGlyphClassifier);
}
