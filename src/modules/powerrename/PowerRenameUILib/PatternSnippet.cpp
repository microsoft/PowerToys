#include "pch.h"
#include "PatternSnippet.h"
#include "PatternSnippet.g.cpp"

namespace winrt::PowerRenameUI::implementation
{
    PatternSnippet::PatternSnippet(hstring const& code, hstring const& description) :
        m_code{ code }, m_description{ description }
    {
    }

    hstring PatternSnippet::Code()
    {
        return m_code;
    }

    void PatternSnippet::Code(hstring const& value)
    {
        if (m_code != value)
        {
            m_code = value;
            m_propertyChanged(*this, Microsoft::UI::Xaml::Data::PropertyChangedEventArgs{ L"Code" });
        }
    }

    hstring PatternSnippet::Description()
    {
        return m_description;
    }

    void PatternSnippet::Description(hstring const& value)
    {
        if (m_description != value)
        {
            m_description = value;
            m_propertyChanged(*this, Microsoft::UI::Xaml::Data::PropertyChangedEventArgs{ L"Description" });
        }
    }

    winrt::event_token PatternSnippet::PropertyChanged(winrt::Microsoft::UI::Xaml::Data::PropertyChangedEventHandler const& handler)
    {
        return m_propertyChanged.add(handler);
    }

    void PatternSnippet::PropertyChanged(winrt::event_token const& token) noexcept
    {
        m_propertyChanged.remove(token);
    }
}
