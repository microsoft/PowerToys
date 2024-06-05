#include "pch.h"
#include "EditShortcutsWindow.h"

#include <common/Display/dpi_aware.h>
#include <common/utils/EventLocker.h>
#include <common/utils/winapi_error.h>
#include <keyboardmanager/common/MappingConfiguration.h>

#include "KeyboardManagerState.h"
#include "Dialog.h"
#include "KeyDropDownControl.h"
#include "LoadingAndSavingRemappingHelper.h"
#include "ShortcutControl.h"
#include "Styles.h"
#include "UIHelpers.h"
#include "XamlBridge2.h"
#include "ShortcutErrorType.h"
#include "EditorConstants.h"
#include <common/Themes/theme_listener.h>

using namespace winrt::Windows::Foundation;

static UINT g_currentDPI = DPIAware::DEFAULT_DPI;

LRESULT CALLBACK EditShortcutsWindowProc(HWND, UINT, WPARAM, LPARAM);

// This Hwnd will be the window handler for the Xaml Island: A child window that contains Xaml.
HWND hWndXamlIslandEditShortcutsWindow = nullptr;

// This variable is used to check if window registration has been done to avoid repeated registration leading to an error.
bool isEditShortcutsWindowRegistrationCompleted = false;

// Holds the native window handle of EditShortcuts Window.
HWND hwndEditShortcutsNativeWindow = nullptr;
std::mutex editShortcutsWindowMutex;

// Stores a pointer to the Xaml Bridge object so that it can be accessed from the window procedure
static XamlBridge2* xamlBridgePtr = nullptr;

// Theming
static ThemeListener theme_listener{};

static void handleTheme()
{
    auto theme = theme_listener.AppTheme;
    auto isDark = theme == AppTheme::Dark;
    Logger::info(L"Theme is now {}", isDark ? L"Dark" : L"Light");
    if (hwndEditShortcutsNativeWindow != nullptr)
    {
        ThemeHelpers::SetImmersiveDarkMode(hwndEditShortcutsNativeWindow, isDark);
    }
}

static IAsyncAction OnClickAccept(
    KBMEditor::KeyboardManagerState& keyboardManagerState,
    XamlRoot root,
    std::function<void()> ApplyRemappings)
{
    ShortcutErrorType isSuccess = LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(ShortcutControl::shortcutRemapBuffer);

    if (isSuccess != ShortcutErrorType::NoError)
    {
        if (!co_await Dialog::PartialRemappingConfirmationDialog(root, GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_PARTIALCONFIRMATIONDIALOGTITLE)))
        {
            co_return;
        }
    }
    ApplyRemappings();
}

// Function to create the Edit Shortcuts Window
inline void CreateEditShortcutsWindowImpl(HINSTANCE hInst, KBMEditor::KeyboardManagerState& keyboardManagerState, MappingConfiguration& mappingConfiguration, std::wstring keysForShortcutToEdit, std::wstring action)
{
    Logger::trace("CreateEditShortcutsWindowImpl()");
    auto locker = EventLocker::Get(KeyboardManagerConstants::EditorWindowEventName.c_str());
    if (!locker.has_value())
    {
        Logger::error(L"Failed to lock event {}. {}", KeyboardManagerConstants::EditorWindowEventName, get_last_error_or_default(GetLastError()));
    }

    Logger::trace(L"Signaled {} event to suspend the KBM engine", KeyboardManagerConstants::EditorWindowEventName);

    // Window Registration
    const wchar_t szWindowClass[] = L"EditShortcutsWindow";

    if (!isEditShortcutsWindowRegistrationCompleted)
    {
        WNDCLASSEX windowClass = {};
        windowClass.cbSize = sizeof(WNDCLASSEX);
        windowClass.lpfnWndProc = EditShortcutsWindowProc;
        windowClass.hInstance = hInst;
        windowClass.lpszClassName = szWindowClass;
        windowClass.hbrBackground = CreateSolidBrush((ThemeHelpers::GetAppTheme() == AppTheme::Dark) ? 0x00000000 : 0x00FFFFFF);
        windowClass.hIcon = static_cast<HICON>(LoadImageW(
            windowClass.hInstance,
            MAKEINTRESOURCE(IDS_KEYBOARDMANAGER_ICON),
            IMAGE_ICON,
            48,
            48,
            LR_DEFAULTCOLOR));
        if (RegisterClassEx(&windowClass) == NULL)
        {
            MessageBox(NULL, GET_RESOURCE_STRING(IDS_REGISTERCLASSFAILED_ERRORMESSAGE).c_str(), GET_RESOURCE_STRING(IDS_REGISTERCLASSFAILED_ERRORTITLE).c_str(), NULL);
            return;
        }

        isEditShortcutsWindowRegistrationCompleted = true;
    }

    // we might be passed via cmdline some keysForShortcutToEdit, this means we're editing just one shortcut
    if (!keysForShortcutToEdit.empty())
    {
    }

    // Find coordinates of the screen where the settings window is placed.
    RECT desktopRect = UIHelpers::GetForegroundWindowDesktopRect();

    // Calculate DPI dependent window size
    float windowWidth = EditorConstants::DefaultEditShortcutsWindowWidth;
    float windowHeight = EditorConstants::DefaultEditShortcutsWindowHeight;

    if (!keysForShortcutToEdit.empty())
    {
        windowWidth = EditorConstants::DefaultEditSingleShortcutsWindowWidth;
        windowHeight = EditorConstants::DefaultEditSingleShortcutsWindowHeight;
    }

    DPIAware::ConvertByCursorPosition(windowWidth, windowHeight);
    DPIAware::GetScreenDPIForCursor(g_currentDPI);

    // Window Creation
    HWND _hWndEditShortcutsWindow = CreateWindow(
        szWindowClass,
        GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_WINDOWNAME).c_str(),
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MAXIMIZEBOX,
        ((desktopRect.right + desktopRect.left) / 2) - ((int)windowWidth / 2),
        ((desktopRect.bottom + desktopRect.top) / 2) - ((int)windowHeight / 2),
        static_cast<int>(windowWidth),
        static_cast<int>(windowHeight),
        NULL,
        NULL,
        hInst,
        NULL);

    if (_hWndEditShortcutsWindow == NULL)
    {
        MessageBox(NULL, GET_RESOURCE_STRING(IDS_CREATEWINDOWFAILED_ERRORMESSAGE).c_str(), GET_RESOURCE_STRING(IDS_CREATEWINDOWFAILED_ERRORTITLE).c_str(), NULL);
        return;
    }

    // Ensures the window is in foreground on first startup. If this is not done, the window appears behind because the thread is not on the foreground.
    if (_hWndEditShortcutsWindow)
    {
        SetForegroundWindow(_hWndEditShortcutsWindow);
    }

    // Store the newly created Edit Shortcuts window's handle.
    std::unique_lock<std::mutex> hwndLock(editShortcutsWindowMutex);
    hwndEditShortcutsNativeWindow = _hWndEditShortcutsWindow;
    hwndLock.unlock();

    // Hide icon and caption from title bar
    const DWORD windowThemeOptionsMask = WTNCA_NODRAWCAPTION | WTNCA_NODRAWICON;
    WTA_OPTIONS windowThemeOptions{ windowThemeOptionsMask, windowThemeOptionsMask };
    SetWindowThemeAttribute(_hWndEditShortcutsWindow, WTA_NONCLIENT, &windowThemeOptions, sizeof(windowThemeOptions));

    handleTheme();
    theme_listener.AddChangedHandler(handleTheme);

    // Create the xaml bridge object
    XamlBridge2 xamlBridge(_hWndEditShortcutsWindow);

    // Create the desktop window xaml source object and set its content
    hWndXamlIslandEditShortcutsWindow = xamlBridge.InitBridge();

    // Set the pointer to the xaml bridge object
    xamlBridgePtr = &xamlBridge;

    // Header for the window
    Windows::UI::Xaml::Controls::RelativePanel header;
    header.Margin({ 10, 10, 10, 30 });

    // Header text
    TextBlock headerText;
    headerText.Text(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_WINDOWNAME));
    headerText.FontSize(30);
    headerText.Margin({ 0, 0, 0, 0 });
    header.SetAlignLeftWithPanel(headerText, true);

    // Cancel button
    Button cancelButton;
    cancelButton.Content(winrt::box_value(GET_RESOURCE_STRING(IDS_CANCEL_BUTTON)));
    cancelButton.Margin({ 10, 0, 0, 0 });
    cancelButton.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        // Close the window since settings do not need to be saved
        PostMessage(_hWndEditShortcutsWindow, WM_CLOSE, 0, 0);
    });

    //  Text block for information about remap key section.
    TextBlock shortcutRemapInfoHeader;
    shortcutRemapInfoHeader.Text(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_INFO));
    shortcutRemapInfoHeader.Margin({ 10, 0, 0, 10 });
    shortcutRemapInfoHeader.FontWeight(Text::FontWeights::SemiBold());
    shortcutRemapInfoHeader.TextWrapping(TextWrapping::Wrap);

    TextBlock shortcutRemapInfoExample;
    shortcutRemapInfoExample.Text(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_INFOEXAMPLE));
    shortcutRemapInfoExample.Margin({ 10, 0, 0, 20 });
    shortcutRemapInfoExample.FontStyle(Text::FontStyle::Italic);
    shortcutRemapInfoExample.TextWrapping(TextWrapping::Wrap);

    // Table to display the shortcuts
    StackPanel shortcutTable;

    // First header textblock in the header row of the shortcut table
    TextBlock originalShortcutHeader;
    originalShortcutHeader.Text(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_SOURCEHEADER));
    originalShortcutHeader.FontWeight(Text::FontWeights::Bold());

    // Second header textblock in the header row of the shortcut table
    TextBlock newShortcutHeader;
    newShortcutHeader.Text(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_TARGETHEADER));
    newShortcutHeader.FontWeight(Text::FontWeights::Bold());

    // Third header textblock in the header row of the shortcut table
    TextBlock targetAppHeader;
    targetAppHeader.Text(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_TARGETAPPHEADER));
    targetAppHeader.FontWeight(Text::FontWeights::Bold());
    targetAppHeader.HorizontalAlignment(HorizontalAlignment::Center);

    StackPanel tableHeader = StackPanel();
    tableHeader.Orientation(Orientation::Horizontal);
    tableHeader.Margin({ 10, 0, 0, 10 });
    auto originalShortcutContainer = UIHelpers::GetWrapped(originalShortcutHeader, EditorConstants::ShortcutOriginColumnWidth + static_cast<double>(EditorConstants::ShortcutArrowColumnWidth));
    tableHeader.Children().Append(originalShortcutContainer.as<FrameworkElement>());
    auto newShortcutHeaderContainer = UIHelpers::GetWrapped(newShortcutHeader, EditorConstants::ShortcutTargetColumnWidth);
    tableHeader.Children().Append(newShortcutHeaderContainer.as<FrameworkElement>());
    tableHeader.Children().Append(targetAppHeader);

    // Store handle of edit shortcuts window
    ShortcutControl::editShortcutsWindowHandle = _hWndEditShortcutsWindow;

    // Store keyboard manager state
    ShortcutControl::keyboardManagerState = &keyboardManagerState;
    KeyDropDownControl::keyboardManagerState = &keyboardManagerState;
    KeyDropDownControl::mappingConfiguration = &mappingConfiguration;

    // Clear the shortcut remap buffer
    ShortcutControl::shortcutRemapBuffer.clear();

    // Vector to store dynamically allocated control objects to avoid early destruction
    std::vector<std::vector<std::unique_ptr<ShortcutControl>>> keyboardRemapControlObjects;

    // Set keyboard manager UI state so that shortcut remaps are not applied while on this window
    keyboardManagerState.SetUIState(KBMEditor::KeyboardManagerUIState::EditShortcutsWindowActivated, _hWndEditShortcutsWindow);

    // Load existing os level shortcuts into UI
    // Create copy of the remaps to avoid concurrent access
    ShortcutRemapTable osLevelShortcutReMapCopy = mappingConfiguration.osLevelShortcutReMap;

    // Apply button
    Button applyButton;
    applyButton.Name(L"applyButton");
    applyButton.Content(winrt::box_value(GET_RESOURCE_STRING(IDS_OK_BUTTON)));
    applyButton.Style(AccentButtonStyle());
    applyButton.MinWidth(EditorConstants::HeaderButtonWidth);
    cancelButton.MinWidth(EditorConstants::HeaderButtonWidth);
    header.SetAlignRightWithPanel(cancelButton, true);
    header.SetLeftOf(applyButton, cancelButton);

    auto ApplyRemappings = [&mappingConfiguration, _hWndEditShortcutsWindow]() {
        LoadingAndSavingRemappingHelper::ApplyShortcutRemappings(mappingConfiguration, ShortcutControl::shortcutRemapBuffer, true);
        bool saveResult = mappingConfiguration.SaveSettingsToFile();
        PostMessage(_hWndEditShortcutsWindow, WM_CLOSE, 0, 0);
    };

    applyButton.Click([&keyboardManagerState, applyButton, ApplyRemappings](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        OnClickAccept(keyboardManagerState, applyButton.XamlRoot(), ApplyRemappings);
    });

    auto OnClickAcceptNoCheckFn = ApplyRemappings;

    for (const auto& it : osLevelShortcutReMapCopy)
    {
        auto isHidden = false;

        // check to see if this should be hidden because it's NOT the one we are looking for.
        // It will still be there for backward compatability, just not visible
        if (!keysForShortcutToEdit.empty())
        {
            isHidden = (keysForShortcutToEdit != it.first.ToHstringVK());
        }

        ShortcutControl::AddNewShortcutControlRow(shortcutTable, keyboardRemapControlObjects, it.first, it.second.targetShortcut, L"");
    }

    // Load existing app-specific shortcuts into UI
    // Create copy of the remaps to avoid concurrent access
    AppSpecificShortcutRemapTable appSpecificShortcutReMapCopy = mappingConfiguration.appSpecificShortcutReMap;

    // Iterate through all the apps
    for (const auto& itApp : appSpecificShortcutReMapCopy)
    {
        // Iterate through shortcuts for each app
        for (const auto& itShortcut : itApp.second)
        {
            auto isHidden = false;
            if (!keysForShortcutToEdit.empty())
            {
                isHidden = (keysForShortcutToEdit != itShortcut.first.ToHstringVK());
            }

            ShortcutControl::AddNewShortcutControlRow(shortcutTable, keyboardRemapControlObjects, itShortcut.first, itShortcut.second.targetShortcut, itApp.first);
        }
    }

    header.Children().Append(headerText);

    header.Children().Append(applyButton);
    header.Children().Append(cancelButton);

    ScrollViewer scrollViewer;
    scrollViewer.VerticalScrollMode(ScrollMode::Enabled);
    scrollViewer.HorizontalScrollMode(ScrollMode::Enabled);
    scrollViewer.VerticalScrollBarVisibility(ScrollBarVisibility::Auto);
    scrollViewer.HorizontalScrollBarVisibility(ScrollBarVisibility::Auto);

    // Add shortcut button
    Windows::UI::Xaml::Controls::Button addShortcut;
    addShortcut.Margin({ 10, 10, 0, 25 });
    addShortcut.Style(AccentButtonStyle());
    addShortcut.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ShortcutControl& newShortcut = ShortcutControl::AddNewShortcutControlRow(shortcutTable, keyboardRemapControlObjects);

        // Whenever a remap is added move to the bottom of the screen
        scrollViewer.ChangeView(nullptr, scrollViewer.ScrollableHeight(), nullptr);

        // Set focus to the first "Select" Button in the newly added row
        UIHelpers::SetFocusOnFirstSelectButtonInLastRowOfEditShortcutsWindow(shortcutTable, EditorConstants::ShortcutTableColCount);

        //newShortcut.OpenNewShortcutControlRow(shortcutTable, shortcutTable.Children().GetAt(shortcutTable.Children().Size() - 1).as<StackPanel>());
    });

    // if this is a delete action we just want to quick load the screen to delete the shortcut and close
    // this is so we can delete from the KBM settings screen
    if (action == L"isDelete")
    {
        auto indexToDelete = -1;
        for (int i = 0; i < ShortcutControl::shortcutRemapBuffer.size(); i++)
        {
            auto tempShortcut = std::get<Shortcut>(ShortcutControl::shortcutRemapBuffer[i].first[0]);
            if (tempShortcut.ToHstringVK() == keysForShortcutToEdit)
            {
                indexToDelete = i;
            }
        }
        if (indexToDelete >= 0)
        {
            ShortcutControl::shortcutRemapBuffer.erase(ShortcutControl::shortcutRemapBuffer.begin() + indexToDelete);
        }
        OnClickAcceptNoCheckFn();
        return;
    }

    // Remap shortcut button content
    StackPanel addShortcutContent;

    addShortcutContent.Orientation(Orientation::Horizontal);
    addShortcutContent.Spacing(10);
    addShortcutContent.Children().Append(SymbolIcon(Symbol::Add));
    TextBlock addShortcutText;
    addShortcutText.Text(GET_RESOURCE_STRING(IDS_ADD_SHORTCUT_BUTTON));
    addShortcutContent.Children().Append(addShortcutText);
    addShortcut.Content(addShortcutContent);

    // Set accessible name for the add shortcut button
    addShortcut.SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_ADD_SHORTCUT_BUTTON)));

    // Header and example text at the top of the window
    StackPanel helperText;
    helperText.Children().Append(shortcutRemapInfoHeader);
    helperText.Children().Append(shortcutRemapInfoExample);

    // Remapping table
    StackPanel mappingsPanel;
    mappingsPanel.Children().Append(tableHeader);
    mappingsPanel.Children().Append(shortcutTable);
    mappingsPanel.Children().Append(addShortcut);

    // Remapping table should be scrollable

    scrollViewer.Content(mappingsPanel);

    RelativePanel xamlContainer;
    xamlContainer.SetBelow(helperText, header);
    xamlContainer.SetBelow(scrollViewer, helperText);
    xamlContainer.SetAlignLeftWithPanel(header, true);
    xamlContainer.SetAlignRightWithPanel(header, true);
    xamlContainer.SetAlignLeftWithPanel(helperText, true);
    xamlContainer.SetAlignRightWithPanel(helperText, true);
    xamlContainer.SetAlignLeftWithPanel(scrollViewer, true);
    xamlContainer.SetAlignRightWithPanel(scrollViewer, true);
    xamlContainer.Children().Append(header);
    xamlContainer.Children().Append(helperText);
    xamlContainer.Children().Append(scrollViewer);

    try
    {
        // If a layout update has been triggered by other methods (e.g.: adapting to zoom level), this may throw an exception.
        xamlContainer.UpdateLayout();
    }
    catch (...)
    {
    }

    UserControl xamlContent;
    xamlContent.Content(xamlContainer);
    if (Windows::Foundation::Metadata::ApiInformation::IsTypePresent(L"Windows.UI.Composition.ICompositionSupportsSystemBackdrop"))
    {
        // Apply Mica
        muxc::BackdropMaterial::SetApplyToRootOrPageBackground(xamlContent, true);
    }
    else
    {
        // Mica isn't available
        xamlContainer.Background(Application::Current().Resources().Lookup(box_value(L"ApplicationPageBackgroundThemeBrush")).as<Media::SolidColorBrush>());
    }

    Window::Current().Content(xamlContent);

    ////End XAML Island section
    if (_hWndEditShortcutsWindow)
    {
        ShowWindow(_hWndEditShortcutsWindow, SW_SHOW);
        UpdateWindow(_hWndEditShortcutsWindow);
    }

    // Message loop:
    xamlBridge.MessageLoop();

    // Reset pointers to nullptr
    xamlBridgePtr = nullptr;
    hWndXamlIslandEditShortcutsWindow = nullptr;
    hwndLock.lock();
    hwndEditShortcutsNativeWindow = nullptr;
    keyboardManagerState.ResetUIState();
    keyboardManagerState.ClearRegisteredKeyDelays();
}

void CreateEditShortcutsWindow(HINSTANCE hInst, KBMEditor::KeyboardManagerState& keyboardManagerState, MappingConfiguration& mappingConfiguration, std::wstring keysForShortcutToEdit, std::wstring action)
{
    // Move implementation into the separate method so resources get destroyed correctly
    CreateEditShortcutsWindowImpl(hInst, keyboardManagerState, mappingConfiguration, keysForShortcutToEdit, action);

    // Calling ClearXamlIslands() outside of the message loop is not enough to prevent
    // Microsoft.UI.XAML.dll from crashing during deinitialization, see https://github.com/microsoft/PowerToys/issues/10906
    Logger::trace("Terminating process {}", GetCurrentProcessId());
    Logger::flush();
    TerminateProcess(GetCurrentProcess(), 0);
}

LRESULT CALLBACK EditShortcutsWindowProc(HWND hWnd, UINT messageCode, WPARAM wParam, LPARAM lParam)
{
    switch (messageCode)
    {
    // Resize the XAML window whenever the parent window is painted or resized
    case WM_PAINT:
    case WM_SIZE:
    {
        RECT rect = { 0 };
        GetClientRect(hWnd, &rect);
        SetWindowPos(hWndXamlIslandEditShortcutsWindow, 0, rect.left, rect.top, rect.right, rect.bottom, SWP_SHOWWINDOW);
    }
    break;
    // To avoid UI elements overlapping on making the window smaller enforce a minimum window size
    case WM_GETMINMAXINFO:
    {
        LPMINMAXINFO mmi = reinterpret_cast<LPMINMAXINFO>(lParam);
        float minWidth = EditorConstants::MinimumEditShortcutsWindowWidth;
        float minHeight = EditorConstants::MinimumEditShortcutsWindowHeight;

        DPIAware::Convert(MonitorFromWindow(hWnd, MONITOR_DEFAULTTONULL), minWidth, minHeight);

        mmi->ptMinTrackSize.x = static_cast<LONG>(minWidth);
        mmi->ptMinTrackSize.y = static_cast<LONG>(minHeight);
    }
    break;
    case WM_GETDPISCALEDSIZE:
    {
        UINT newDPI = static_cast<UINT>(wParam);
        SIZE* size = reinterpret_cast<SIZE*>(lParam);
        Logger::trace(L"WM_GETDPISCALEDSIZE: DPI {} size X {} Y {}", newDPI, size->cx, size->cy);

        float scalingFactor = static_cast<float>(newDPI) / g_currentDPI;
        Logger::trace(L"WM_GETDPISCALEDSIZE: scaling factor {}", scalingFactor);

        size->cx = static_cast<LONG>(size->cx * scalingFactor);
        size->cy = static_cast<LONG>(size->cy * scalingFactor);

        return 1;
    }
    break;
    case WM_DPICHANGED:
    {
        UINT newDPI = static_cast<UINT>(LOWORD(wParam));
        g_currentDPI = newDPI;

        RECT* rect = reinterpret_cast<RECT*>(lParam);
        SetWindowPos(
            hWnd,
            nullptr,
            rect->left,
            rect->top,
            rect->right - rect->left,
            rect->bottom - rect->top,
            SWP_NOZORDER | SWP_NOACTIVATE);

        Logger::trace(L"WM_DPICHANGED: new dpi {} rect {} {} ", newDPI, rect->right - rect->left, rect->bottom - rect->top);
    }
    break;
    default:
        // If the Xaml Bridge object exists, then use it's message handler to handle keyboard focus operations
        if (xamlBridgePtr != nullptr)
        {
            return xamlBridgePtr->MessageHandler(messageCode, wParam, lParam);
        }
        else if (messageCode == WM_NCDESTROY)
        {
            PostQuitMessage(0);
            break;
        }
        return DefWindowProc(hWnd, messageCode, wParam, lParam);
        break;
    }

    return 0;
}

// Function to check if there is already a window active if yes bring to foreground
bool CheckEditShortcutsWindowActive()
{
    bool result = false;
    std::unique_lock<std::mutex> hwndLock(editShortcutsWindowMutex);
    if (hwndEditShortcutsNativeWindow != nullptr)
    {
        // Check if the window is minimized if yes then restore the window.
        if (IsIconic(hwndEditShortcutsNativeWindow))
        {
            ShowWindow(hwndEditShortcutsNativeWindow, SW_RESTORE);
        }

        // If there is an already existing window no need to create a new open bring it on foreground.
        SetForegroundWindow(hwndEditShortcutsNativeWindow);
        result = true;
    }

    return result;
}

// Function to close any active Edit Shortcuts window
void CloseActiveEditShortcutsWindow()
{
    std::unique_lock<std::mutex> hwndLock(editShortcutsWindowMutex);
    if (hwndEditShortcutsNativeWindow != nullptr)
    {
        PostMessage(hwndEditShortcutsNativeWindow, WM_CLOSE, 0, 0);
    }
}
