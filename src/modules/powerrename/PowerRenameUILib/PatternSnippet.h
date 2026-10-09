#pragma once
#include "PatternSnippet.g.h"

namespace winrt::PowerRenameUI::implementation
{
    struct PatternSnippet : PatternSnippetT<PatternSnippet>
    {
        PatternSnippet() = delete;

        PatternSnippet(hstring const& code, hstring const& description);
        hstring Code();
        void Code(hstring const& value);
        hstring Description();
        void Description(hstring const& value);
        winrt::event_token PropertyChanged(winrt::Microsoft::UI::Xaml::Data::PropertyChangedEventHandler const& handler);
        void PropertyChanged(winrt::event_token const& token) noexcept;

    private:
        winrt::hstring m_code;
        winrt::hstring m_description;
        winrt::event<Microsoft::UI::Xaml::Data::PropertyChangedEventHandler> m_propertyChanged;
    };
}
namespace winrt::PowerRenameUI::factory_implementation
{
    struct PatternSnippet : PatternSnippetT<PatternSnippet, implementation::PatternSnippet>
    {
    };
}
