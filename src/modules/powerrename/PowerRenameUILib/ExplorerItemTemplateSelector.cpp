#include "pch.h"
#include "ExplorerItemTemplateSelector.h"
#include "ExplorerItemTemplateSelector.g.cpp"

namespace winrt::PowerRenameUILib::implementation
{
    Windows::UI::Xaml::DataTemplate ExplorerItemTemplateSelector::SelectTemplateCore(IInspectable const& item)
    {
        ExplorerItem explorerItem = (ExplorerItem&)item;
        return explorerItem.Type() == 0 ? m_folderTemplate : m_fileTemplate;
    }

    Windows::UI::Xaml::DataTemplate ExplorerItemTemplateSelector::SelectTemplateCore(IInspectable const&, Windows::UI::Xaml::DependencyObject const&)
    {
        return Windows::UI::Xaml::DataTemplate();
    }

    winrt::Windows::UI::Xaml::DataTemplate ExplorerItemTemplateSelector::FolderTemplate()
    {
        return m_folderTemplate;
    }

    void ExplorerItemTemplateSelector::FolderTemplate(winrt::Windows::UI::Xaml::DataTemplate const& value)
    {
        m_folderTemplate = value;
    }

    winrt::Windows::UI::Xaml::DataTemplate ExplorerItemTemplateSelector::FileTemplate()
    {
        return m_fileTemplate;
    }

    void ExplorerItemTemplateSelector::FileTemplate(winrt::Windows::UI::Xaml::DataTemplate const& value)
    {
        m_fileTemplate = value;
    }
}
