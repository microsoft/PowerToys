#include "pch.h"
#include "MainWindow.xaml.h"
#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include <settings.h>
#include <trace.h>

#include <common/logger/call_tracer.h>
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/process_path.h>

#include <atlstr.h>
#include <exception>
#include <string>
#include <sstream>
#include <vector>

#include "microsoft.ui.xaml.window.h"
#include <winrt/Microsoft.UI.Interop.h>
#include <winrt/Microsoft.UI.Windowing.h>

using namespace winrt;
using namespace Windows::UI::Xaml;
using namespace winrt::Microsoft::Windows::ApplicationModel::Resources;

#define MAX_LOADSTRING 100

// Non-localizable
const std::wstring PowerRenameUIIco = L"PowerRenameUI.ico";
const wchar_t c_WindowClass[] = L"PowerRename";
HINSTANCE g_hostHInst;

extern std::vector<std::wstring> g_files;

namespace winrt::PowerRenameUI::implementation
{
    MainWindow::MainWindow() :
        m_instance{ nullptr }, m_allSelected{ true }, m_managerEvents{ this }
    {

        auto windowNative{ this->try_as<::IWindowNative>() };
        winrt::check_bool(windowNative);
        windowNative->get_WindowHandle(&m_window);
        Microsoft::UI::WindowId windowId =
            Microsoft::UI::GetWindowIdFromWindow(m_window);

        POINT cursorPosition{};
        if (GetCursorPos(&cursorPosition))
        {
            HMONITOR hMonitor = MonitorFromPoint(cursorPosition, MONITOR_DEFAULTTOPRIMARY);
            MONITORINFOEX monitorInfo;
            monitorInfo.cbSize = sizeof(MONITORINFOEX);
            GetMonitorInfo(hMonitor, &monitorInfo);
            RECT rect;
            if (GetWindowRect(m_window, &rect))
            {
                int width = rect.right - rect.left;
                int height = rect.bottom - rect.top;

                MoveWindow(m_window,
                             monitorInfo.rcWork.left + (monitorInfo.rcWork.right - monitorInfo.rcWork.left - width) / 2,
                             monitorInfo.rcWork.top + (monitorInfo.rcWork.bottom - monitorInfo.rcWork.top - height) / 2,
                             width,
                             height,
                             true);
            }
        }


        Microsoft::UI::Windowing::AppWindow appWindow =
            Microsoft::UI::Windowing::AppWindow::GetFromWindowId(windowId);
        appWindow.SetIcon(PowerRenameUIIco);

        Title(hstring{ L"PowerRename" });
        
        m_searchMRUList = winrt::single_threaded_observable_vector<hstring>();
        m_replaceMRUList = winrt::single_threaded_observable_vector<hstring>();

        m_explorerItems = winrt::single_threaded_observable_vector<PowerRenameUI::ExplorerItem>();

        m_searchRegExShortcuts = winrt::single_threaded_observable_vector<PowerRenameUI::PatternSnippet>();
        auto factory = winrt::get_activation_factory<ResourceManager, IResourceManagerFactory>();
        ResourceManager manager = factory.CreateInstance(L"resources.pri");

        m_searchRegExShortcuts.Append(winrt::make<PatternSnippet>(L".", manager.MainResourceMap().GetValue(L"Resources/RegExCheatSheet_MatchAny").ValueAsString()));
        m_searchRegExShortcuts.Append(winrt::make<PatternSnippet>(L"\\d", manager.MainResourceMap().GetValue(L"Resources/RegExCheatSheet_MatchDigit").ValueAsString()));
        m_searchRegExShortcuts.Append(winrt::make<PatternSnippet>(L"\\D", manager.MainResourceMap().GetValue(L"Resources/RegExCheatSheet_MatchNonDigit").ValueAsString()));
        m_searchRegExShortcuts.Append(winrt::make<PatternSnippet>(L"\\w", manager.MainResourceMap().GetValue(L"Resources/RegExCheatSheet_MatchNonWS").ValueAsString()));
        m_searchRegExShortcuts.Append(winrt::make<PatternSnippet>(L"\\S", manager.MainResourceMap().GetValue(L"Resources/RegExCheatSheet_MatchWordChar").ValueAsString()));
        m_searchRegExShortcuts.Append(winrt::make<PatternSnippet>(L"\\S+", manager.MainResourceMap().GetValue(L"Resources/RegExCheatSheet_MatchOneOrMoreWS").ValueAsString()));
        m_searchRegExShortcuts.Append(winrt::make<PatternSnippet>(L"\\b", manager.MainResourceMap().GetValue(L"Resources/RegExCheatSheet_MatchWordBoundary").ValueAsString()));

        m_dateTimeShortcuts = winrt::single_threaded_observable_vector<PowerRenameUI::PatternSnippet>();
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$YYYY", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_FullYear").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$YY", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_YearLastTwoDigits").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$Y", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_YearLastDigit").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$MMMM", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_MonthName").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$MMM", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_MonthNameAbbr").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$MM", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_MonthDigitLZero").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$M", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_MonthDigit").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$DDDD", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_DayName").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$DDD", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_DayNameAbbr").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$DD", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_DayDigitLZero").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$D", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_DayDigit").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$hh", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_HoursLZero").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$h", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_Hours").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$mm", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_MinutesLZero").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$m", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_Minutes").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$ss", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_SecondsLZero").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$s", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_Seconds").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$fff", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_MilliSeconds3D").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$ff", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_MilliSeconds2D").ValueAsString()));
        m_dateTimeShortcuts.Append(winrt::make<PatternSnippet>(L"$f", manager.MainResourceMap().GetValue(L"Resources/DateTimeCheatSheet_MilliSeconds1D").ValueAsString()));

        InitializeComponent();

        if (SUCCEEDED(CPowerRenameManager::s_CreateInstance(&m_prManager)))
        {
            // Create the factory for our items
            CComPtr<IPowerRenameItemFactory> prItemFactory;
            if (SUCCEEDED(CPowerRenameItem::s_CreateInstance(nullptr, IID_PPV_ARGS(&prItemFactory))))
            {
                if (SUCCEEDED(m_prManager->PutRenameItemFactory(prItemFactory)))
                {
                    if (SUCCEEDED(m_prManager->Advise(&m_managerEvents, &m_cookie)))
                    {
                        CComPtr<IShellItemArray> shellItemArray;
                        // To test PowerRename uncomment this line and update the path to
                        // your local (absolute or relative) path which you want to see in PowerRename
                        // g_files.push_back(<path>);

                        if (!g_files.empty())
                        {
                            if (SUCCEEDED(CreateShellItemArrayFromPaths(g_files, &shellItemArray)))
                            {
                                CComPtr<IEnumShellItems> enumShellItems;
                                if (SUCCEEDED(shellItemArray->EnumItems(&enumShellItems)))
                                {
                                    EnumerateShellItems(enumShellItems);
                                }
                            }
                        }
                        else
                        {
                            Logger::warn(L"No items selected to be renamed.");
                        }
                    }
                }
            }
            else
            {
                Logger::error(L"Error creating PowerRenameItemFactory");
            }
        }
        else
        {
            Logger::error(L"Error creating PowerRenameManager");
        }
        try
        {
            PopulateExplorerItems();
            UpdateCounts();
            SetHandlers();
            ReadSettings();
        }
        catch (std::exception e)
        {
            Logger::error("Exception thrown during explorer items population: {}", std::string{ e.what() });
        }

        m_uiUpdatesItem.ButtonRenameEnabled(false);
        InitAutoComplete();
        SearchReplaceChanged();
    }

    void MainWindow::AddExplorerItem(int32_t id, hstring const& original, hstring const& renamed, int32_t type, uint32_t depth, bool checked)
    {
        auto newItem = winrt::make<PowerRenameUI::implementation::ExplorerItem>(id, original, renamed, type, depth, checked);
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
        m_searchMRUList.Append(value);
    }

    void MainWindow::AppendReplaceMRU(hstring const& value)
    {
        m_replaceMRUList.Append(value);
    }

    PowerRenameUI::ExplorerItem MainWindow::FindById(int32_t id)
    {
        return m_explorerItemsMap.contains(id) ? m_explorerItemsMap[id] : NULL;
    }

    void MainWindow::ToggleAll(bool checked)
    {
        std::for_each(m_explorerItems.begin(), m_explorerItems.end(), [checked](auto item) { item.Checked(checked); });
    }

    void MainWindow::Checked_ids(winrt::Windows::Foundation::IInspectable const& sender, winrt::Microsoft::UI::Xaml::RoutedEventArgs const&)
    {
        auto checkbox = sender.as<Microsoft::UI::Xaml::Controls::CheckBox>();
        auto id = std::stoi(std::wstring{ checkbox.Name() });
        auto item = FindById(id);
        if (item != NULL && checkbox.IsChecked().GetBoolean() != item.Checked())
        {
            m_uiUpdatesItem.Checked(checkbox.IsChecked().GetBoolean());
            m_uiUpdatesItem.ChangedExplorerItemId(id);
        }
    }

    void MainWindow::SelectAll(winrt::Windows::Foundation::IInspectable const&, winrt::Microsoft::UI::Xaml::RoutedEventArgs const&)
    {
        if (checkBox_selectAll().IsChecked().GetBoolean() != m_allSelected)
        {
            ToggleAll(checkBox_selectAll().IsChecked().GetBoolean());
            m_uiUpdatesItem.ToggleAll();
            m_allSelected = !m_allSelected;
        }
    }

    void MainWindow::ShowAll(winrt::Windows::Foundation::IInspectable const&, winrt::Microsoft::UI::Xaml::RoutedEventArgs const&)
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

    void MainWindow::ShowRenamed(winrt::Windows::Foundation::IInspectable const&, winrt::Microsoft::UI::Xaml::RoutedEventArgs const&)
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

    void MainWindow::RegExItemClick(winrt::Windows::Foundation::IInspectable const&, winrt::Microsoft::UI::Xaml::Controls::ItemClickEventArgs const& e)
    {
        auto s = e.ClickedItem().try_as<PatternSnippet>();
    }

    void MainWindow::DateTimeItemClick(winrt::Windows::Foundation::IInspectable const&, winrt::Microsoft::UI::Xaml::Controls::ItemClickEventArgs const& e)
    {
        auto s = e.ClickedItem().try_as<PatternSnippet>();
    }

    void MainWindow::button_rename_Click(winrt::Microsoft::UI::Xaml::Controls::SplitButton const&, winrt::Microsoft::UI::Xaml::Controls::SplitButtonClickEventArgs const&)
    {
        m_uiUpdatesItem.CloseUIWindow(false);
        m_uiUpdatesItem.Rename();
    }

    void MainWindow::MenuFlyoutItem_Click(winrt::Windows::Foundation::IInspectable const&, winrt::Microsoft::UI::Xaml::RoutedEventArgs const&)
    {
        m_uiUpdatesItem.CloseUIWindow(true);
        m_uiUpdatesItem.Rename();
    }

    void MainWindow::OpenDocs(winrt::Windows::Foundation::IInspectable const&, winrt::Microsoft::UI::Xaml::RoutedEventArgs const&)
    {
        Windows::System::Launcher::LaunchUriAsync(winrt::Windows::Foundation::Uri{ L"https://aka.ms/PowerToysOverview_PowerRename" });
    }

    HRESULT MainWindow::CreateShellItemArrayFromPaths(
        std::vector<std::wstring> files,
        IShellItemArray** shellItemArray)
    {
        _TRACER_;

        *shellItemArray = nullptr;
        PIDLIST_ABSOLUTE* itemList = nullptr;
        itemList = new (std::nothrow) PIDLIST_ABSOLUTE[files.size()];
        HRESULT hr = itemList ? S_OK : E_OUTOFMEMORY;
        UINT itemsCnt = 0;
        for (const auto& file : files)
        {
            const DWORD BUFSIZE = 4096;
            TCHAR buffer[BUFSIZE] = TEXT("");
            auto retval = GetFullPathName(file.c_str(), BUFSIZE, buffer, NULL);
            if (retval != 0 && PathFileExists(buffer))
            {
                hr = SHParseDisplayName(buffer, nullptr, &itemList[itemsCnt], 0, nullptr);
                ++itemsCnt;
            }
        }
        if (SUCCEEDED(hr) && itemsCnt > 0)
        {
            hr = SHCreateShellItemArrayFromIDLists(itemsCnt, const_cast<LPCITEMIDLIST*>(itemList), shellItemArray);
            if (SUCCEEDED(hr))
            {
                for (UINT i = 0; i < itemsCnt; i++)
                {
                    CoTaskMemFree(itemList[i]);
                }
            }
            else
            {
                Logger::error(L"Creating ShellItemArray from path list failed.");
            }
        }
        else
        {
            Logger::error(L"Parsing path list display names failed.");
            hr = E_FAIL;
        }

        delete[] itemList;
        return hr;
    }

    void MainWindow::PopulateExplorerItems()
    {
        _TRACER_;

        UINT count = 0;
        m_prManager->GetVisibleItemCount(&count);
        Logger::debug(L"Number of visible items: {}", count);

        for (UINT i = 0; i < count; ++i)
        {
            CComPtr<IPowerRenameItem> renameItem;
            if (SUCCEEDED(m_prManager->GetVisibleItemByIndex(i, &renameItem)))
            {
                int id = 0;
                renameItem->GetId(&id);

                PWSTR originalName = nullptr;
                renameItem->GetOriginalName(&originalName);
                PWSTR newName = nullptr;
                renameItem->GetNewName(&newName);

                bool selected;
                renameItem->GetSelected(&selected);

                UINT depth = 0;
                renameItem->GetDepth(&depth);

                bool isFolder = false;
                winrt::check_hresult(renameItem->GetIsFolder(&isFolder));

                AddExplorerItem(
                    id, originalName, newName == nullptr ? hstring{} : hstring{ newName }, isFolder ? 0 : 1, depth, selected);
            }
        }
    }

    HRESULT MainWindow::InitAutoComplete()
    {
        _TRACER_;

        HRESULT hr = S_OK;
        if (CSettingsInstance().GetMRUEnabled())
        {
            hr = CPowerRenameMRU::CPowerRenameMRUSearch_CreateInstance(&m_searchMRU);
            if (SUCCEEDED(hr))
            {
                for (const auto& item : m_searchMRU->GetMRUStrings())
                {
                    if (!item.empty())
                    {
                        AppendSearchMRU(hstring{ item });
                    }
                }
            }

            if (SUCCEEDED(hr))
            {
                hr = CPowerRenameMRU::CPowerRenameMRUReplace_CreateInstance(&m_replaceMRU);
                if (SUCCEEDED(hr))
                {
                    for (const auto& item : m_replaceMRU->GetMRUStrings())
                    {
                        if (!item.empty())
                        {
                            AppendReplaceMRU(hstring{ item });
                        }
                    }
                }
            }
        }

        return hr;
    }

    HRESULT MainWindow::EnumerateShellItems(_In_ IEnumShellItems* enumShellItems)
    {
        _TRACER_;

        HRESULT hr = S_OK;
        // Enumerate the data object and populate the manager
        if (m_prManager)
        {
            m_disableCountUpdate = true;

            // Ensure we re-create the enumerator
            m_prEnum = nullptr;
            hr = CPowerRenameEnum::s_CreateInstance(nullptr, m_prManager, IID_PPV_ARGS(&m_prEnum));
            if (SUCCEEDED(hr))
            {
                hr = m_prEnum->Start(enumShellItems);
            }

            m_disableCountUpdate = false;
        }

        return hr;
    }

    void MainWindow::SearchReplaceChanged(bool forceRenaming)
    {
        _TRACER_;

        Logger::debug(L"Forced renaming - {}", forceRenaming);
        // Pass updated search and replace terms to the IPowerRenameRegEx handler
        CComPtr<IPowerRenameRegEx> prRegEx;
        if (m_prManager && SUCCEEDED(m_prManager->GetRenameRegEx(&prRegEx)))
        {
            winrt::hstring searchTerm = textBox_search().Text();
            prRegEx->PutSearchTerm(searchTerm.c_str(), forceRenaming);

            winrt::hstring replaceTerm = textBox_replace().Text();
            prRegEx->PutReplaceTerm(replaceTerm.c_str(), forceRenaming);
        }
    }

    void MainWindow::ValidateFlags(PowerRenameFlags flag)
    {
        if (flag == Uppercase)
        {
            if (toggleButton_upperCase().IsChecked())
            {
                toggleButton_lowerCase().IsChecked(false);
                toggleButton_titleCase().IsChecked(false);
                toggleButton_capitalize().IsChecked(false);
            }
        }
        else if (flag == Lowercase)
        {
            if (toggleButton_lowerCase().IsChecked())
            {
                toggleButton_upperCase().IsChecked(false);
                toggleButton_titleCase().IsChecked(false);
                toggleButton_capitalize().IsChecked(false);
            }
        }
        else if (flag == Titlecase)
        {
            if (toggleButton_titleCase().IsChecked())
            {
                toggleButton_upperCase().IsChecked(false);
                toggleButton_lowerCase().IsChecked(false);
                toggleButton_capitalize().IsChecked(false);
            }
        }
        else if (flag == Capitalized)
        {
            if (toggleButton_capitalize().IsChecked())
            {
                toggleButton_upperCase().IsChecked(false);
                toggleButton_lowerCase().IsChecked(false);
                toggleButton_titleCase().IsChecked(false);
            }
        }

        m_flagValidationInProgress = true;
    }

    void MainWindow::UpdateFlag(PowerRenameFlags flag, UpdateFlagCommand command)
    {
        _TRACER_;

        DWORD flags{};
        m_prManager->GetFlags(&flags);

        if (command == UpdateFlagCommand::Set)
        {
            flags |= flag;
        }
        else if (command == UpdateFlagCommand::Reset)
        {
            flags &= ~flag;
        }

        Logger::debug(L"Flag {} " + std::wstring{ command == UpdateFlagCommand::Set ? L"set" : L"reset" }, flag);

        // Ensure we update flags
        if (m_prManager)
        {
            m_prManager->PutFlags(flags);
        }
    }

    void MainWindow::SetHandlers()
    {
        _TRACER_;

        m_uiUpdatesItem.PropertyChanged([&](auto const&, auto const& e) {
            std::wstring property{ e.PropertyName() };
            if (property == L"ShowAll")
            {
                SwitchView();
            }
            else if (property == L"ChangedItemId")
            {
                ToggleItem(m_uiUpdatesItem.ChangedExplorerItemId(), m_uiUpdatesItem.Checked());
            }
            else if (property == L"ToggleAll")
            {
                ToggleAll();
            }
            else if (property == L"Rename")
            {
                Rename(m_uiUpdatesItem.CloseUIWindow());
            }
        });

        // AutoSuggestBox Search
        textBox_search().TextChanged([&](auto const&, auto const&) {
            SearchReplaceChanged();
        });

        // AutoSuggestBox Replace
        textBox_replace().TextChanged([&](auto const&, auto const&) {
            SearchReplaceChanged();
        });

        // ToggleButton UpperCase
        toggleButton_upperCase().Checked([&](auto const&, auto const&) {
            ValidateFlags(Uppercase);
            UpdateFlag(Uppercase, UpdateFlagCommand::Set);
        });
        toggleButton_upperCase().Unchecked([&](auto const&, auto const&) {
            UpdateFlag(Uppercase, UpdateFlagCommand::Reset);
        });

        // ToggleButton LowerCase
        toggleButton_lowerCase().Checked([&](auto const&, auto const&) {
            ValidateFlags(Lowercase);
            UpdateFlag(Lowercase, UpdateFlagCommand::Set);
        });
        toggleButton_lowerCase().Unchecked([&](auto const&, auto const&) {
            UpdateFlag(Lowercase, UpdateFlagCommand::Reset);
        });

        // ToggleButton TitleCase
        toggleButton_titleCase().Checked([&](auto const&, auto const&) {
            ValidateFlags(Titlecase);
            UpdateFlag(Titlecase, UpdateFlagCommand::Set);
        });
        toggleButton_titleCase().Unchecked([&](auto const&, auto const&) {
            UpdateFlag(Titlecase, UpdateFlagCommand::Reset);
        });

        // ToggleButton Capitalize
        toggleButton_capitalize().Checked([&](auto const&, auto const&) {
            ValidateFlags(Capitalized);
            UpdateFlag(Capitalized, UpdateFlagCommand::Set);
        });
        toggleButton_capitalize().Unchecked([&](auto const&, auto const&) {
            UpdateFlag(Capitalized, UpdateFlagCommand::Reset);
        });

        // CheckBox Regex
        checkBox_regex().Checked([&](auto const&, auto const&) {
            ValidateFlags(UseRegularExpressions);
            UpdateFlag(UseRegularExpressions, UpdateFlagCommand::Set);
        });
        checkBox_regex().Unchecked([&](auto const&, auto const&) {
            UpdateFlag(UseRegularExpressions, UpdateFlagCommand::Reset);
        });

        // CheckBox CaseSensitive
        checkBox_case().Checked([&](auto const&, auto const&) {
            ValidateFlags(CaseSensitive);
            UpdateFlag(CaseSensitive, UpdateFlagCommand::Set);
        });
        checkBox_case().Unchecked([&](auto const&, auto const&) {
            UpdateFlag(CaseSensitive, UpdateFlagCommand::Reset);
        });

        // ComboBox RenameParts
        comboBox_renameParts().SelectionChanged([&](auto const&, auto const&) {
            int selectedIndex = comboBox_renameParts().SelectedIndex();
            if (selectedIndex == 0)
            { // Filename + extension
                UpdateFlag(NameOnly, UpdateFlagCommand::Reset);
                UpdateFlag(ExtensionOnly, UpdateFlagCommand::Reset);
            }
            else if (selectedIndex == 1) // Filename Only
            {
                ValidateFlags(NameOnly);
                UpdateFlag(ExtensionOnly, UpdateFlagCommand::Reset);
                UpdateFlag(NameOnly, UpdateFlagCommand::Set);
            }
            else if (selectedIndex == 2) // Extension Only
            {
                ValidateFlags(ExtensionOnly);
                UpdateFlag(NameOnly, UpdateFlagCommand::Reset);
                UpdateFlag(ExtensionOnly, UpdateFlagCommand::Set);
            }
        });

        // CheckBox MatchAllOccurences
        checkBox_matchAll().Checked([&](auto const&, auto const&) {
            ValidateFlags(MatchAllOccurences);
            UpdateFlag(MatchAllOccurences, UpdateFlagCommand::Set);
        });
        checkBox_matchAll().Unchecked([&](auto const&, auto const&) {
            UpdateFlag(MatchAllOccurences, UpdateFlagCommand::Reset);
        });

        // ToggleButton IncludeFiles
        toggleButton_includeFiles().Checked([&](auto const&, auto const&) {
            ValidateFlags(ExcludeFiles);
            UpdateFlag(ExcludeFiles, UpdateFlagCommand::Reset);
        });
        toggleButton_includeFiles().Unchecked([&](auto const&, auto const&) {
            UpdateFlag(ExcludeFiles, UpdateFlagCommand::Set);
        });

        // ToggleButton IncludeFolders
        toggleButton_includeFolders().Checked([&](auto const&, auto const&) {
            ValidateFlags(ExcludeFolders);
            UpdateFlag(ExcludeFolders, UpdateFlagCommand::Reset);
        });
        toggleButton_includeFolders().Unchecked([&](auto const&, auto const&) {
            UpdateFlag(ExcludeFolders, UpdateFlagCommand::Set);
        });

        // ToggleButton IncludeSubfolders
        toggleButton_includeSubfolders().Checked([&](auto const&, auto const&) {
            ValidateFlags(ExcludeSubfolders);
            UpdateFlag(ExcludeSubfolders, UpdateFlagCommand::Reset);
        });
        toggleButton_includeSubfolders().Unchecked([&](auto const&, auto const&) {
            UpdateFlag(ExcludeSubfolders, UpdateFlagCommand::Set);
        });

        // CheckBox EnumerateItems
        toggleButton_enumItems().Checked([&](auto const&, auto const&) {
            ValidateFlags(EnumerateItems);
            UpdateFlag(EnumerateItems, UpdateFlagCommand::Set);
        });
        toggleButton_enumItems().Unchecked([&](auto const&, auto const&) {
            UpdateFlag(EnumerateItems, UpdateFlagCommand::Reset);
        });

        // ButtonSettings
        button_settings().Click([&](auto const&, auto const&) {
            OpenSettingsApp();
        });
    }

    void MainWindow::ToggleItem(int32_t id, bool checked)
    {
        _TRACER_;
        Logger::debug(L"Toggling item with id = {}", id);
        CComPtr<IPowerRenameItem> spItem;

        if (SUCCEEDED(m_prManager->GetItemById(id, &spItem)))
        {
            spItem->PutSelected(checked);
        }
        UpdateCounts();
    }

    void MainWindow::ToggleAll()
    {
        _TRACER_;

        UINT itemCount = 0;
        m_prManager->GetItemCount(&itemCount);
        bool selected = checkBox_selectAll().IsChecked().GetBoolean();
        for (UINT i = 0; i < itemCount; i++)
        {
            CComPtr<IPowerRenameItem> spItem;
            if (SUCCEEDED(m_prManager->GetItemByIndex(i, &spItem)))
            {
                spItem->PutSelected(selected);
            }
        }
        UpdateCounts();
    }

    void MainWindow::SwitchView()
    {
        _TRACER_;

        m_prManager->SwitchFilter(0);
        PopulateExplorerItems();
        UpdateCounts();
    }

    void MainWindow::Rename(bool closeWindow)
    {
        _TRACER_;

        if (m_prManager)
        {
            m_prManager->Rename(m_window, closeWindow);
        }

        // Persist the current settings.  We only do this when
        // a rename is actually performed.  Not when the user
        // closes/cancels the dialog.
        WriteSettings();
    }

    HRESULT MainWindow::ReadSettings()
    {
        _TRACER_;

        bool persistState{ CSettingsInstance().GetPersistState() };
        Logger::debug(L"ReadSettings with persistState = {}", persistState);

        // Check if we should read flags from settings
        // or the defaults from the manager.
        DWORD flags = 0;
        if (persistState)
        {
            flags = CSettingsInstance().GetFlags();

            textBox_search().Text(CSettingsInstance().GetSearchText().c_str());
            textBox_replace().Text(CSettingsInstance().GetReplaceText().c_str());
        }
        else
        {
            m_prManager->GetFlags(&flags);
        }

        m_prManager->PutFlags(flags);
        SetCheckboxesFromFlags(flags);

        return S_OK;
    }

    HRESULT MainWindow::WriteSettings()
    {
        _TRACER_;

        // Check if we should store our settings
        if (CSettingsInstance().GetPersistState())
        {
            DWORD flags = 0;
            m_prManager->GetFlags(&flags);
            CSettingsInstance().SetFlags(flags);

            winrt::hstring searchTerm = textBox_search().Text();
            CSettingsInstance().SetSearchText(std::wstring{ searchTerm });

            if (CSettingsInstance().GetMRUEnabled() && m_searchMRU)
            {
                CComPtr<IPowerRenameMRU> spSearchMRU;
                if (SUCCEEDED(m_searchMRU->QueryInterface(IID_PPV_ARGS(&spSearchMRU))))
                {
                    spSearchMRU->AddMRUString(searchTerm.c_str());
                }
            }

            winrt::hstring replaceTerm = textBox_replace().Text();
            CSettingsInstance().SetReplaceText(std::wstring{ replaceTerm });

            if (CSettingsInstance().GetMRUEnabled() && m_replaceMRU)
            {
                CComPtr<IPowerRenameMRU> spReplaceMRU;
                if (SUCCEEDED(m_replaceMRU->QueryInterface(IID_PPV_ARGS(&spReplaceMRU))))
                {
                    spReplaceMRU->AddMRUString(replaceTerm.c_str());
                }
            }

            Trace::SettingsChanged();
        }

        return S_OK;
    }

    HRESULT MainWindow::OpenSettingsApp()
    {
        std::wstring path = get_module_folderpath(g_hostHInst);
        path += L"\\..\\..\\PowerToys.exe";

        std::wstring openSettings = L"--open-settings=PowerRename";

        CString commandLine;
        commandLine.Format(_T("\"%s\""), path.c_str());
        commandLine.AppendFormat(_T(" %s"), openSettings.c_str());

        int nSize = commandLine.GetLength() + 1;
        LPTSTR lpszCommandLine = new TCHAR[nSize];
        _tcscpy_s(lpszCommandLine, nSize, commandLine);

        STARTUPINFO startupInfo;
        ZeroMemory(&startupInfo, sizeof(STARTUPINFO));
        startupInfo.cb = sizeof(STARTUPINFO);
        startupInfo.wShowWindow = SW_SHOWNORMAL;

        PROCESS_INFORMATION processInformation;

        // Start the resizer
        CreateProcess(
            NULL,
            lpszCommandLine,
            NULL,
            NULL,
            TRUE,
            0,
            NULL,
            NULL,
            &startupInfo,
            &processInformation);

        delete[] lpszCommandLine;

        if (!CloseHandle(processInformation.hProcess))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        if (!CloseHandle(processInformation.hThread))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        return S_OK;
    }

    void MainWindow::SetCheckboxesFromFlags(DWORD flags)
    {
        if (flags & CaseSensitive)
        {
            checkBox_case().IsChecked(true);
        }
        if (flags & MatchAllOccurences)
        {
            checkBox_matchAll().IsChecked(true);
        }
        if (flags & UseRegularExpressions)
        {
            checkBox_regex().IsChecked(true);
        }
        if (flags & EnumerateItems)
        {
            toggleButton_enumItems().IsChecked(true);
        }
        if (flags & ExcludeFiles)
        {
            toggleButton_includeFiles().IsChecked(false);
        }
        if (flags & ExcludeFolders)
        {
            toggleButton_includeFolders().IsChecked(false);
        }
        if (flags & ExcludeSubfolders)
        {
            toggleButton_includeSubfolders().IsChecked(false);
        }
        if (flags & NameOnly)
        {
            comboBox_renameParts().SelectedIndex(1);
        }
        else if (flags & ExtensionOnly)
        {
            comboBox_renameParts().SelectedIndex(2);
        }
        if (flags & Uppercase)
        {
            toggleButton_upperCase().IsChecked(true);
        }
        else if (flags & Lowercase)
        {
            toggleButton_lowerCase().IsChecked(true);
        }
        else if (flags & Titlecase)
        {
            toggleButton_titleCase().IsChecked(true);
        }
        else if (flags & Capitalized)
        {
            toggleButton_capitalize().IsChecked(true);
        }
    }

    void MainWindow::UpdateCounts()
    {
        // This method is CPU intensive.  We disable it during certain operations
        // for performance reasons.
        if (m_disableCountUpdate)
        {
            return;
        }

        UINT selectedCount = 0;
        UINT renamingCount = 0;
        if (m_prManager)
        {
            m_prManager->GetSelectedItemCount(&selectedCount);
            m_prManager->GetRenameItemCount(&renamingCount);
        }

        if (m_selectedCount != selectedCount ||
            m_renamingCount != renamingCount)
        {
            m_selectedCount = selectedCount;
            m_renamingCount = renamingCount;

            // Update Rename button state
            m_uiUpdatesItem.ButtonRenameEnabled(renamingCount > 0);
        }

        m_uiUpdatesItem.OriginalCount(std::to_wstring(m_explorerItems.Size()));
        m_uiUpdatesItem.RenamedCount(std::to_wstring(m_renamingCount));
    }

    HRESULT MainWindow::OnItemAdded(_In_ IPowerRenameItem*)
    {
        return S_OK;
    }

    HRESULT MainWindow::OnUpdate(_In_ IPowerRenameItem* renameItem)
    {
        int id;
        HRESULT hr = renameItem->GetId(&id);
        if (SUCCEEDED(hr))
        {
            PWSTR newName = nullptr;
            hr = renameItem->GetNewName(&newName);
            if (SUCCEEDED(hr))
            {
                hstring newNameStr = newName == nullptr ? hstring{} : newName;
                UpdateExplorerItem(id, newNameStr);
            }
        }

        return S_OK;
    }

    HRESULT MainWindow::OnRename(_In_ IPowerRenameItem* renameItem)
    {
        int id;
        HRESULT hr = renameItem->GetId(&id);
        if (SUCCEEDED(hr))
        {
            PWSTR newName = nullptr;
            hr = renameItem->GetOriginalName(&newName);
            if (SUCCEEDED(hr))
            {
                hstring newNameStr = newName == nullptr ? hstring{} : newName;
                UpdateRenamedExplorerItem(id, newNameStr);
            }
        }

        UpdateCounts();
        return S_OK;
    }

    HRESULT MainWindow::OnError(_In_ IPowerRenameItem*)
    {
        return S_OK;
    }

    HRESULT MainWindow::OnRegExStarted(_In_ DWORD)
    {
        return S_OK;
    }

    HRESULT MainWindow::OnRegExCanceled(_In_ DWORD)
    {
        return S_OK;
    }

    HRESULT MainWindow::OnRegExCompleted(_In_ DWORD)
    {
        _TRACER_;

        if (m_flagValidationInProgress)
        {
            m_flagValidationInProgress = false;
        }
        else
        {
            DWORD filter = 0;
            m_prManager->GetFilter(&filter);
            if (filter == PowerRenameFilters::ShouldRename)
            {
                m_explorerItems.Clear();
                PopulateExplorerItems();
            }
        }

        UpdateCounts();
        return S_OK;
    }

    HRESULT MainWindow::OnRenameStarted()
    {
        return S_OK;
    }

    HRESULT MainWindow::OnRenameCompleted(bool closeUIWindowAfterRenaming)
    {
        _TRACER_;

        Logger::debug(L"Renaming completed. Close UI window - {}", closeUIWindowAfterRenaming);
        if (closeUIWindowAfterRenaming)
        {
            // Close the window
            PostMessage(m_window, WM_CLOSE, (WPARAM)0, (LPARAM)0);
        }
        else
        {
            // Force renaming work to start so newly renamed items are processed right away
            SearchReplaceChanged(true);
        }
        return S_OK;
    }
}

