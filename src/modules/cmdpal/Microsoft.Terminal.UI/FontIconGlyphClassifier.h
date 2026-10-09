#pragma once

#include "FontIconGlyphClassifier.g.h"

namespace winrt::Microsoft::Terminal::UI::implementation
{
    struct FontIconGlyphClassifier
    {
        [[nodiscard]] static bool IsLikelyToBeEmojiOrSymbolIcon(const winrt::hstring& text);

        [[nodiscard]] static FontIconGlyphKind Classify(winrt::hstring const& text) noexcept;
    };
}

namespace winrt::Microsoft::Terminal::UI::factory_implementation
{
    BASIC_FACTORY(FontIconGlyphClassifier);
}
