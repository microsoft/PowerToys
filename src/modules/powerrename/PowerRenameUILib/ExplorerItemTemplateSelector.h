#pragma once
#include "ExplorerItemTemplateSelector.g.h"

namespace winrt::PowerRenameUILib::implementation
{
    struct ExplorerItemTemplateSelector : ExplorerItemTemplateSelectorT<ExplorerItemTemplateSelector>
    {
        ExplorerItemTemplateSelector() = default;

        Windows::UI::Xaml::DataTemplate SelectTemplateCore(IInspectable const&);
        Windows::UI::Xaml::DataTemplate SelectTemplateCore(IInspectable const&, Windows::UI::Xaml::DependencyObject const&);

        winrt::Windows::UI::Xaml::DataTemplate FolderTemplate();
        void FolderTemplate(winrt::Windows::UI::Xaml::DataTemplate const& value);
        winrt::Windows::UI::Xaml::DataTemplate FileTemplate();
        void FileTemplate(winrt::Windows::UI::Xaml::DataTemplate const& value);

    private:
        Windows::UI::Xaml::DataTemplate m_folderTemplate{ nullptr };
        Windows::UI::Xaml::DataTemplate m_fileTemplate{ nullptr };
    };
}
namespace winrt::PowerRenameUILib::factory_implementation
{
    struct ExplorerItemTemplateSelector : ExplorerItemTemplateSelectorT<ExplorerItemTemplateSelector, implementation::ExplorerItemTemplateSelector>
    {
    };
}
