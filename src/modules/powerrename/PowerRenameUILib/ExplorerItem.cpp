#include "pch.h"
#include "ExplorerItem.h"
#include "ExplorerItem.g.cpp"

namespace {
    const wchar_t fileImagePath[] = L"ms-appx:///Assets/file.png";
    const wchar_t folderImagePath[] = L"ms-appx:///Assets/folder.png";
}

namespace winrt::PowerRenameUILib::implementation
{
    ExplorerItem::ExplorerItem(int32_t id, hstring const& original, hstring const& renamed, int32_t type, uint32_t depth, bool checked) :
        m_id{ id }, m_idStr{ std::to_wstring(id) }, m_original{ original }, m_renamed{ renamed }, m_type{ type }, m_depth{ depth }, m_checked{ checked }
    {
        m_imagePath = (m_type == static_cast<UINT>(ExplorerItemType::Folder)) ? folderImagePath : fileImagePath;
        m_highlight = m_checked && !m_renamed.empty() ? Windows::UI::Xaml::Visibility::Visible : Windows::UI::Xaml::Visibility::Collapsed;
    }

    int32_t ExplorerItem::Id()
    {
        return m_id;
    }

    hstring ExplorerItem::IdStr()
    {
        return m_idStr;
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
            m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"Original" });
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
            m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"Renamed" });

            auto visibility = m_checked && !m_renamed.empty() ? Windows::UI::Xaml::Visibility::Visible : Windows::UI::Xaml::Visibility::Collapsed;
            if (m_highlight != visibility)
            {
                m_highlight = visibility;
                m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"Highlight" });
            }
        }
    }

    double ExplorerItem::Indentation() {
        return static_cast<double>(m_depth) * 12;
    }

    hstring ExplorerItem::ImagePath()
    {
        return m_imagePath;
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
            m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"Type" });
        }
    }

    bool ExplorerItem::Checked()
    {
        return m_checked;
    }

    void ExplorerItem::Checked(bool value)
    {
        if (m_checked != value)
        {
            m_checked = value;
            m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"Checked" });

            auto visibility = m_checked && !m_renamed.empty() ? Windows::UI::Xaml::Visibility::Visible : Windows::UI::Xaml::Visibility::Collapsed;
            if (m_highlight != visibility)
            {
                m_highlight = visibility;
                m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"Highlight" });
            }
        }
    }

    winrt::Windows::UI::Xaml::Visibility ExplorerItem::Highlight()
    {
        return m_highlight;
    }

    winrt::event_token ExplorerItem::PropertyChanged(winrt::Windows::UI::Xaml::Data::PropertyChangedEventHandler const& handler)
    {
        return m_propertyChanged.add(handler);
    }

    void ExplorerItem::PropertyChanged(winrt::event_token const& token) noexcept
    {
        m_propertyChanged.remove(token);
    }
}
