#pragma once
#include "ExplorerItem.g.h"

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
        Microsoft::UI::Xaml::Visibility Highlight();
        Windows::Foundation::Collections::IObservableVector<PowerRenameUI::ExplorerItem> Children();
        winrt::event_token PropertyChanged(Microsoft::UI::Xaml::Data::PropertyChangedEventHandler const& handler);
        void PropertyChanged(winrt::event_token const& token) noexcept;
    
    private:
        int32_t m_id;
        hstring m_idStr;
        winrt::hstring m_original;
        winrt::hstring m_renamed;
        uint32_t m_depth;
        hstring m_imagePath;
        int32_t m_type;
        bool m_checked;
        Microsoft::UI::Xaml::Visibility m_highlight;
        winrt::event<Microsoft::UI::Xaml::Data::PropertyChangedEventHandler> m_propertyChanged;
    };
}
namespace winrt::PowerRenameUI::factory_implementation
{
    struct ExplorerItem : ExplorerItemT<ExplorerItem, implementation::ExplorerItem>
    {
    };
}
