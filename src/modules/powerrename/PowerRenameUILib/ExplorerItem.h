#pragma once

#include "winrt/Microsoft.UI.Xaml.h"
#include "winrt/Microsoft.UI.Xaml.Markup.h"
#include "winrt/Microsoft.UI.Xaml.Interop.h"
#include "winrt/Microsoft.UI.Xaml.Controls.Primitives.h"
#include "ExplorerItem.g.h"
#include "PowerRenameInterfaces.h"

namespace winrt::PowerRenameUI::implementation
{
    struct ExplorerItem : ExplorerItemT<ExplorerItem>
    {
        enum class ExplorerItemType
        {
            Folder = 0,
            File = 1
        };

        ExplorerItem() = default;

        ExplorerItem(int32_t id, hstring const& original, hstring const& renamed, int32_t type, uint32_t depth, bool checked);
        int32_t Id();
        hstring IdStr();
        hstring Original();
        void Original(hstring const& value);
        hstring Renamed();
        void Renamed(hstring const& value);
        double Indentation();
        hstring ImagePath();
        int32_t Type();
        void Type(int32_t value);
        bool Checked();
        void Checked(bool value);
        int32_t State();
        void State(int32_t value);
        winrt::event_token PropertyChanged(winrt::Microsoft::UI::Xaml::Data::PropertyChangedEventHandler const& handler);
        void PropertyChanged(winrt::event_token const& token) noexcept;

    private:
        std::wstring StateToErrorMessage();

        int32_t m_id;
        hstring m_idStr;
        winrt::hstring m_original;
        winrt::hstring m_renamed;
        uint32_t m_depth;
        hstring m_imagePath;
        int32_t m_type;
        bool m_checked;
        PowerRenameItemRenameStatus m_state;
        winrt::event<Microsoft::UI::Xaml::Data::PropertyChangedEventHandler> m_propertyChanged;

    };
}

namespace winrt::PowerRenameUI::factory_implementation
{
    struct ExplorerItem : ExplorerItemT<ExplorerItem, implementation::ExplorerItem>
    {
    };
}
