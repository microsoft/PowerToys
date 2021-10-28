#pragma once
#include "PatternSnippet.g.h"

namespace winrt::PowerRenameUILib::implementation
{
    struct PatternSnippet : PatternSnippetT<PatternSnippet>
    {
        PatternSnippet() = delete;

        PatternSnippet(hstring const& code, hstring const& description);
        hstring Code();
        void Code(hstring const& value);
        hstring Description();
        void Description(hstring const& value);
        winrt::event_token PropertyChanged(winrt::Windows::UI::Xaml::Data::PropertyChangedEventHandler const& handler);
        void PropertyChanged(winrt::event_token const& token) noexcept;

    private:
        winrt::hstring m_code;
        winrt::hstring m_description;
        winrt::event<Windows::UI::Xaml::Data::PropertyChangedEventHandler> m_propertyChanged;
    };
}
namespace winrt::PowerRenameUILib::factory_implementation
{
    struct PatternSnippet : PatternSnippetT<PatternSnippet, implementation::PatternSnippet>
    {
    };
}
