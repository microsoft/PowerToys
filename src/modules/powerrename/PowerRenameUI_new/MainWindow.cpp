#include "pch.h"
#include "MainWindow.h"
#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif
#include <winuser.h>

using namespace winrt;
using namespace Windows::UI::Xaml;

namespace winrt::PowerRenameUI_new::implementation
{
    MainWindow::MainWindow() :
        m_allSelected{ true }
    {
        m_searchMRU = winrt::single_threaded_observable_vector<hstring>();
        m_replaceMRU = winrt::single_threaded_observable_vector<hstring>();

        m_explorerItems = winrt::single_threaded_observable_vector<PowerRenameUI_new::ExplorerItem>();

        m_searchRegExShortcuts = winrt::single_threaded_observable_vector<PowerRenameUI_new::PatternSnippet>();
        auto resourceLoader{ Windows::ApplicationModel::Resources::ResourceLoader::GetForCurrentView() };

        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"\\.", resourceLoader.GetString(L"RegExCheatSheet_MatchAny")));
        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"\\d", resourceLoader.GetString(L"RegExCheatSheet_MatchDigit")));
        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"\\D", resourceLoader.GetString(L"RegExCheatSheet_MatchNonDigit")));
        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"\\w", resourceLoader.GetString(L"RegExCheatSheet_MatchNonWS")));
        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"\\S", resourceLoader.GetString(L"RegExCheatSheet_MatchWordChar")));
        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"\\S+", resourceLoader.GetString(L"RegExCheatSheet_MatchSeveralWS")));
        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"\\b", resourceLoader.GetString(L"RegExCheatSheet_MatchWordBoundary")));

        m_dateTimeShortcuts = winrt::single_threaded_observable_vector<PowerRenameUI_new::PatternSnippet>();
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$YYYY", resourceLoader.GetString(L"DateTimeCheatSheet_FullYear")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$YY", resourceLoader.GetString(L"DateTimeCheatSheet_YearLastTwoDigits")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$Y", resourceLoader.GetString(L"DateTimeCheatSheet_YearLastDigit")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$MMMM", resourceLoader.GetString(L"DateTimeCheatSheet_MonthName")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$MMM", resourceLoader.GetString(L"DateTimeCheatSheet_MonthNameAbbr")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$MM", resourceLoader.GetString(L"DateTimeCheatSheet_MonthDigitLZero")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$M", resourceLoader.GetString(L"DateTimeCheatSheet_MonthDigit")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$DDDD", resourceLoader.GetString(L"DateTimeCheatSheet_DayName")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$DDD", resourceLoader.GetString(L"DateTimeCheatSheet_DayNameAbbr")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$DD", resourceLoader.GetString(L"DateTimeCheatSheet_DayDigitLZero")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$D", resourceLoader.GetString(L"DateTimeCheatSheet_DayDigit")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$hh", resourceLoader.GetString(L"DateTimeCheatSheet_HoursLZero")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$h", resourceLoader.GetString(L"DateTimeCheatSheet_Hours")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$mm", resourceLoader.GetString(L"DateTimeCheatSheet_MinutesLZero")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$m", resourceLoader.GetString(L"DateTimeCheatSheet_Minutes")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$ss", resourceLoader.GetString(L"DateTimeCheatSheet_SecondsLZero")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$s", resourceLoader.GetString(L"DateTimeCheatSheet_Seconds")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$fff", resourceLoader.GetString(L"DateTimeCheatSheet_MilliSeconds3D")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$ff", resourceLoader.GetString(L"DateTimeCheatSheet_MilliSeconds2D")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUI_new::implementation::PatternSnippet>(L"$f", resourceLoader.GetString(L"DateTimeCheatSheet_MilliSeconds1D")));

        InitializeComponent();
    }

    Windows::Foundation::Collections::IObservableVector<hstring> MainWindow::SearchMRU()
    {
        return m_searchMRU;
    }

    Windows::Foundation::Collections::IObservableVector<hstring> MainWindow::ReplaceMRU()
    {
        return m_replaceMRU;
    }
    
    winrt::Windows::Foundation::Collections::IObservableVector<winrt::PowerRenameUI_new::ExplorerItem> MainWindow::ExplorerItems()
    {
        return m_explorerItems;
    }

    winrt::Windows::Foundation::Collections::IObservableVector<winrt::PowerRenameUI_new::PatternSnippet> MainWindow::SearchRegExShortcuts()
    {
        return m_searchRegExShortcuts;
    }

    winrt::Windows::Foundation::Collections::IObservableVector<winrt::PowerRenameUI_new::PatternSnippet> MainWindow::DateTimeShortcuts()
    {
        return m_dateTimeShortcuts;
    }

    Windows::UI::Xaml::Controls::AutoSuggestBox MainWindow::AutoSuggestBoxSearch()
    {
        return textBox_search();
    }

    Windows::UI::Xaml::Controls::AutoSuggestBox MainWindow::AutoSuggestBoxReplace()
    {
        return textBox_replace();
    }

    Windows::UI::Xaml::Controls::CheckBox MainWindow::ChckBoxRegex()
    {
        return chckBox_regex();
    }

    Windows::UI::Xaml::Controls::CheckBox MainWindow::ChckBoxCaseSensitive()
    {
        return chckBox_case();
    }

    Windows::UI::Xaml::Controls::CheckBox MainWindow::ChckBoxMatchAll()
    {
        return chckBox_matchAll();
    }

    Windows::UI::Xaml::Controls::ComboBox MainWindow::ComboBoxRenameParts()
    {
        return comboBox_renameParts();
    }

    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::TglBtnIncludeFiles()
    {
        return tglBtn_includeFiles();
    }

    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::TglBtnIncludeFolders()
    {
        return tglBtn_includeFolders();
    }

    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::TglBtnIncludeSubfolders()
    {
        return tglBtn_includeSubfolders();
    }

    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::TglBtnEnumerateItems()
    {
        return tglBtn_enumItems();
    }

    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::TglBtnUpperCase()
    {
        return tglBtn_upperCase();
    }
    
    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::TglBtnLowerCase()
    {
        return tglBtn_lowerCase();
    }
    
    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::TglBtnTitleCase()
    {
        return tglBtn_titleCase();
    }

    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::TglBtnCapitalize()
    {
        return tglBtn_capitalize();
    }

    Windows::UI::Xaml::Controls::Button MainWindow::BtnSettings()
    {
        return btn_settings();
    }

    Windows::UI::Xaml::Controls::CheckBox MainWindow::ChckBoxSelectAll()
    {
        return chckBox_selectAll();
    }

    PowerRenameUI_new::UIUpdates MainWindow::UIUpdatesItem()
    {
        return m_uiUpdatesItem;
    }

    void MainWindow::AddExplorerItem(int32_t id, hstring const& original, hstring const& renamed, int32_t type, int32_t parentId, bool checked)
    {
        auto newItem = winrt::make<PowerRenameUI_new::implementation::ExplorerItem>(id, original, renamed, type, checked);
        if (parentId == 0)
        {
            m_explorerItems.Append(newItem);
        }
        else
        {
            auto parent = FindById(parentId);
            parent.Children().Append(newItem);
        }
    }

    void MainWindow::UpdateExplorerItem(int32_t id, hstring const& newName)
    {        
        auto itemToUpdate = FindById(id);
        if (itemToUpdate != NULL)
        {
            itemToUpdate.Renamed(newName);
        }
    }

    void MainWindow::UpdateRenamedExplorerItem(int32_t id, hstring const& newOriginalName)
    {
        auto itemToUpdate = FindById(id);
        if (itemToUpdate != NULL)
        {
            itemToUpdate.Original(newOriginalName);
            itemToUpdate.Renamed(L"");
        }
    }

    void MainWindow::AppendSearchMRU(hstring const& value)
    {
        m_searchMRU.Append(value);
    }

    void MainWindow::AppendReplaceMRU(hstring const& value)
    {
        m_replaceMRU.Append(value);
    }

    PowerRenameUI_new::ExplorerItem MainWindow::FindById(int32_t id)
    {
        auto fakeRoot = winrt::make<PowerRenameUI_new::implementation::ExplorerItem>(0, L"Fake", L"", 0, false);
        fakeRoot.Children(m_explorerItems);
        return FindById(fakeRoot, id);
    }

    PowerRenameUI_new::ExplorerItem MainWindow::FindById(PowerRenameUI_new::ExplorerItem& root, int32_t id)
    {
        if (root.Id() == id)
            return root;

        if (root.Type() == static_cast<UINT>(ExplorerItem::ExplorerItemType::Folder))
        {
            for (auto c : root.Children())
            {
                auto result = FindById(c, id);
                if (result != NULL)
                    return result;
            }
        }

        return NULL;
    }

    void MainWindow::ToggleAll(PowerRenameUI_new::ExplorerItem node, bool checked)
    {
        if (node == NULL)
            return;

        node.Checked(checked);

        if (node.Type() == static_cast<UINT>(ExplorerItem::ExplorerItemType::Folder))
        {
            for (auto c : node.Children())
            {
                ToggleAll(c, checked);
            }
        }
    }

    void MainWindow::Checked_ids(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const&)
    {
        auto checkbox = sender.as<Windows::UI::Xaml::Controls::CheckBox>();
        auto id = std::stoi(std::wstring{ checkbox.Name() });
        auto item = FindById(id);
        if (checkbox.IsChecked().GetBoolean() != item.Checked())
        {
            m_uiUpdatesItem.Checked(checkbox.IsChecked().GetBoolean());
            m_uiUpdatesItem.ChangedExplorerItemId(id);
        }
    }

    void MainWindow::SelectAll(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::RoutedEventArgs const&)
    {
        if (chckBox_selectAll().IsChecked().GetBoolean() != m_allSelected)
        {
            auto fakeRoot = winrt::make<PowerRenameUI_new::implementation::ExplorerItem>(0, L"Fake", L"", 0, false);
            fakeRoot.Children(m_explorerItems);
            ToggleAll(fakeRoot, chckBox_selectAll().IsChecked().GetBoolean());
            m_uiUpdatesItem.ToggleAll();
            m_allSelected = !m_allSelected;
        }
    }

    void MainWindow::ShowAll(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::RoutedEventArgs const&)
    {
        btn_showAll().IsChecked(true);
        btn_showRenamed().IsChecked(false);
        if (!m_uiUpdatesItem.ShowAll())
        {
            m_explorerItems.Clear();
            m_uiUpdatesItem.ShowAll(true);
        }
    }

    void MainWindow::ShowRenamed(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::RoutedEventArgs const&)
    {
        btn_showRenamed().IsChecked(true);
        btn_showAll().IsChecked(false);
        if (m_uiUpdatesItem.ShowAll())
        {
            m_explorerItems.Clear();
            m_uiUpdatesItem.ShowAll(false);
        }
    }

    void MainWindow::RegExItemClick(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::Controls::ItemClickEventArgs const& e)
    {
        auto s = e.ClickedItem().try_as<PatternSnippet>();
        RegExFlyout().Hide();
        textBox_search().Text(textBox_search().Text() + s->Code());
    }

    void MainWindow::DateTimeItemClick(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::Controls::ItemClickEventArgs const& e)
    {
        auto s = e.ClickedItem().try_as<PatternSnippet>();
        DateTimeFlyout().Hide();
        textBox_replace().Text(textBox_replace().Text() + s->Code());
    }

    void MainWindow::btn_rename_Click(winrt::Microsoft::UI::Xaml::Controls::SplitButton const&, winrt::Microsoft::UI::Xaml::Controls::SplitButtonClickEventArgs const&)
    {
        m_uiUpdatesItem.CloseUIWindow(false);
        m_uiUpdatesItem.Rename();
    }

    void MainWindow::MenuFlyoutItem_Click(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::RoutedEventArgs const&)
    {
        m_uiUpdatesItem.CloseUIWindow(true);
        m_uiUpdatesItem.Rename();
    }
}
