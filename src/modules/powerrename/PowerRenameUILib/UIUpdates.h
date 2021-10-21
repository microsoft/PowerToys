#pragma once
#include "UIUpdates.g.h"

namespace winrt::PowerRenameUILib::implementation
{
    struct UIUpdates : UIUpdatesT<UIUpdates>
    {
        UIUpdates();

        bool ShowAll();
        void ShowAll(bool value);
        int32_t ChangedExplorerItemId();
        void ChangedExplorerItemId(int32_t value);
        bool Checked();
        void Checked(bool value);
        winrt::event_token PropertyChanged(winrt::Windows::UI::Xaml::Data::PropertyChangedEventHandler const& handler);
        void PropertyChanged(winrt::event_token const& token) noexcept;
        void ToggleAll();
        bool CloseUIWindow();
        void CloseUIWindow(bool closeUIWindow);
        bool ButtonRenameEnabled();
        void ButtonRenameEnabled(bool value);
        void Rename();

    private:
        bool m_showAll;
        int32_t m_changedItemId;
        bool m_checked;
        bool m_closeUIWindow;
        bool m_buttonRenameEnabled;
        winrt::event<Windows::UI::Xaml::Data::PropertyChangedEventHandler> m_propertyChanged;
    };
}
namespace winrt::PowerRenameUILib::factory_implementation
{
    struct UIUpdates : UIUpdatesT<UIUpdates, implementation::UIUpdates>
    {
    };
}
