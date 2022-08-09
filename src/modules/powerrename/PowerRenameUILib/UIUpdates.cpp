#include "pch.h"
#include "UIUpdates.h"
#include "UIUpdates.g.cpp"

namespace winrt::PowerRenameUI::implementation
{
    UIUpdates::UIUpdates()
    {
    }

    winrt::event_token UIUpdates::PropertyChanged(Microsoft::UI::Xaml::Data::PropertyChangedEventHandler const& handler)
    {
        return m_propertyChanged.add(handler);
    }

    void UIUpdates::PropertyChanged(winrt::event_token const& token) noexcept
    {
        m_propertyChanged.remove(token);
    }

    hstring UIUpdates::OriginalCount()
    {
        return m_originalCount;
    }

    void UIUpdates::OriginalCount(hstring value)
    {
        if (m_originalCount != value)
        {
            m_originalCount = value;
            m_propertyChanged(*this, Microsoft::UI::Xaml::Data::PropertyChangedEventArgs{ L"OriginalCount" });
        }
    }

    hstring UIUpdates::RenamedCount()
    {
        return m_renamedCount;
    }

    void UIUpdates::RenamedCount(hstring value)
    {
        if (m_renamedCount != value)
        {
            m_renamedCount = value;
            m_propertyChanged(*this, Microsoft::UI::Xaml::Data::PropertyChangedEventArgs{ L"RenamedCount" });
        }
    }
}
