#pragma once
#include "UIUpdates.g.h"

namespace winrt::PowerRenameUI::implementation
{
    struct UIUpdates : UIUpdatesT<UIUpdates>
    {
        UIUpdates();

        winrt::event_token PropertyChanged(winrt::Microsoft::UI::Xaml::Data::PropertyChangedEventHandler const& handler);
        void PropertyChanged(winrt::event_token const& token) noexcept;
        bool CloseUIWindow();
        void CloseUIWindow(bool closeUIWindow);
        bool ButtonRenameEnabled();
        void ButtonRenameEnabled(bool value);
        void Rename();
        hstring OriginalCount();
        void OriginalCount(hstring value);
        hstring RenamedCount();
        void RenamedCount(hstring value);

    private:
        bool m_closeUIWindow;
        bool m_buttonRenameEnabled;
        hstring m_originalCount;
        hstring m_renamedCount;
        winrt::event<Microsoft::UI::Xaml::Data::PropertyChangedEventHandler> m_propertyChanged;
    };
}
namespace winrt::PowerRenameUI::factory_implementation
{
    struct UIUpdates : UIUpdatesT<UIUpdates, implementation::UIUpdates>
    {
    };
}
