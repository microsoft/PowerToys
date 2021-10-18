#pragma once

#include "winrt/Windows.UI.Xaml.h"
#include "winrt/Windows.UI.Xaml.Markup.h"
#include "winrt/Windows.UI.Xaml.Interop.h"
#include "winrt/Windows.UI.Xaml.Controls.Primitives.h"
#include "MainWindow.g.h"
#include "PatternSnippet.h"
#include "ExplorerItem.h"
#include "ExplorerItemTemplateSelector.h"

namespace winrt::PowerRenameUI_new::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        MainWindow();

        Windows::Foundation::Collections::IObservableVector<hstring> SearchMRU();
        Windows::Foundation::Collections::IObservableVector<hstring> ReplaceMRU();
        winrt::Windows::Foundation::Collections::IObservableVector<PowerRenameUI_new::ExplorerItem> ExplorerItems();
        winrt::Windows::Foundation::Collections::IObservableVector<PowerRenameUI_new::PatternSnippet> SearchRegExShortcuts();
        winrt::Windows::Foundation::Collections::IObservableVector<PowerRenameUI_new::PatternSnippet> DateTimeShortcuts();

        Windows::UI::Xaml::Controls::AutoSuggestBox AutoSuggestBoxSearch();
        Windows::UI::Xaml::Controls::AutoSuggestBox AutoSuggestBoxReplace();

        Windows::UI::Xaml::Controls::CheckBox CheckBoxRegex();
        Windows::UI::Xaml::Controls::CheckBox CheckBoxCaseSensitive();
        Windows::UI::Xaml::Controls::CheckBox CheckBoxMatchAll();

        Windows::UI::Xaml::Controls::ComboBox ComboBoxRenameParts();

        Windows::UI::Xaml::Controls::Primitives::ToggleButton ToggleButtonIncludeFiles();
        Windows::UI::Xaml::Controls::Primitives::ToggleButton ToggleButtonIncludeFolders();
        Windows::UI::Xaml::Controls::Primitives::ToggleButton ToggleButtonIncludeSubfolders();

        Windows::UI::Xaml::Controls::Primitives::ToggleButton ToggleButtonUpperCase();
        Windows::UI::Xaml::Controls::Primitives::ToggleButton ToggleButtonLowerCase();
        Windows::UI::Xaml::Controls::Primitives::ToggleButton ToggleButtonTitleCase();
        Windows::UI::Xaml::Controls::Primitives::ToggleButton ToggleButtonCapitalize();

        Windows::UI::Xaml::Controls::Primitives::ToggleButton ToggleButtonEnumerateItems();

        Windows::UI::Xaml::Controls::Button ButtonSettings();

        Windows::UI::Xaml::Controls::CheckBox CheckBoxSelectAll();

        PowerRenameUI_new::UIUpdates UIUpdatesItem();

        void AddExplorerItem(int32_t id, hstring const& original, hstring const& renamed, int32_t type, int32_t parentId, bool checked);
        void UpdateExplorerItem(int32_t id, hstring const& newName);
        void UpdateRenamedExplorerItem(int32_t id, hstring const& newOriginalName);
        void AppendSearchMRU(hstring const& value);
        void AppendReplaceMRU(hstring const& value);

        void Checked_ids(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
        void SelectAll(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
        void ShowAll(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
        void ShowRenamed(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
    private:
        bool m_allSelected;
        PowerRenameUI_new::UIUpdates m_uiUpdatesItem;
        PowerRenameUI_new::ExplorerItem FindById(int32_t id);
        PowerRenameUI_new::ExplorerItem FindById(PowerRenameUI_new::ExplorerItem& root, int32_t id);
        void ToggleAll(PowerRenameUI_new::ExplorerItem node, bool checked);

        winrt::Windows::Foundation::Collections::IObservableVector<hstring> m_searchMRU;
        winrt::Windows::Foundation::Collections::IObservableVector<hstring> m_replaceMRU;
        winrt::Windows::Foundation::Collections::IObservableVector<PowerRenameUI_new::ExplorerItem> m_explorerItems;
        winrt::Windows::Foundation::Collections::IObservableVector<PowerRenameUI_new::PatternSnippet> m_searchRegExShortcuts;
        winrt::Windows::Foundation::Collections::IObservableVector<PowerRenameUI_new::PatternSnippet> m_dateTimeShortcuts;

    public:
        void RegExItemClick(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::Controls::ItemClickEventArgs const& e);
        void DateTimeItemClick(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::Controls::ItemClickEventArgs const& e);
        void button_rename_Click(winrt::Microsoft::UI::Xaml::Controls::SplitButton const& sender, winrt::Microsoft::UI::Xaml::Controls::SplitButtonClickEventArgs const& args);
        void MenuFlyoutItem_Click(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
        void OpenDocs(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const& e);
    };
}

namespace winrt::PowerRenameUI_new::factory_implementation
{
    struct MainWindow : MainWindowT<MainWindow, implementation::MainWindow>
    {
    };
}
