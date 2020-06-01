#include "pch.h"
#include "EditShortcutsWindow.h"
#include "ShortcutControl.h"
#include "KeyDropDownControl.h"
#include "XamlBridge.h"
#include <keyboardmanager/common/trace.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <common/windows_colors.h>
#include <common/dpi_aware.h>
#include "Styles.h"
#include "Dialog.h"
#include <keyboardmanager/dll/resource.h>

using namespace winrt::Windows::Foundation;

LRESULT CALLBACK EditShortcutsWindowProc(HWND, UINT, WPARAM, LPARAM);

// This Hwnd will be the window handler for the Xaml Island: A child window that contains Xaml.
HWND hWndXamlIslandEditShortcutsWindow = nullptr;
// This variable is used to check if window registration has been done to avoid repeated registration leading to an error.
bool isEditShortcutsWindowRegistrationCompleted = false;
// Holds the native window handle of EditShortcuts Window.
HWND hwndEditShortcutsNativeWindow = nullptr;
std::mutex editShortcutsWindowMutex;
// Stores a pointer to the Xaml Bridge object so that it can be accessed from the window procedure
static XamlBridge* xamlBridgePtr = nullptr;

static IAsyncAction OnClickAccept(
    KeyboardManagerState& keyboardManagerState,
    XamlRoot root,
    std::function<void()> ApplyRemappings)
{
    KeyboardManagerHelper::ErrorType isSuccess = Dialog::CheckIfRemappingsAreValid<Shortcut>(
        ShortcutControl::shortcutRemapBuffer,
        [](Shortcut shortcut) {
            return shortcut.IsValidShortcut();
        });
    if (isSuccess != KeyboardManagerHelper::ErrorType::NoError)
    {
        if (!co_await Dialog::PartialRemappingConfirmationDialog(root))
        {
            co_return;
        }
    }
    ApplyRemappings();
}

// Function to create the Edit Shortcuts Window
void createEditShortcutsWindow(HINSTANCE hInst, KeyboardManagerState& keyboardManagerState)
{
    // Window Registration
    const wchar_t szWindowClass[] = L"EditShortcutsWindow";

    if (!isEditShortcutsWindowRegistrationCompleted)
    {
        WNDCLASSEX windowClass = {};
        windowClass.cbSize = sizeof(WNDCLASSEX);
        windowClass.lpfnWndProc = EditShortcutsWindowProc;
        windowClass.hInstance = hInst;
        windowClass.lpszClassName = szWindowClass;
        windowClass.hbrBackground = (HBRUSH)(COLOR_WINDOW);
        windowClass.hIcon = (HICON)LoadImageW(
            windowClass.hInstance,
            MAKEINTRESOURCE(IDS_KEYBOARDMANAGER_ICON),
            IMAGE_ICON,
            48,
            48,
            LR_DEFAULTCOLOR);
        if (RegisterClassEx(&windowClass) == NULL)
        {
            MessageBox(NULL, L"Windows registration failed!", L"Error", NULL);
            return;
        }

        isEditShortcutsWindowRegistrationCompleted = true;
    }

    // Find center screen coordinates
    RECT desktopRect;
    GetClientRect(GetDesktopWindow(), &desktopRect);
    // Calculate DPI dependent window size
    int windowWidth = KeyboardManagerConstants::DefaultEditShortcutsWindowWidth;
    int windowHeight = KeyboardManagerConstants::DefaultEditShortcutsWindowHeight;
    DPIAware::Convert(nullptr, windowWidth, windowHeight);

    // Window Creation
    HWND _hWndEditShortcutsWindow = CreateWindow(
        szWindowClass,
        L"Remap shortcuts",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MAXIMIZEBOX,
        (desktopRect.right / 2) - (windowWidth / 2),
        (desktopRect.bottom / 2) - (windowHeight / 2),
        windowWidth,
        windowHeight,
        NULL,
        NULL,
        hInst,
        NULL);
    if (_hWndEditShortcutsWindow == NULL)
    {
        MessageBox(NULL, L"Call to CreateWindow failed!", L"Error", NULL);
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

    // Create the xaml bridge object
    XamlBridge xamlBridge(_hWndEditShortcutsWindow);
    // DesktopSource needs to be declared before the RelativePanel xamlContainer object to avoid errors
    winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource desktopSource;
    // Create the desktop window xaml source object and set its content
    hWndXamlIslandEditShortcutsWindow = xamlBridge.InitDesktopWindowsXamlSource(desktopSource);

    // Set the pointer to the xaml bridge object
    xamlBridgePtr = &xamlBridge;

    // Header for the window
    Windows::UI::Xaml::Controls::RelativePanel header;
    header.Margin({ 10, 10, 10, 30 });

    // Header text
    TextBlock headerText;
    headerText.Text(L"Remap shortcuts");
    headerText.FontSize(30);
    headerText.Margin({ 0, 0, 0, 0 });
    header.SetAlignLeftWithPanel(headerText, true);

    // Cancel button
    Button cancelButton;
    cancelButton.Content(winrt::box_value(L"Cancel"));
    cancelButton.Margin({ 10, 0, 0, 0 });
    cancelButton.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        // Close the window since settings do not need to be saved
        PostMessage(_hWndEditShortcutsWindow, WM_CLOSE, 0, 0);
    });

    //  Text block for information about remap key section.
    TextBlock shortcutRemapInfoHeader;
    shortcutRemapInfoHeader.Text(L"Select shortcut you want to change (Shortcut) and the shortcut you want it to invoke (Mapped To).");
    shortcutRemapInfoHeader.Margin({ 10, 0, 0, 10 });
    shortcutRemapInfoHeader.FontWeight(Text::FontWeights::SemiBold());
    shortcutRemapInfoHeader.TextWrapping(TextWrapping::Wrap);

    TextBlock shortcutRemapInfoExample;
    shortcutRemapInfoExample.Text(L"For example, if you want Ctrl+C to paste, Ctrl+C is the \"Shortcut\" and Ctrl+V would be your \"Mapped To\".");
    shortcutRemapInfoExample.Margin({ 10, 0, 0, 20 });
    shortcutRemapInfoExample.FontStyle(Text::FontStyle::Italic);
    shortcutRemapInfoExample.TextWrapping(TextWrapping::Wrap);

    // Table to display the shortcuts
    Windows::UI::Xaml::Controls::Grid shortcutTable;
    Grid keyRemapTable;
    ColumnDefinition originalColumn;
    originalColumn.MinWidth(3 * KeyboardManagerConstants::ShortcutTableDropDownWidth + 2 * KeyboardManagerConstants::ShortcutTableDropDownSpacing);
    originalColumn.MaxWidth(3 * KeyboardManagerConstants::ShortcutTableDropDownWidth + 2 * KeyboardManagerConstants::ShortcutTableDropDownSpacing);
    ColumnDefinition arrowColumn;
    arrowColumn.MinWidth(KeyboardManagerConstants::TableArrowColWidth);
    ColumnDefinition newColumn;
    newColumn.MinWidth(3 * KeyboardManagerConstants::ShortcutTableDropDownWidth + 2 * KeyboardManagerConstants::ShortcutTableDropDownSpacing);
    newColumn.MaxWidth(3 * KeyboardManagerConstants::ShortcutTableDropDownWidth + 2 * KeyboardManagerConstants::ShortcutTableDropDownSpacing);
    ColumnDefinition removeColumn;
    removeColumn.MinWidth(KeyboardManagerConstants::TableRemoveColWidth);
    shortcutTable.Margin({ 10, 10, 10, 20 });
    shortcutTable.HorizontalAlignment(HorizontalAlignment::Stretch);
    shortcutTable.ColumnDefinitions().Append(originalColumn);
    shortcutTable.ColumnDefinitions().Append(arrowColumn);
    shortcutTable.ColumnDefinitions().Append(newColumn);
    shortcutTable.ColumnDefinitions().Append(removeColumn);
    shortcutTable.RowDefinitions().Append(RowDefinition());

    // First header textblock in the header row of the shortcut table
    TextBlock originalShortcutHeader;
    originalShortcutHeader.Text(L"Shortcut:");
    originalShortcutHeader.FontWeight(Text::FontWeights::Bold());
    originalShortcutHeader.Margin({ 0, 0, 0, 10 });

    // Second header textblock in the header row of the shortcut table
    TextBlock newShortcutHeader;
    newShortcutHeader.Text(L"Mapped To:");
    newShortcutHeader.FontWeight(Text::FontWeights::Bold());
    newShortcutHeader.Margin({ 0, 0, 0, 10 });

    shortcutTable.SetColumn(originalShortcutHeader, KeyboardManagerConstants::ShortcutTableOriginalColIndex);
    shortcutTable.SetRow(originalShortcutHeader, 0);
    shortcutTable.SetColumn(newShortcutHeader, KeyboardManagerConstants::ShortcutTableNewColIndex);
    shortcutTable.SetRow(newShortcutHeader, 0);

    shortcutTable.Children().Append(originalShortcutHeader);
    shortcutTable.Children().Append(newShortcutHeader);

    // Store handle of edit shortcuts window
    ShortcutControl::EditShortcutsWindowHandle = _hWndEditShortcutsWindow;
    // Store keyboard manager state
    ShortcutControl::keyboardManagerState = &keyboardManagerState;
    KeyDropDownControl::keyboardManagerState = &keyboardManagerState;
    // Clear the shortcut remap buffer
    ShortcutControl::shortcutRemapBuffer.clear();
    // Vector to store dynamically allocated control objects to avoid early destruction
    std::vector<std::vector<std::unique_ptr<ShortcutControl>>> keyboardRemapControlObjects;

    // Set keyboard manager UI state so that shortcut remaps are not applied while on this window
    keyboardManagerState.SetUIState(KeyboardManagerUIState::EditShortcutsWindowActivated, _hWndEditShortcutsWindow);

    // Load existing shortcuts into UI
    std::unique_lock<std::mutex> lock(keyboardManagerState.osLevelShortcutReMap_mutex);
    for (const auto& it : keyboardManagerState.osLevelShortcutReMap)
    {
        ShortcutControl::AddNewShortcutControlRow(shortcutTable, keyboardRemapControlObjects, it.first, it.second.targetShortcut);
    }
    lock.unlock();

    // Apply button
    Button applyButton;
    applyButton.Content(winrt::box_value(L"OK"));
    applyButton.Style(AccentButtonStyle());
    applyButton.MinWidth(KeyboardManagerConstants::HeaderButtonWidth);
    cancelButton.MinWidth(KeyboardManagerConstants::HeaderButtonWidth);
    header.SetAlignRightWithPanel(cancelButton, true);
    header.SetLeftOf(applyButton, cancelButton);

    auto ApplyRemappings = [&keyboardManagerState, _hWndEditShortcutsWindow]() {
        KeyboardManagerHelper::ErrorType isSuccess = KeyboardManagerHelper::ErrorType::NoError;
        // Clear existing shortcuts
        keyboardManagerState.ClearOSLevelShortcuts();
        DWORD successfulRemapCount = 0;
        // Save the shortcuts that are valid and report if any of them were invalid
        for (int i = 0; i < ShortcutControl::shortcutRemapBuffer.size(); i++)
        {
            Shortcut originalShortcut = ShortcutControl::shortcutRemapBuffer[i][0];
            Shortcut newShortcut = ShortcutControl::shortcutRemapBuffer[i][1];

            if (originalShortcut.IsValidShortcut() && newShortcut.IsValidShortcut())
            {
                bool result = keyboardManagerState.AddOSLevelShortcut(originalShortcut, newShortcut);
                if (!result)
                {
                    isSuccess = KeyboardManagerHelper::ErrorType::RemapUnsuccessful;
                    // Tooltip is already shown for this row
                }
                else
                {
                    successfulRemapCount += 1;
                }
            }
            else
            {
                isSuccess = KeyboardManagerHelper::ErrorType::RemapUnsuccessful;
            }
        }

        Trace::OSLevelShortcutRemapCount(successfulRemapCount);
        // Save the updated key remaps to file.
        bool saveResult = keyboardManagerState.SaveConfigToFile();
        if (!saveResult)
        {
            isSuccess = KeyboardManagerHelper::ErrorType::SaveFailed;
        }

        PostMessage(_hWndEditShortcutsWindow, WM_CLOSE, 0, 0);
    };

    applyButton.Click([&keyboardManagerState, applyButton, ApplyRemappings](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        OnClickAccept(keyboardManagerState, applyButton.XamlRoot(), ApplyRemappings);
    });

    header.Children().Append(headerText);
    header.Children().Append(applyButton);
    header.Children().Append(cancelButton);

    ScrollViewer scrollViewer;

    // Add shortcut button
    Windows::UI::Xaml::Controls::Button addShortcut;
    FontIcon plusSymbol;
    plusSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    plusSymbol.Glyph(L"\xE109");
    addShortcut.Content(plusSymbol);
    addShortcut.Margin({ 10, 0, 0, 25 });
    addShortcut.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ShortcutControl::AddNewShortcutControlRow(shortcutTable, keyboardRemapControlObjects);
        // Whenever a remap is added move to the bottom of the screen
        scrollViewer.ChangeView(nullptr, scrollViewer.ScrollableHeight(), nullptr);
    });

    StackPanel mappingsPanel;
    mappingsPanel.Children().Append(shortcutRemapInfoHeader);
    mappingsPanel.Children().Append(shortcutRemapInfoExample);
    mappingsPanel.Children().Append(shortcutTable);
    mappingsPanel.Children().Append(addShortcut);

    scrollViewer.Content(mappingsPanel);

    RelativePanel xamlContainer;
    xamlContainer.SetBelow(scrollViewer, header);
    xamlContainer.SetAlignLeftWithPanel(header, true);
    xamlContainer.SetAlignRightWithPanel(header, true);
    xamlContainer.SetAlignLeftWithPanel(scrollViewer, true);
    xamlContainer.SetAlignRightWithPanel(scrollViewer, true);
    xamlContainer.Children().Append(header);
    xamlContainer.Children().Append(scrollViewer);
    xamlContainer.UpdateLayout();

    desktopSource.Content(xamlContainer);

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

    // Cannot be done in WM_DESTROY because that causes crashes due to fatal app exit
    xamlBridge.ClearXamlIslands();
}

LRESULT CALLBACK EditShortcutsWindowProc(HWND hWnd, UINT messageCode, WPARAM wParam, LPARAM lParam)
{
    RECT rcClient;
    switch (messageCode)
    {
    // Resize the XAML window whenever the parent window is painted or resized
    case WM_PAINT:
    case WM_SIZE:
        GetClientRect(hWnd, &rcClient);
        SetWindowPos(hWndXamlIslandEditShortcutsWindow, 0, rcClient.left, rcClient.top, rcClient.right, rcClient.bottom, SWP_SHOWWINDOW);
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
