#include "pch.h"

#include <set>

#include <common/Display/dpi_aware.h>
#include <common/interop/shared_constants.h>
#include <common/themes/windows_colors.h>
#include <common/utils/EventLocker.h>
#include <common/utils/winapi_error.h>

#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/common/MappingConfiguration.h>

#include <KeyboardManagerState.h>
#include "EditKeyboardWindow.h"
#include "SingleKeyRemapControl.h"
#include "KeyDropDownControl.h"
#include "XamlBridge2.h"
#include "Styles.h"
#include "Dialog.h"
#include "LoadingAndSavingRemappingHelper.h"
#include "UIHelpers.h"
#include "ShortcutErrorType.h"
#include "EditorConstants.h"
#include <common/Themes/theme_listener.h>

using namespace winrt::Windows::Foundation;

static UINT g_currentDPI = DPIAware::DEFAULT_DPI;

LRESULT CALLBACK EditKeyboardWindowProc(HWND, UINT, WPARAM, LPARAM);

// This Hwnd will be the window handler for the Xaml Island: A child window that contains Xaml.
HWND hWndXamlIslandEditKeyboardWindow = nullptr;

// This variable is used to check if window registration has been done to avoid repeated registration leading to an error.
bool isEditKeyboardWindowRegistrationCompleted = false;

// Holds the native window handle of EditKeyboard Window.
HWND hwndEditKeyboardNativeWindow = nullptr;
std::mutex editKeyboardWindowMutex;

// Stores a pointer to the Xaml Bridge object so that it can be accessed from the window procedure
static XamlBridge2* xamlBridgePtr = nullptr;

// Theming
static ThemeListener theme_listener{};

static void handleTheme()
{
    auto theme = theme_listener.AppTheme;
    auto isDark = theme == AppTheme::Dark;
    Logger::info(L"Theme is now {}", isDark ? L"Dark" : L"Light");
    if (hwndEditKeyboardNativeWindow != nullptr)
    {
        ThemeHelpers::SetImmersiveDarkMode(hwndEditKeyboardNativeWindow, isDark);
    }
}

static IAsyncOperation<bool> OrphanKeysConfirmationDialog(
    KBMEditor::KeyboardManagerState& state,
    const std::vector<DWORD>& keys,
    XamlRoot root)
{
    ContentDialog confirmationDialog;
    confirmationDialog.XamlRoot(root);
    confirmationDialog.Title(box_value(GET_RESOURCE_STRING(IDS_EDITKEYBOARD_ORPHANEDDIALOGTITLE)));
    confirmationDialog.Content(nullptr);
    confirmationDialog.IsPrimaryButtonEnabled(true);
    confirmationDialog.DefaultButton(ContentDialogButton::Primary);
    confirmationDialog.PrimaryButtonText(winrt::hstring(GET_RESOURCE_STRING(IDS_CONTINUE_BUTTON)));
    confirmationDialog.IsSecondaryButtonEnabled(true);
    confirmationDialog.SecondaryButtonText(winrt::hstring(GET_RESOURCE_STRING(IDS_CANCEL_BUTTON)));

    TextBlock orphanKeysBlock;
    std::wstring orphanKeyString;
    for (auto k : keys)
    {
        orphanKeyString.append(state.keyboardMap.GetKeyName(k));
        orphanKeyString.append(L", ");
    }

    orphanKeyString = orphanKeyString.substr(0, max(0, orphanKeyString.length() - 2));
    orphanKeysBlock.Text(winrt::hstring(orphanKeyString));
    orphanKeysBlock.TextWrapping(TextWrapping::Wrap);
    confirmationDialog.Content(orphanKeysBlock);

    ContentDialogResult res = co_await confirmationDialog.ShowAsync();

    co_return res == ContentDialogResult::Primary;
}

static IAsyncAction OnClickAccept(KBMEditor::KeyboardManagerState& keyboardManagerState, XamlRoot root, std::function<void()> ApplyRemappings)
{
    ShortcutErrorType isSuccess = LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(SingleKeyRemapControl::singleKeyRemapBuffer);

    if (isSuccess != ShortcutErrorType::NoError)
    {
        if (!co_await Dialog::PartialRemappingConfirmationDialog(root, GET_RESOURCE_STRING(IDS_EDITKEYBOARD_PARTIALCONFIRMATIONDIALOGTITLE)))
        {
            co_return;
        }
    }

    // Check for orphaned keys
    // Draw content Dialog
    std::vector<DWORD> orphanedKeys = LoadingAndSavingRemappingHelper::GetOrphanedKeys(SingleKeyRemapControl::singleKeyRemapBuffer);
    if (orphanedKeys.size() > 0)
    {
        if (!co_await OrphanKeysConfirmationDialog(keyboardManagerState, orphanedKeys, root))
        {
            co_return;
        }
    }

    ApplyRemappings();
}

// Function to create the Edit Keyboard Window
inline void CreateEditKeyboardWindowImpl(HINSTANCE hInst, KBMEditor::KeyboardManagerState& keyboardManagerState, MappingConfiguration& mappingConfiguration)
{
    Logger::trace("CreateEditKeyboardWindowImpl()");
    auto locker = EventLocker::Get(KeyboardManagerConstants::EditorWindowEventName.c_str());
    if (!locker.has_value())
    {
        Logger::error(L"Failed to lock event {}. {}", KeyboardManagerConstants::EditorWindowEventName, get_last_error_or_default(GetLastError()));
    }

    Logger::trace(L"Signaled {} event to suspend the KBM engine", KeyboardManagerConstants::EditorWindowEventName);

    // Window Registration
    const wchar_t szWindowClass[] = L"EditKeyboardWindow";
    if (!isEditKeyboardWindowRegistrationCompleted)
    {
        WNDCLASSEX windowClass = {};
        windowClass.cbSize = sizeof(WNDCLASSEX);
        windowClass.lpfnWndProc = EditKeyboardWindowProc;
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

        isEditKeyboardWindowRegistrationCompleted = true;
    }

    // Find coordinates of the screen where the settings window is placed.
    RECT desktopRect = UIHelpers::GetForegroundWindowDesktopRect();

    // Calculate DPI dependent window size
    float windowWidth = EditorConstants::DefaultEditKeyboardWindowWidth;
    float windowHeight = EditorConstants::DefaultEditKeyboardWindowHeight;

    DPIAware::ConvertByCursorPosition(windowWidth, windowHeight);
    DPIAware::GetScreenDPIForCursor(g_currentDPI);

    // Window Creation
    HWND _hWndEditKeyboardWindow = CreateWindow(
        szWindowClass,
        GET_RESOURCE_STRING(IDS_EDITKEYBOARD_WINDOWNAME).c_str(),
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MAXIMIZEBOX,
        ((desktopRect.right + desktopRect.left) / 2) - ((int)windowWidth / 2),
        ((desktopRect.bottom + desktopRect.top) / 2) - ((int)windowHeight / 2),
        static_cast<int>(windowWidth),
        static_cast<int>(windowHeight),
        NULL,
        NULL,
        hInst,
        NULL);

    if (_hWndEditKeyboardWindow == NULL)
    {
        MessageBox(NULL, GET_RESOURCE_STRING(IDS_CREATEWINDOWFAILED_ERRORMESSAGE).c_str(), GET_RESOURCE_STRING(IDS_CREATEWINDOWFAILED_ERRORTITLE).c_str(), NULL);
        return;
    }

    // Ensures the window is in foreground on first startup. If this is not done, the window appears behind because the thread is not on the foreground.
    if (_hWndEditKeyboardWindow)
    {
        SetForegroundWindow(_hWndEditKeyboardWindow);
    }

    // Store the newly created Edit Keyboard window's handle.
    std::unique_lock<std::mutex> hwndLock(editKeyboardWindowMutex);
    hwndEditKeyboardNativeWindow = _hWndEditKeyboardWindow;
    hwndLock.unlock();

    // Hide icon and caption from title bar
    const DWORD windowThemeOptionsMask = WTNCA_NODRAWCAPTION | WTNCA_NODRAWICON;
    WTA_OPTIONS windowThemeOptions{ windowThemeOptionsMask, windowThemeOptionsMask };
    SetWindowThemeAttribute(_hWndEditKeyboardWindow, WTA_NONCLIENT, &windowThemeOptions, sizeof(windowThemeOptions));

    handleTheme();
    theme_listener.AddChangedHandler(handleTheme);

    // Create the xaml bridge object
    XamlBridge2 xamlBridge(_hWndEditKeyboardWindow);

    // Create the desktop window xaml source object and set its content
    hWndXamlIslandEditKeyboardWindow = xamlBridge.InitBridge();

    // Set the pointer to the xaml bridge object
    xamlBridgePtr = &xamlBridge;

    // Header for the window
    Windows::UI::Xaml::Controls::RelativePanel header;
    header.Margin({ 10, 10, 10, 30 });

    // Header text
    TextBlock headerText;
    headerText.Text(GET_RESOURCE_STRING(IDS_EDITKEYBOARD_WINDOWNAME));
    headerText.FontSize(30);
    headerText.Margin({ 0, 0, 0, 0 });
    header.SetAlignLeftWithPanel(headerText, true);

    // Header Cancel button
    Button cancelButton;
    cancelButton.Content(winrt::box_value(GET_RESOURCE_STRING(IDS_CANCEL_BUTTON)));
    cancelButton.Margin({ 10, 0, 0, 0 });
    cancelButton.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        // Close the window since settings do not need to be saved
        PostMessage(_hWndEditKeyboardWindow, WM_CLOSE, 0, 0);
    });

    //  Text block for information about remap key section.
    TextBlock keyRemapInfoHeader;
    keyRemapInfoHeader.Text(GET_RESOURCE_STRING(IDS_EDITKEYBOARD_INFO));
    keyRemapInfoHeader.Margin({ 10, 0, 0, 10 });
    keyRemapInfoHeader.FontWeight(Text::FontWeights::SemiBold());
    keyRemapInfoHeader.TextWrapping(TextWrapping::Wrap);

    TextBlock keyRemapInfoExample;
    keyRemapInfoExample.Text(GET_RESOURCE_STRING(IDS_EDITKEYBOARD_INFOEXAMPLE));
    keyRemapInfoExample.Margin({ 10, 0, 0, 20 });
    keyRemapInfoExample.FontStyle(Text::FontStyle::Italic);
    keyRemapInfoExample.TextWrapping(TextWrapping::Wrap);

    // Table to display the key remaps
    StackPanel keyRemapTable;

    // First header textblock in the header row of the keys remap table
    TextBlock originalKeyRemapHeader;
    originalKeyRemapHeader.Text(GET_RESOURCE_STRING(IDS_EDITKEYBOARD_SOURCEHEADER));
    originalKeyRemapHeader.FontWeight(Text::FontWeights::Bold());
    StackPanel originalKeyHeaderContainer = UIHelpers::GetWrapped(originalKeyRemapHeader, EditorConstants::RemapTableDropDownWidth + EditorConstants::TableArrowColWidth).as<StackPanel>();

    // Second header textblock in the header row of the keys remap table
    TextBlock newKeyRemapHeader;
    newKeyRemapHeader.Text(GET_RESOURCE_STRING(IDS_EDITKEYBOARD_TARGETHEADER));
    newKeyRemapHeader.FontWeight(Text::FontWeights::Bold());

    StackPanel tableHeader = StackPanel();
    tableHeader.Orientation(Orientation::Horizontal);
    tableHeader.Margin({ 10, 0, 0, 10 });
    tableHeader.Children().Append(originalKeyHeaderContainer);
    tableHeader.Children().Append(newKeyRemapHeader);

    // Store handle of edit keyboard window
    SingleKeyRemapControl::EditKeyboardWindowHandle = _hWndEditKeyboardWindow;

    // Store keyboard manager state
    SingleKeyRemapControl::keyboardManagerState = &keyboardManagerState;
    KeyDropDownControl::keyboardManagerState = &keyboardManagerState;
    KeyDropDownControl::mappingConfiguration = &mappingConfiguration;

    // Clear the single key remap buffer
    SingleKeyRemapControl::singleKeyRemapBuffer.clear();

    // Vector to store dynamically allocated control objects to avoid early destruction
    std::vector<std::vector<std::unique_ptr<SingleKeyRemapControl>>> keyboardRemapControlObjects;

    // Set keyboard manager UI state so that remaps are not applied while on this window
    keyboardManagerState.SetUIState(KBMEditor::KeyboardManagerUIState::EditKeyboardWindowActivated, _hWndEditKeyboardWindow);

    // Load existing remaps into UI
    SingleKeyRemapTable singleKeyRemapCopy = mappingConfiguration.singleKeyReMap;
    SingleKeyToTextRemapTable singleKeyToTextRemapCopy = mappingConfiguration.singleKeyToTextReMap;

    LoadingAndSavingRemappingHelper::PreProcessRemapTable(singleKeyRemapCopy);
    LoadingAndSavingRemappingHelper::PreProcessRemapTable(singleKeyToTextRemapCopy);

    for (const auto& it : singleKeyRemapCopy)
    {
        SingleKeyRemapControl::AddNewControlKeyRemapRow(keyRemapTable, keyboardRemapControlObjects, it.first, it.second);
    }

    for (const auto& it : singleKeyToTextRemapCopy)
    {
        SingleKeyRemapControl::AddNewControlKeyRemapRow(keyRemapTable, keyboardRemapControlObjects, it.first, it.second);
    }

    // Main Header Apply button
    Button applyButton;
    applyButton.Content(winrt::box_value(GET_RESOURCE_STRING(IDS_OK_BUTTON)));
    applyButton.Style(AccentButtonStyle());
    applyButton.MinWidth(EditorConstants::HeaderButtonWidth);
    cancelButton.MinWidth(EditorConstants::HeaderButtonWidth);
    header.SetAlignRightWithPanel(cancelButton, true);
    header.SetLeftOf(applyButton, cancelButton);

    auto ApplyRemappings = [&mappingConfiguration, _hWndEditKeyboardWindow]() {
        LoadingAndSavingRemappingHelper::ApplySingleKeyRemappings(mappingConfiguration, SingleKeyRemapControl::singleKeyRemapBuffer, true);
        bool saveResult = mappingConfiguration.SaveSettingsToFile();
        PostMessage(_hWndEditKeyboardWindow, WM_CLOSE, 0, 0);
    };

    applyButton.Click([&keyboardManagerState, ApplyRemappings, applyButton](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        OnClickAccept(keyboardManagerState, applyButton.XamlRoot(), ApplyRemappings);
    });

    header.Children().Append(headerText);
    header.Children().Append(applyButton);
    header.Children().Append(cancelButton);

    ScrollViewer scrollViewer;
    scrollViewer.VerticalScrollMode(ScrollMode::Enabled);
    scrollViewer.HorizontalScrollMode(ScrollMode::Enabled);
    scrollViewer.VerticalScrollBarVisibility(ScrollBarVisibility::Auto);
    scrollViewer.HorizontalScrollBarVisibility(ScrollBarVisibility::Auto);

    // Add remap key button
    Windows::UI::Xaml::Controls::Button addRemapKey;
    addRemapKey.Margin({ 10, 10, 0, 25 });
    addRemapKey.Style(AccentButtonStyle());
    addRemapKey.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        SingleKeyRemapControl::AddNewControlKeyRemapRow(keyRemapTable, keyboardRemapControlObjects);

        // Whenever a remap is added move to the bottom of the screen
        scrollViewer.ChangeView(nullptr, scrollViewer.ScrollableHeight(), nullptr);

        // Set focus to the first "Select" Button in the newly added row
        UIHelpers::SetFocusOnFirstSelectButtonInLastRowOfEditKeyboardWindow(keyRemapTable, EditorConstants::RemapTableColCount);
    });

    // Remap key button content
    StackPanel addRemapKeyContent;
    addRemapKeyContent.Orientation(Orientation::Horizontal);
    addRemapKeyContent.Spacing(10);
    addRemapKeyContent.Children().Append(SymbolIcon(Symbol::Add));
    TextBlock addRemapKeyText;
    addRemapKeyText.Text(GET_RESOURCE_STRING(IDS_ADD_KEY_REMAP_BUTTON));
    addRemapKeyContent.Children().Append(addRemapKeyText);
    addRemapKey.Content(addRemapKeyContent);

    // Set accessible name for the addRemapKey button
    addRemapKey.SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_ADD_KEY_REMAP_BUTTON)));

    // Header and example text at the top of the window
    StackPanel helperText;
    helperText.Children().Append(keyRemapInfoHeader);
    helperText.Children().Append(keyRemapInfoExample);

    // Remapping table
    StackPanel mappingsPanel;
    mappingsPanel.Children().Append(tableHeader);
    mappingsPanel.Children().Append(keyRemapTable);
    mappingsPanel.Children().Append(addRemapKey);

    // Remapping table should be scrollable
    scrollViewer.Content(mappingsPanel);

    // Creating the Xaml content. xamlContainer is the parent UI element
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
    if (_hWndEditKeyboardWindow)
    {
        ShowWindow(_hWndEditKeyboardWindow, SW_SHOW);
        UpdateWindow(_hWndEditKeyboardWindow);
    }

    // Message loop:
    xamlBridge.MessageLoop();

    // Reset pointers to nullptr
    xamlBridgePtr = nullptr;
    hWndXamlIslandEditKeyboardWindow = nullptr;
    hwndLock.lock();
    theme_listener.DelChangedHandler(handleTheme);
    hwndEditKeyboardNativeWindow = nullptr;
    keyboardManagerState.ResetUIState();
    keyboardManagerState.ClearRegisteredKeyDelays();
}

void CreateEditKeyboardWindow(HINSTANCE hInst, KBMEditor::KeyboardManagerState& keyboardManagerState, MappingConfiguration& mappingConfiguration)
{
    // Move implementation into the separate method so resources get destroyed correctly
    CreateEditKeyboardWindowImpl(hInst, keyboardManagerState, mappingConfiguration);

    // Calling ClearXamlIslands() outside of the message loop is not enough to prevent
    // Microsoft.UI.XAML.dll from crashing during deinitialization, see https://github.com/microsoft/PowerToys/issues/10906
    Logger::trace("Terminating process {}", GetCurrentProcessId());
    Logger::flush();
    TerminateProcess(GetCurrentProcess(), 0);
}

LRESULT CALLBACK EditKeyboardWindowProc(HWND hWnd, UINT messageCode, WPARAM wParam, LPARAM lParam)
{
    switch (messageCode)
    {
    // Resize the XAML window whenever the parent window is painted or resized
    case WM_PAINT:
    case WM_SIZE:
    {
        RECT rect = { 0 };
        GetClientRect(hWnd, &rect);
        SetWindowPos(hWndXamlIslandEditKeyboardWindow, 0, rect.left, rect.top, rect.right, rect.bottom, SWP_SHOWWINDOW);
    }
    break;
    // To avoid UI elements overlapping on making the window smaller enforce a minimum window size
    case WM_GETMINMAXINFO:
    {
        LPMINMAXINFO mmi = reinterpret_cast<LPMINMAXINFO>(lParam);
        float minWidth = EditorConstants::MinimumEditKeyboardWindowWidth;
        float minHeight = EditorConstants::MinimumEditKeyboardWindowHeight;
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
bool CheckEditKeyboardWindowActive()
{
    bool result = false;
    std::unique_lock<std::mutex> hwndLock(editKeyboardWindowMutex);
    if (hwndEditKeyboardNativeWindow != nullptr)
    {
        // Check if the window is minimized if yes then restore the window.
        if (IsIconic(hwndEditKeyboardNativeWindow))
        {
            ShowWindow(hwndEditKeyboardNativeWindow, SW_RESTORE);
        }

        // If there is an already existing window no need to create a new open bring it on foreground.
        SetForegroundWindow(hwndEditKeyboardNativeWindow);
        result = true;
    }

    return result;
}

// Function to close any active Edit Keyboard window
void CloseActiveEditKeyboardWindow()
{
    std::unique_lock<std::mutex> hwndLock(editKeyboardWindowMutex);
    if (hwndEditKeyboardNativeWindow != nullptr)
    {
        Logger::trace("CloseActiveEditKeyboardWindow()");
        PostMessage(hwndEditKeyboardNativeWindow, WM_CLOSE, 0, 0);
    }
}
