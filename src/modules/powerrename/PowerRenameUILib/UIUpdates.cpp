#include "pch.h"
#include "UIUpdates.h"
#include "UIUpdates.g.cpp"

namespace winrt::PowerRenameUILib::implementation
{
    UIUpdates::UIUpdates() :
        m_showAll{ true }, m_changedItemId{ -1 }, m_checked{ true }, m_closeUIWindow{ false }, m_buttonRenameEnabled{ false }
    {
    }

    bool UIUpdates::ShowAll()
    {
        return m_showAll;
    }

    void UIUpdates::ShowAll(bool value)
    {
        if (m_showAll != value)
        {
            m_showAll = value;
            m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"ShowAll" });
        }
    }

    int32_t UIUpdates::ChangedExplorerItemId()
    {
        return m_changedItemId;
    }

    void UIUpdates::ChangedExplorerItemId(int32_t value)
    {
        m_changedItemId = value;
        m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"ChangedItemId" });
    }

    bool UIUpdates::Checked()
    {
        return m_checked;
    }

    void UIUpdates::Checked(bool value)
    {
        m_checked = value;
    }

    winrt::event_token UIUpdates::PropertyChanged(winrt::Windows::UI::Xaml::Data::PropertyChangedEventHandler const& handler)
    {
        return m_propertyChanged.add(handler);
    }

    void UIUpdates::PropertyChanged(winrt::event_token const& token) noexcept
    {
        m_propertyChanged.remove(token);
    }

    void UIUpdates::ToggleAll()
    {
        m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"ToggleAll" });
    }

    void UIUpdates::Rename()
    {
        m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"Rename" });
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
            m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"OriginalCount" });
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
            m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"RenamedCount" });
        }
    }

    bool UIUpdates::CloseUIWindow()
    {
        return m_closeUIWindow;
    }

    void UIUpdates::CloseUIWindow(bool closeUIWindow)
    {
        m_closeUIWindow = closeUIWindow;
    }

    bool UIUpdates::ButtonRenameEnabled()
    {
        return m_buttonRenameEnabled;
    }

    void UIUpdates::ButtonRenameEnabled(bool value)
    {
        if (m_buttonRenameEnabled != value)
        {
            m_buttonRenameEnabled = value;
            m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"ButtonRenameEnabled" });
        }
    }
}
