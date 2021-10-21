#include "pch.h"
#include "ExplorerItem.h"
#include "ExplorerItem.g.cpp"

namespace winrt::PowerRenameUILib::implementation
{
    ExplorerItem::ExplorerItem(int32_t id, hstring const& original, hstring const& renamed, int32_t type, bool checked) :
        m_id{ id }, m_idStr{ std::to_wstring(id) }, m_original{ original }, m_renamed{ renamed }, m_type{ type }, m_checked{ checked }
    {
        if (m_type == static_cast<UINT>(ExplorerItemType::Folder))
        {
            m_children = winrt::single_threaded_observable_vector<PowerRenameUILib::ExplorerItem>();
        }
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
        }
    }

    winrt::Windows::Foundation::Collections::IObservableVector<winrt::PowerRenameUILib::ExplorerItem> ExplorerItem::Children()
    {
        return m_children;
    }

    void ExplorerItem::Children(Windows::Foundation::Collections::IObservableVector<PowerRenameUILib::ExplorerItem> const& value)
    {
        if (m_children != value)
        {
            m_children = value;
            m_propertyChanged(*this, Windows::UI::Xaml::Data::PropertyChangedEventArgs{ L"Children" });
        }
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
