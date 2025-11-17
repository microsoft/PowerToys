#include "pch.h"
#include "ExplorerItem.h"
#if __has_include("ExplorerItem.g.cpp")
#include "ExplorerItem.g.cpp"
#endif

using namespace winrt;
using namespace Microsoft::UI::Xaml;
using namespace Microsoft::Windows::ApplicationModel::Resources;

namespace
{
    const wchar_t fileImagePath[] = L"ms-appx:///Assets/PowerRename/file.png";
    const wchar_t folderImagePath[] = L"ms-appx:///Assets/PowerRename/folder.png";

    std::wstring PowerRenameItemRenameStatusToString(PowerRenameItemRenameStatus status)
    {
        switch (status)
        {
        case PowerRenameItemRenameStatus::Init:
        {
            return L"Normal";
        }
        case PowerRenameItemRenameStatus::ShouldRename:
        {
            return L"Highlight";
        }
        case PowerRenameItemRenameStatus::ItemNameInvalidChar:
        {
            return L"Error";
        }
        case PowerRenameItemRenameStatus::ItemNameTooLong:
        {
            return L"Error";
        }
        default:
            return L"Normal";
        }
    }
}

namespace winrt::PowerRenameUI::implementation
{
    DependencyProperty ExplorerItem::_CheckedProperty{ nullptr };

    void ExplorerItem::_InitializeProperties()
    {
        if (!_CheckedProperty)
        {
            _CheckedProperty =
                DependencyProperty::Register(
                    L"Checked",
                    xaml_typename<bool>(),
                    xaml_typename<PowerRenameUI::ExplorerItem>(),
                    PropertyMetadata{ box_value(true) });
        }
    }

    ExplorerItem::ExplorerItem()
    {
        _InitializeProperties();
    }

    ExplorerItem::ExplorerItem(int32_t id, hstring const& original, hstring const& renamed, int32_t type, uint32_t depth, bool checked) :
        m_id{ id }, m_idStr{ std::to_wstring(id) }, m_original{ original }, m_renamed{ renamed }, m_type{ type }, m_depth{ depth }
    {
        _InitializeProperties();

        m_imagePath = (m_type == static_cast<UINT>(ExplorerItemType::Folder)) ? folderImagePath : fileImagePath;
        Checked(checked);
    }

    int32_t ExplorerItem::Id()
    {
        return m_id;
    }

    void ExplorerItem::Id(const int32_t value)
    {
        if (m_id != value)
        {
            m_id = value;
            m_propertyChanged(*this, Microsoft::UI::Xaml::Data::PropertyChangedEventArgs{ L"Id" });
        }
    }

    hstring ExplorerItem::IdStr()
    {
        return m_idStr;
    }

    void ExplorerItem::IdStr(hstring const& value)
    {
        if (m_idStr != value)
        {
            m_idStr = value;
            m_propertyChanged(*this, Microsoft::UI::Xaml::Data::PropertyChangedEventArgs{ L"IdStr" });
        }
    }

    hstring ExplorerItem::Original()
    {
        return m_original;
    }

    void ExplorerItem::Original(hstring const& value)
    {
        if (m_original != value)
        {
            m_original = value;
            m_propertyChanged(*this, Microsoft::UI::Xaml::Data::PropertyChangedEventArgs{ L"Original" });
        }
    }

    hstring ExplorerItem::Renamed()
    {
        return m_renamed;
    }

    void ExplorerItem::Renamed(hstring const& value)
    {
        if (m_renamed != value)
        {
            m_renamed = value;
            m_propertyChanged(*this, Microsoft::UI::Xaml::Data::PropertyChangedEventArgs{ L"Renamed" });
        }
    }

    double ExplorerItem::Indentation()
    {
        return static_cast<double>(m_depth) * 12;
    }

    void ExplorerItem::Indentation(double value)
    {
        if (m_depth != value)
        {
            m_depth = static_cast<uint32_t>(value / 12);
            m_propertyChanged(*this, Microsoft::UI::Xaml::Data::PropertyChangedEventArgs{ L"Indentation" });
        }
    }

    hstring ExplorerItem::ImagePath()
    {
        return m_imagePath;
    }

    void ExplorerItem::ImagePath(hstring const& value)
    {
        if (m_imagePath != value)
        {
            m_imagePath = value;
            m_propertyChanged(*this, Microsoft::UI::Xaml::Data::PropertyChangedEventArgs{ L"ImagePath" });
        }
    }

    int32_t ExplorerItem::Type()
    {
        return m_type;
    }

    void ExplorerItem::Type(int32_t value)
    {
        if (m_type != value)
        {
            m_type = value;
            m_propertyChanged(*this, Microsoft::UI::Xaml::Data::PropertyChangedEventArgs{ L"Type" });
        }
    }

    int32_t ExplorerItem::State()
    {
        return static_cast<int32_t>(m_state);
    }

    void ExplorerItem::State(int32_t value)
    {
        m_state = static_cast<PowerRenameItemRenameStatus>(value);
        ErrorMessageTxt().Text(StateToErrorMessage());

        if (m_renamed == L"")
        {
            VisualStateManager::GoToState(*this, L"Normal", false);
        }
        else
        {
            VisualStateManager::GoToState(*this, PowerRenameItemRenameStatusToString(m_state), false);
        }
    }

    winrt::event_token ExplorerItem::PropertyChanged(winrt::Microsoft::UI::Xaml::Data::PropertyChangedEventHandler const& handler)
    {
        return m_propertyChanged.add(handler);
    }

    void ExplorerItem::PropertyChanged(winrt::event_token const& token) noexcept
    {
        m_propertyChanged.remove(token);
    }

    std::wstring ExplorerItem::StateToErrorMessage()
    {
        static auto factory = winrt::get_activation_factory<ResourceManager, IResourceManagerFactory>();
        static ResourceManager manager = factory.CreateInstance(L"PowerToys.PowerRename.pri");
        static auto invalid_char_error = manager.MainResourceMap().GetValue(L"Resources/ErrorMessage_InvalidChar").ValueAsString();
        static auto name_too_long_error = manager.MainResourceMap().GetValue(L"Resources/ErrorMessage_FileNameTooLong").ValueAsString();

        switch (m_state)
        {
        case PowerRenameItemRenameStatus::ItemNameInvalidChar:
        {
            return std::wstring{ invalid_char_error };
        }
        case PowerRenameItemRenameStatus::ItemNameTooLong:
        {
            return std::wstring{ name_too_long_error };
        }
        default:
            return {};
        }
    }

}
