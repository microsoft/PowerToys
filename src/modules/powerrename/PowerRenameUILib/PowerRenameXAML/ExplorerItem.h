#pragma once

#include "winrt/Microsoft.UI.Xaml.h"
#include "winrt/Microsoft.UI.Xaml.Markup.h"
#include "winrt/Microsoft.UI.Xaml.Interop.h"
#include "winrt/Microsoft.UI.Xaml.Controls.Primitives.h"
#include "ExplorerItem.g.h"
#include "PowerRenameInterfaces.h"
#include "..\Utils.h"

namespace winrt::PowerRenameUI::implementation
{
    struct ExplorerItem : ExplorerItemT<ExplorerItem>
    {
        enum class ExplorerItemType
        {
            Folder = 0,
            File = 1
        };

        ExplorerItem();

        ExplorerItem(int32_t id, hstring const& original, hstring const& renamed, int32_t type, uint32_t depth, bool checked);
        int32_t Id();
        void Id(int32_t);

        hstring IdStr();
        void IdStr(hstring const& value);

        hstring Original();
        void Original(hstring const& value);

        hstring Renamed();
        void Renamed(hstring const& value);

        double Indentation();
        void Indentation(double value);

        hstring ImagePath();
        void ImagePath(hstring const& value);
        int32_t Type();
        void Type(int32_t value);
        int32_t State();
        void State(int32_t value);
        winrt::event_token PropertyChanged(winrt::Microsoft::UI::Xaml::Data::PropertyChangedEventHandler const& handler);
        void PropertyChanged(winrt::event_token const& token) noexcept;

        DEPENDENCY_PROPERTY(bool, Checked);

    private:

        static void _InitializeProperties();

        std::wstring StateToErrorMessage();

        int32_t m_id{};
        hstring m_idStr;
        winrt::hstring m_original;
        winrt::hstring m_renamed;
        uint32_t m_depth{};
        hstring m_imagePath;
        int32_t m_type{};
        PowerRenameItemRenameStatus m_state{};
        winrt::event<Microsoft::UI::Xaml::Data::PropertyChangedEventHandler> m_propertyChanged;
    };
}

namespace winrt::PowerRenameUI::factory_implementation
{
    struct ExplorerItem : ExplorerItemT<ExplorerItem, implementation::ExplorerItem>
    {
    };
}
