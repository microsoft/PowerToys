// PowerRenameUIHost.cpp : Defines the entry point for the application.
//
#include "pch.h"

#include "PowerRenameUIHost.h"
#include <settings.h>
#include <trace.h>

#include <exception>
#include <string>
#include <sstream>
#include <vector>

#include <common/logger/call_tracer.h>
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/process_path.h>

#define MAX_LOADSTRING 100

// Non-localizable
const std::wstring moduleName = L"PowerRename";
const std::wstring internalPath = L"";
const wchar_t c_WindowClass[] = L"PowerRename";
HINSTANCE g_hostHInst;

int AppWindow::Show(HINSTANCE hInstance, std::vector<std::wstring> files)
{
    auto window = AppWindow(hInstance, files);
    window.CreateAndShowWindow();
    Logger::debug(L"PowerRename UI created. Starting the message loop.");

    return window.MessageLoop(window.m_accelerators.get());
}

LRESULT AppWindow::MessageHandler(UINT message, WPARAM wParam, LPARAM lParam) noexcept
{
    switch (message)
    {
        HANDLE_MSG(WindowHandle(), WM_CREATE, OnCreate);
        HANDLE_MSG(WindowHandle(), WM_COMMAND, OnCommand);
        HANDLE_MSG(WindowHandle(), WM_DESTROY, OnDestroy);
        HANDLE_MSG(WindowHandle(), WM_SIZE, OnResize);
    default:
        return base_type::MessageHandler(message, wParam, lParam);
    }

    return base_type::MessageHandler(message, wParam, lParam);
}

AppWindow::AppWindow(HINSTANCE hInstance, std::vector<std::wstring> files) noexcept :
    m_instance{ hInstance }, m_managerEvents{ this }
{
    if (SUCCEEDED(CPowerRenameManager::s_CreateInstance(&m_prManager)))
    {
        // Create the factory for our items
        CComPtr<IPowerRenameItemFactory> prItemFactory;
        if (SUCCEEDED(CPowerRenameItem::s_CreateInstance(nullptr, IID_PPV_ARGS(&prItemFactory))))
        {
            if(SUCCEEDED(m_prManager->PutRenameItemFactory(prItemFactory)))
            {
                if (SUCCEEDED(m_prManager->Advise(&m_managerEvents, &m_cookie)))
                {
                    CComPtr<IShellItemArray> shellItemArray;
                    // To test PowerRenameUIHost uncomment this line and update the path to
                    // your local (absolute or relative) path which you want to see in PowerRename
                    //files.push_back(L"<path>");

                    if (!files.empty())
                    {
                        if (SUCCEEDED(CreateShellItemArrayFromPaths(files, &shellItemArray)))
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
}

void AppWindow::CreateAndShowWindow()
{
    _TRACER_;

    m_accelerators.reset(LoadAcceleratorsW(m_instance, MAKEINTRESOURCE(IDC_POWERRENAMEUIHOST)));

    WNDCLASSEXW wcex = { sizeof(wcex) };
    wcex.style = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc = WndProc;
    wcex.hInstance = m_instance;
    wcex.hIcon = LoadIconW(m_instance, MAKEINTRESOURCE(IDC_POWERRENAMEUIHOST));
    wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wcex.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    wcex.lpszClassName = c_WindowClass;
    wcex.hIconSm = LoadIconW(wcex.hInstance, MAKEINTRESOURCE(IDI_SMALL));
    RegisterClassExW(&wcex); // don't test result, handle error at CreateWindow

    wchar_t title[64];
    LoadStringW(m_instance, IDS_APP_TITLE, title, ARRAYSIZE(title));

    // hardcoded width and height (1200 x 600) - with WinUI 3, it should auto-scale to the content
    int windowWidth = 1200;
    int windowHeight = 600;
    m_window = CreateWindowW(c_WindowClass, title, WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, 0, windowWidth, windowHeight, nullptr, nullptr, m_instance, this);
    THROW_LAST_ERROR_IF(!m_window);

    POINT cursorPosition{};
    if (GetCursorPos(&cursorPosition))
    {
        HMONITOR hMonitor = MonitorFromPoint(cursorPosition, MONITOR_DEFAULTTOPRIMARY);
        MONITORINFOEX monitorInfo;
        monitorInfo.cbSize = sizeof(MONITORINFOEX);
        GetMonitorInfo(hMonitor, &monitorInfo);

        SetWindowPos(m_window,
                     HWND_TOP,
                     monitorInfo.rcWork.left + (monitorInfo.rcWork.right - monitorInfo.rcWork.left - windowWidth) / 2,
                     monitorInfo.rcWork.top + (monitorInfo.rcWork.bottom - monitorInfo.rcWork.top - windowHeight) / 2,
                     0,
                     0,
                     SWP_NOSIZE);
    }

    ShowWindow(m_window, SW_SHOWNORMAL);
    UpdateWindow(m_window);
    SetFocus(m_window);
}

bool AppWindow::OnCreate(HWND, LPCREATESTRUCT) noexcept
{
    _TRACER_;

    m_mainUserControl = winrt::PowerRenameUILib::MainWindow();
    m_xamlIsland = CreateDesktopWindowsXamlSource(WS_TABSTOP, m_mainUserControl);

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

    m_mainUserControl.UIUpdatesItem().ButtonRenameEnabled(false);
    InitAutoComplete();
    SearchReplaceChanged();
    return true;
}

void AppWindow::OnCommand(HWND, int id, HWND hwndControl, UINT codeNotify) noexcept
{
    switch (id)
    {
    case IDM_ABOUT:
        DialogBoxW(m_instance, MAKEINTRESOURCE(IDD_ABOUTBOX), WindowHandle(), [](HWND hDlg, UINT message, WPARAM wParam, LPARAM lParam) -> INT_PTR {
            switch (message)
            {
            case WM_INITDIALOG:
                return TRUE;

            case WM_COMMAND:
                if ((LOWORD(wParam) == IDOK) || (LOWORD(wParam) == IDCANCEL))
                {
                    EndDialog(hDlg, LOWORD(wParam));
                    return TRUE;
                }
                break;
            }
            return FALSE;
        });
        break;

    case IDM_EXIT:
        PostQuitMessage(0);
        break;
    }
}

void AppWindow::OnDestroy(HWND hwnd) noexcept
{
    _TRACER_;

    base_type::OnDestroy(hwnd);
}

void AppWindow::OnResize(HWND, UINT state, int cx, int cy) noexcept
{
    _TRACER_;

    SetWindowPos(m_xamlIsland, NULL, 0, 0, cx, cy, SWP_SHOWWINDOW);
}

HRESULT AppWindow::CreateShellItemArrayFromPaths(
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

void AppWindow::PopulateExplorerItems()
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
            bool isSubFolderContent = false;
            winrt::check_hresult(renameItem->GetIsFolder(&isFolder));

            m_mainUserControl.AddExplorerItem(
                id, originalName, newName == nullptr ? hstring{} : hstring{ newName }, isFolder ? 0 : 1, depth, selected);
        }
    }
}

HRESULT AppWindow::InitAutoComplete()
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
                    m_mainUserControl.AppendSearchMRU(item);
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
                        m_mainUserControl.AppendReplaceMRU(item);
                    }
                }
            }
        }
    }

    return hr;
}

HRESULT AppWindow::EnumerateShellItems(_In_ IEnumShellItems* enumShellItems)
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

void AppWindow::SearchReplaceChanged(bool forceRenaming)
{
    _TRACER_;

    Logger::debug(L"Forced renaming - {}", forceRenaming);
    // Pass updated search and replace terms to the IPowerRenameRegEx handler
    CComPtr<IPowerRenameRegEx> prRegEx;
    if (m_prManager && SUCCEEDED(m_prManager->GetRenameRegEx(&prRegEx)))
    {
        winrt::hstring searchTerm = m_mainUserControl.AutoSuggestBoxSearch().Text();
        prRegEx->PutSearchTerm(searchTerm.c_str(), forceRenaming);

        winrt::hstring replaceTerm = m_mainUserControl.AutoSuggestBoxReplace().Text();
        prRegEx->PutReplaceTerm(replaceTerm.c_str(), forceRenaming);
    }
}

void AppWindow::ValidateFlags(PowerRenameFlags flag)
{
    if (flag == Uppercase)
    {
        if (m_mainUserControl.ToggleButtonUpperCase().IsChecked())
        {
            m_mainUserControl.ToggleButtonLowerCase().IsChecked(false);
            m_mainUserControl.ToggleButtonTitleCase().IsChecked(false);
            m_mainUserControl.ToggleButtonCapitalize().IsChecked(false);
        }
    }
    else if (flag == Lowercase)
    {
        if (m_mainUserControl.ToggleButtonLowerCase().IsChecked())
        {
            m_mainUserControl.ToggleButtonUpperCase().IsChecked(false);
            m_mainUserControl.ToggleButtonTitleCase().IsChecked(false);
            m_mainUserControl.ToggleButtonCapitalize().IsChecked(false);
        }
    }
    else if (flag == Titlecase)
    {
        if (m_mainUserControl.ToggleButtonTitleCase().IsChecked())
        {
            m_mainUserControl.ToggleButtonUpperCase().IsChecked(false);
            m_mainUserControl.ToggleButtonLowerCase().IsChecked(false);
            m_mainUserControl.ToggleButtonCapitalize().IsChecked(false);
        }
    }
    else if (flag == Capitalized)
    {
        if (m_mainUserControl.ToggleButtonCapitalize().IsChecked())
        {
            m_mainUserControl.ToggleButtonUpperCase().IsChecked(false);
            m_mainUserControl.ToggleButtonLowerCase().IsChecked(false);
            m_mainUserControl.ToggleButtonTitleCase().IsChecked(false);
        }
    }

    m_flagValidationInProgress = true;
}

void AppWindow::UpdateFlag(PowerRenameFlags flag, UpdateFlagCommand command)
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

void AppWindow::SetHandlers()
{
    _TRACER_;

    m_mainUserControl.UIUpdatesItem().PropertyChanged([&](winrt::Windows::Foundation::IInspectable const& sender, Data::PropertyChangedEventArgs const& e) {
        std::wstring property{ e.PropertyName() };
        if (property == L"ShowAll")
        {
            SwitchView();
        }
        else if (property == L"ChangedItemId")
        {
            ToggleItem(m_mainUserControl.UIUpdatesItem().ChangedExplorerItemId(), m_mainUserControl.UIUpdatesItem().Checked());
        }
        else if (property == L"ToggleAll")
        {
            ToggleAll();
        }
        else if (property == L"Rename")
        {
            Rename(m_mainUserControl.UIUpdatesItem().CloseUIWindow());
        }
    });

    // AutoSuggestBox Search
    m_mainUserControl.AutoSuggestBoxSearch().TextChanged([&](winrt::Windows::Foundation::IInspectable const& sender, AutoSuggestBoxTextChangedEventArgs const&) {
        SearchReplaceChanged();
    });

    // AutoSuggestBox Replace
    m_mainUserControl.AutoSuggestBoxReplace().TextChanged([&](winrt::Windows::Foundation::IInspectable const& sender, AutoSuggestBoxTextChangedEventArgs const&) {
        SearchReplaceChanged();
    });

    // ToggleButton UpperCase
    m_mainUserControl.ToggleButtonUpperCase().Checked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ValidateFlags(Uppercase);
        UpdateFlag(Uppercase, UpdateFlagCommand::Set);
    });
    m_mainUserControl.ToggleButtonUpperCase().Unchecked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        UpdateFlag(Uppercase, UpdateFlagCommand::Reset);
    });

    // ToggleButton LowerCase
    m_mainUserControl.ToggleButtonLowerCase().Checked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ValidateFlags(Lowercase);
        UpdateFlag(Lowercase, UpdateFlagCommand::Set);
    });
    m_mainUserControl.ToggleButtonLowerCase().Unchecked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        UpdateFlag(Lowercase, UpdateFlagCommand::Reset);
    });

    // ToggleButton TitleCase
    m_mainUserControl.ToggleButtonTitleCase().Checked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ValidateFlags(Titlecase);
        UpdateFlag(Titlecase, UpdateFlagCommand::Set);
    });
    m_mainUserControl.ToggleButtonTitleCase().Unchecked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        UpdateFlag(Titlecase, UpdateFlagCommand::Reset);
    });

    // ToggleButton Capitalize
    m_mainUserControl.ToggleButtonCapitalize().Checked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ValidateFlags(Capitalized);
        UpdateFlag(Capitalized, UpdateFlagCommand::Set);
    });
    m_mainUserControl.ToggleButtonCapitalize().Unchecked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        UpdateFlag(Capitalized, UpdateFlagCommand::Reset);
    });

    // CheckBox Regex
    m_mainUserControl.CheckBoxRegex().Checked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ValidateFlags(UseRegularExpressions);
        UpdateFlag(UseRegularExpressions, UpdateFlagCommand::Set);
    });
    m_mainUserControl.CheckBoxRegex().Unchecked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        UpdateFlag(UseRegularExpressions, UpdateFlagCommand::Reset);
    });

    // CheckBox CaseSensitive
    m_mainUserControl.CheckBoxCaseSensitive().Checked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ValidateFlags(CaseSensitive);
        UpdateFlag(CaseSensitive, UpdateFlagCommand::Set);
    });
    m_mainUserControl.CheckBoxCaseSensitive().Unchecked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        UpdateFlag(CaseSensitive, UpdateFlagCommand::Reset);
    });

    // ComboBox RenameParts
    m_mainUserControl.ComboBoxRenameParts().SelectionChanged([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        if (m_mainUserControl.ComboBoxRenameParts().SelectedIndex() == 0)
        { // Filename + extension
            UpdateFlag(NameOnly, UpdateFlagCommand::Reset);
            UpdateFlag(ExtensionOnly, UpdateFlagCommand::Reset);
        }
        else if (m_mainUserControl.ComboBoxRenameParts().SelectedIndex() == 1) // Filename Only
        {
            ValidateFlags(NameOnly);
            UpdateFlag(ExtensionOnly, UpdateFlagCommand::Reset);
            UpdateFlag(NameOnly, UpdateFlagCommand::Set);
        }
        else if (m_mainUserControl.ComboBoxRenameParts().SelectedIndex() == 2) // Extension Only
        {
            ValidateFlags(ExtensionOnly);
            UpdateFlag(NameOnly, UpdateFlagCommand::Reset);
            UpdateFlag(ExtensionOnly, UpdateFlagCommand::Set);
        }
    });

    // CheckBox MatchAllOccurences
    m_mainUserControl.CheckBoxMatchAll().Checked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ValidateFlags(MatchAllOccurences);
        UpdateFlag(MatchAllOccurences, UpdateFlagCommand::Set);
    });
    m_mainUserControl.CheckBoxMatchAll().Unchecked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        UpdateFlag(MatchAllOccurences, UpdateFlagCommand::Reset);
    });

    // ToggleButton IncludeFiles
    m_mainUserControl.ToggleButtonIncludeFiles().Checked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ValidateFlags(ExcludeFiles);
        UpdateFlag(ExcludeFiles, UpdateFlagCommand::Reset);
    });
    m_mainUserControl.ToggleButtonIncludeFiles().Unchecked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        UpdateFlag(ExcludeFiles, UpdateFlagCommand::Set);
    });

    // ToggleButton IncludeFolders
    m_mainUserControl.ToggleButtonIncludeFolders().Checked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ValidateFlags(ExcludeFolders);
        UpdateFlag(ExcludeFolders, UpdateFlagCommand::Reset);
    });
    m_mainUserControl.ToggleButtonIncludeFolders().Unchecked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        UpdateFlag(ExcludeFolders, UpdateFlagCommand::Set);
    });

    // ToggleButton IncludeSubfolders
    m_mainUserControl.ToggleButtonIncludeSubfolders().Checked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ValidateFlags(ExcludeSubfolders);
        UpdateFlag(ExcludeSubfolders, UpdateFlagCommand::Reset);
    });
    m_mainUserControl.ToggleButtonIncludeSubfolders().Unchecked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        UpdateFlag(ExcludeSubfolders, UpdateFlagCommand::Set);
    });

    // CheckBox EnumerateItems
    m_mainUserControl.ToggleButtonEnumerateItems().Checked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ValidateFlags(EnumerateItems);
        UpdateFlag(EnumerateItems, UpdateFlagCommand::Set);
    });
    m_mainUserControl.ToggleButtonEnumerateItems().Unchecked([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        UpdateFlag(EnumerateItems, UpdateFlagCommand::Reset);
    });

    // ButtonSettings
    m_mainUserControl.ButtonSettings().Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        OpenSettingsApp();
    });
}

void AppWindow::ToggleItem(int32_t id, bool checked)
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

void AppWindow::ToggleAll()
{
    _TRACER_;

    UINT itemCount = 0;
    m_prManager->GetItemCount(&itemCount);
    bool selected = m_mainUserControl.CheckBoxSelectAll().IsChecked().GetBoolean();
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

void AppWindow::SwitchView()
{
    _TRACER_;

    m_prManager->SwitchFilter(0);
    PopulateExplorerItems();
    UpdateCounts();
}

void AppWindow::Rename(bool closeWindow)
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

HRESULT AppWindow::ReadSettings()
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

        m_mainUserControl.AutoSuggestBoxSearch().Text(CSettingsInstance().GetSearchText().c_str());
        m_mainUserControl.AutoSuggestBoxReplace().Text(CSettingsInstance().GetReplaceText().c_str());
    }
    else
    {
        m_prManager->GetFlags(&flags);
    }

    m_prManager->PutFlags(flags);
    SetCheckboxesFromFlags(flags);

    return S_OK;
}

HRESULT AppWindow::WriteSettings()
{
    _TRACER_;

    // Check if we should store our settings
    if (CSettingsInstance().GetPersistState())
    {
        DWORD flags = 0;
        m_prManager->GetFlags(&flags);
        CSettingsInstance().SetFlags(flags);

        winrt::hstring searchTerm = m_mainUserControl.AutoSuggestBoxSearch().Text();
        CSettingsInstance().SetSearchText(std::wstring{ searchTerm });

        if (CSettingsInstance().GetMRUEnabled() && m_searchMRU)
        {
            CComPtr<IPowerRenameMRU> spSearchMRU;
            if (SUCCEEDED(m_searchMRU->QueryInterface(IID_PPV_ARGS(&spSearchMRU))))
            {
                spSearchMRU->AddMRUString(searchTerm.c_str());
            }
        }

        winrt::hstring replaceTerm = m_mainUserControl.AutoSuggestBoxReplace().Text();
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

HRESULT AppWindow::OpenSettingsApp()
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

void AppWindow::SetCheckboxesFromFlags(DWORD flags)
{
    if (flags & CaseSensitive)
    {
        m_mainUserControl.CheckBoxCaseSensitive().IsChecked(true);
    }
    if (flags & MatchAllOccurences)
    {
        m_mainUserControl.CheckBoxMatchAll().IsChecked(true);
    }
    if (flags & UseRegularExpressions)
    {
        m_mainUserControl.CheckBoxRegex().IsChecked(true);
    }
    if (flags & EnumerateItems)
    {
        m_mainUserControl.ToggleButtonEnumerateItems().IsChecked(true);
    }
    if (flags & ExcludeFiles)
    {
        m_mainUserControl.ToggleButtonIncludeFiles().IsChecked(false);
    }
    if (flags & ExcludeFolders)
    {
        m_mainUserControl.ToggleButtonIncludeFolders().IsChecked(false);
    }
    if (flags & ExcludeSubfolders)
    {
        m_mainUserControl.ToggleButtonIncludeSubfolders().IsChecked(false);
    }
    if (flags & NameOnly)
    {
        m_mainUserControl.ComboBoxRenameParts().SelectedIndex(1);
    }
    else if (flags & ExtensionOnly)
    {
        m_mainUserControl.ComboBoxRenameParts().SelectedIndex(2);
    }
    if (flags & Uppercase)
    {
        m_mainUserControl.ToggleButtonUpperCase().IsChecked(true);
    }
    else if (flags & Lowercase)
    {
        m_mainUserControl.ToggleButtonLowerCase().IsChecked(true);
    }
    else if (flags & Titlecase)
    {
        m_mainUserControl.ToggleButtonTitleCase().IsChecked(true);
    }
    else if (flags & Capitalized)
    {
        m_mainUserControl.ToggleButtonCapitalize().IsChecked(true);
    }
}

void AppWindow::UpdateCounts()
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
        m_mainUserControl.UIUpdatesItem().ButtonRenameEnabled(renamingCount > 0);
    }

    m_mainUserControl.UIUpdatesItem().OriginalCount(std::to_wstring(m_mainUserControl.ExplorerItems().Size()));
    m_mainUserControl.UIUpdatesItem().RenamedCount(std::to_wstring(m_renamingCount));
}

HRESULT AppWindow::OnItemAdded(_In_ IPowerRenameItem* renameItem)
{
    return S_OK;
}

HRESULT AppWindow::OnUpdate(_In_ IPowerRenameItem* renameItem)
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
            m_mainUserControl.UpdateExplorerItem(id, newNameStr);
        }
    }

    return S_OK;
}

HRESULT AppWindow::OnRename(_In_ IPowerRenameItem* renameItem)
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
            m_mainUserControl.UpdateRenamedExplorerItem(id, newNameStr);
        }
    }

    UpdateCounts();
    return S_OK;
}

HRESULT AppWindow::OnError(_In_ IPowerRenameItem* renameItem)
{
    return S_OK;
}

HRESULT AppWindow::OnRegExStarted(_In_ DWORD threadId)
{
    return S_OK;
}

HRESULT AppWindow::OnRegExCanceled(_In_ DWORD threadId)
{
    return S_OK;
}

HRESULT AppWindow::OnRegExCompleted(_In_ DWORD threadId)
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
            m_mainUserControl.ExplorerItems().Clear();
            PopulateExplorerItems();
        }
    }

    UpdateCounts();
    return S_OK;
}

HRESULT AppWindow::OnRenameStarted()
{
    return S_OK;
}

HRESULT AppWindow::OnRenameCompleted(bool closeUIWindowAfterRenaming)
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

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                      _In_opt_ HINSTANCE hPrevInstance,
                      _In_ LPWSTR lpCmdLine,
                      _In_ int nCmdShow)
{
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::powerRenameLoggerName);

#define BUFSIZE 4096 * 4

    HANDLE hStdin = GetStdHandle(STD_INPUT_HANDLE);
    if (hStdin == INVALID_HANDLE_VALUE)
    {
        Logger::error(L"Invalid input handle.");
        ExitProcess(1);
    }

    BOOL bSuccess;
    WCHAR chBuf[BUFSIZE];
    DWORD dwRead;
    std::vector<std::wstring> files;
    for (;;)
    {
        // Read from standard input and stop on error or no data.
        bSuccess = ReadFile(hStdin, chBuf, BUFSIZE * sizeof(wchar_t), &dwRead, NULL);

        if (!bSuccess || dwRead == 0)
            break;

        std::wstring inputBatch{ chBuf, dwRead / sizeof(wchar_t) };

        std::wstringstream ss(inputBatch);
        std::wstring item;
        wchar_t delimiter = '?';
        while (std::getline(ss, item, delimiter))
        {
            files.push_back(item);
        }

        if (!bSuccess)
            break;
    }

    Logger::debug(L"Starting PowerRename with {} files selected", files.size());

    g_hostHInst = hInstance;
    try
    {
        winrt::init_apartment(winrt::apartment_type::single_threaded);

        winrt::PowerRenameUILib::App app;
        const auto result = AppWindow::Show(hInstance, files);
        app.Close();
    }
    catch (std::exception e)
    {
        Logger::error("Exception thrown during PowerRename UI initialization: {}", std::string{ e.what() });
    }
}
