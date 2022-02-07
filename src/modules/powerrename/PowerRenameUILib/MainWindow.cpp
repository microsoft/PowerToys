#include "pch.h"
#include "MainWindow.h"
#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

using namespace winrt;
using namespace Windows::UI::Xaml;

namespace winrt::PowerRenameUILib::implementation
{
    MainWindow::MainWindow() :
        m_allSelected{ true }
    {
        m_searchMRU = winrt::single_threaded_observable_vector<hstring>();
        m_replaceMRU = winrt::single_threaded_observable_vector<hstring>();

        m_explorerItems = winrt::single_threaded_observable_vector<PowerRenameUILib::ExplorerItem>();

        m_searchRegExShortcuts = winrt::single_threaded_observable_vector<PowerRenameUILib::PatternSnippet>();
        auto resourceLoader{ Windows::ApplicationModel::Resources::ResourceLoader::GetForCurrentView() };

        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L".", resourceLoader.GetString(L"RegExCheatSheet_MatchAny")));
        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"\\d", resourceLoader.GetString(L"RegExCheatSheet_MatchDigit")));
        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"\\D", resourceLoader.GetString(L"RegExCheatSheet_MatchNonDigit")));
        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"\\w", resourceLoader.GetString(L"RegExCheatSheet_MatchNonWS")));
        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"\\S", resourceLoader.GetString(L"RegExCheatSheet_MatchWordChar")));
        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"\\S+", resourceLoader.GetString(L"RegExCheatSheet_MatchOneOrMoreWS")));
        m_searchRegExShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"\\b", resourceLoader.GetString(L"RegExCheatSheet_MatchWordBoundary")));

        m_dateTimeShortcuts = winrt::single_threaded_observable_vector<PowerRenameUILib::PatternSnippet>();
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$YYYY", resourceLoader.GetString(L"DateTimeCheatSheet_FullYear")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$YY", resourceLoader.GetString(L"DateTimeCheatSheet_YearLastTwoDigits")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$Y", resourceLoader.GetString(L"DateTimeCheatSheet_YearLastDigit")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$MMMM", resourceLoader.GetString(L"DateTimeCheatSheet_MonthName")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$MMM", resourceLoader.GetString(L"DateTimeCheatSheet_MonthNameAbbr")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$MM", resourceLoader.GetString(L"DateTimeCheatSheet_MonthDigitLZero")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$M", resourceLoader.GetString(L"DateTimeCheatSheet_MonthDigit")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$DDDD", resourceLoader.GetString(L"DateTimeCheatSheet_DayName")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$DDD", resourceLoader.GetString(L"DateTimeCheatSheet_DayNameAbbr")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$DD", resourceLoader.GetString(L"DateTimeCheatSheet_DayDigitLZero")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$D", resourceLoader.GetString(L"DateTimeCheatSheet_DayDigit")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$hh", resourceLoader.GetString(L"DateTimeCheatSheet_HoursLZero")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$h", resourceLoader.GetString(L"DateTimeCheatSheet_Hours")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$mm", resourceLoader.GetString(L"DateTimeCheatSheet_MinutesLZero")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$m", resourceLoader.GetString(L"DateTimeCheatSheet_Minutes")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$ss", resourceLoader.GetString(L"DateTimeCheatSheet_SecondsLZero")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$s", resourceLoader.GetString(L"DateTimeCheatSheet_Seconds")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$fff", resourceLoader.GetString(L"DateTimeCheatSheet_MilliSeconds3D")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$ff", resourceLoader.GetString(L"DateTimeCheatSheet_MilliSeconds2D")));
        m_dateTimeShortcuts.Append(winrt::make<PowerRenameUILib::implementation::PatternSnippet>(L"$f", resourceLoader.GetString(L"DateTimeCheatSheet_MilliSeconds1D")));

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
    
    winrt::Windows::Foundation::Collections::IObservableVector<winrt::PowerRenameUILib::ExplorerItem> MainWindow::ExplorerItems()
    {
        return m_explorerItems;
    }

    winrt::Windows::Foundation::Collections::IObservableVector<winrt::PowerRenameUILib::PatternSnippet> MainWindow::SearchRegExShortcuts()
    {
        return m_searchRegExShortcuts;
    }

    winrt::Windows::Foundation::Collections::IObservableVector<winrt::PowerRenameUILib::PatternSnippet> MainWindow::DateTimeShortcuts()
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

    Windows::UI::Xaml::Controls::CheckBox MainWindow::CheckBoxRegex()
    {
        return checkBox_regex();
    }

    Windows::UI::Xaml::Controls::CheckBox MainWindow::CheckBoxCaseSensitive()
    {
        return checkBox_case();
    }

    Windows::UI::Xaml::Controls::CheckBox MainWindow::CheckBoxMatchAll()
    {
        return checkBox_matchAll();
    }

    Windows::UI::Xaml::Controls::ComboBox MainWindow::ComboBoxRenameParts()
    {
        return comboBox_renameParts();
    }

    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::ToggleButtonIncludeFiles()
    {
        return toggleButton_includeFiles();
    }

    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::ToggleButtonIncludeFolders()
    {
        return toggleButton_includeFolders();
    }

    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::ToggleButtonIncludeSubfolders()
    {
        return toggleButton_includeSubfolders();
    }

    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::ToggleButtonEnumerateItems()
    {
        return toggleButton_enumItems();
    }

    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::ToggleButtonUpperCase()
    {
        return toggleButton_upperCase();
    }
    
    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::ToggleButtonLowerCase()
    {
        return toggleButton_lowerCase();
    }
    
    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::ToggleButtonTitleCase()
    {
        return toggleButton_titleCase();
    }

    Windows::UI::Xaml::Controls::Primitives::ToggleButton MainWindow::ToggleButtonCapitalize()
    {
        return toggleButton_capitalize();
    }

    Windows::UI::Xaml::Controls::Button MainWindow::ButtonSettings()
    {
        return button_settings();
    }

    Windows::UI::Xaml::Controls::CheckBox MainWindow::CheckBoxSelectAll()
    {
        return checkBox_selectAll();
    }

    PowerRenameUILib::UIUpdates MainWindow::UIUpdatesItem()
    {
        return m_uiUpdatesItem;
    }

    void MainWindow::AddExplorerItem(int32_t id, hstring const& original, hstring const& renamed, int32_t type, uint32_t depth, bool checked)
    {
        auto newItem = winrt::make<PowerRenameUILib::implementation::ExplorerItem>(id, original, renamed, type, depth, checked);
        m_explorerItems.Append(newItem);
        m_explorerItemsMap[id] = newItem;
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

    PowerRenameUILib::ExplorerItem MainWindow::FindById(int32_t id)
    {
        return m_explorerItemsMap.contains(id) ? m_explorerItemsMap[id] : NULL;
    }

    void MainWindow::ToggleAll(bool checked)
    {
        std::for_each(m_explorerItems.begin(), m_explorerItems.end(), [checked](auto item) { item.Checked(checked); });
    }

    void MainWindow::Checked_ids(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::RoutedEventArgs const&)
    {
        auto checkbox = sender.as<Windows::UI::Xaml::Controls::CheckBox>();
        auto id = std::stoi(std::wstring{ checkbox.Name() });
        auto item = FindById(id);
        if (item != NULL && checkbox.IsChecked().GetBoolean() != item.Checked())
        {
            m_uiUpdatesItem.Checked(checkbox.IsChecked().GetBoolean());
            m_uiUpdatesItem.ChangedExplorerItemId(id);
        }
    }

    void MainWindow::SelectAll(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::RoutedEventArgs const&)
    {
        if (checkBox_selectAll().IsChecked().GetBoolean() != m_allSelected)
        {
            ToggleAll(checkBox_selectAll().IsChecked().GetBoolean());
            m_uiUpdatesItem.ToggleAll();
            m_allSelected = !m_allSelected;
        }
    }

    void MainWindow::ShowAll(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::RoutedEventArgs const&)
    {
        button_showAll().IsChecked(true);
        button_showRenamed().IsChecked(false);
        if (!m_uiUpdatesItem.ShowAll())
        {
            m_explorerItems.Clear();
            m_explorerItemsMap.clear();
            m_uiUpdatesItem.ShowAll(true);
        }
    }

    void MainWindow::ShowRenamed(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::RoutedEventArgs const&)
    {
        button_showRenamed().IsChecked(true);
        button_showAll().IsChecked(false);
        if (m_uiUpdatesItem.ShowAll())
        {
            m_explorerItems.Clear();
            m_explorerItemsMap.clear();
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

    void MainWindow::button_rename_Click(winrt::Microsoft::UI::Xaml::Controls::SplitButton const&, winrt::Microsoft::UI::Xaml::Controls::SplitButtonClickEventArgs const&)
    {
        m_uiUpdatesItem.CloseUIWindow(false);
        m_uiUpdatesItem.Rename();
    }

    void MainWindow::MenuFlyoutItem_Click(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::RoutedEventArgs const&)
    {
        m_uiUpdatesItem.CloseUIWindow(true);
        m_uiUpdatesItem.Rename();
    }

    void MainWindow::OpenDocs(winrt::Windows::Foundation::IInspectable const&, winrt::Windows::UI::Xaml::RoutedEventArgs const&)
    {
        Windows::System::Launcher::LaunchUriAsync(winrt::Windows::Foundation::Uri{ L"https://aka.ms/PowerToysOverview_PowerRename" });
    }
}
