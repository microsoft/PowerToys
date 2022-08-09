#pragma once
#include "UIUpdates.g.h"

namespace winrt::PowerRenameUI::implementation
{
    struct UIUpdates : UIUpdatesT<UIUpdates>
    {
        UIUpdates();

        winrt::event_token PropertyChanged(winrt::Microsoft::UI::Xaml::Data::PropertyChangedEventHandler const& handler);
        void PropertyChanged(winrt::event_token const& token) noexcept;
        hstring OriginalCount();
        void OriginalCount(hstring value);
        hstring RenamedCount();
        void RenamedCount(hstring value);

    private:
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
