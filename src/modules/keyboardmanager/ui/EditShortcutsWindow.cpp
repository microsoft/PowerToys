#include "pch.h"
#include "EditShortcutsWindow.h"
#include "ShortcutControl.h"
#include "KeyDropDownControl.h"
#include "XamlBridge.h"
#include <keyboardmanager/common/trace.h>

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
        windowClass.hIconSm = LoadIcon(windowClass.hInstance, IDI_APPLICATION);
        if (RegisterClassEx(&windowClass) == NULL)
        {
            MessageBox(NULL, L"Windows registration failed!", L"Error", NULL);
            return;
        }

        isEditShortcutsWindowRegistrationCompleted = true;
    }

    // Window Creation
    HWND _hWndEditShortcutsWindow = CreateWindow(
        szWindowClass,
        L"Edit Shortcuts",
        WS_OVERLAPPEDWINDOW | WS_VISIBLE,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        NULL,
        NULL,
        hInst,
        NULL);
    if (_hWndEditShortcutsWindow == NULL)
    {
        MessageBox(NULL, L"Call to CreateWindow failed!", L"Error", NULL);
        return;
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
    headerText.Text(L"Edit Shortcuts");
    headerText.FontSize(30);
    headerText.Margin({ 0, 0, 100, 0 });
    header.SetAlignLeftWithPanel(headerText, true);

    // Cancel button
    Button cancelButton;
    cancelButton.Content(winrt::box_value(L"Cancel"));
    cancelButton.Margin({ 0, 0, 10, 0 });
    cancelButton.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        // Close the window since settings do not need to be saved
        PostMessage(_hWndEditShortcutsWindow, WM_CLOSE, 0, 0);
    });

    //  Text block for information about remap key section.
    TextBlock shortcutRemapInfoHeader;
    shortcutRemapInfoHeader.Text(L"Select shortcut you want to change (Original Shortcut) and the shortcut (New Shortcut) you want it to invoke.");
    shortcutRemapInfoHeader.Margin({ 10, 0, 0, 10 });
    shortcutRemapInfoHeader.FontWeight(Text::FontWeights::SemiBold());

    TextBlock shortcutRemapInfoExample;
    shortcutRemapInfoExample.Text(L"For example, if you want Ctrl+C to paste, Ctrl+C is the Original Shortcut and Ctrl+V is the New Shortcut.");
    shortcutRemapInfoExample.Margin({ 10, 0, 0, 20 });
    shortcutRemapInfoExample.FontStyle(Text::FontStyle::Italic);

    // Table to display the shortcuts
    Windows::UI::Xaml::Controls::Grid shortcutTable;
    ColumnDefinition firstColumn;
    ColumnDefinition secondColumn;
    ColumnDefinition thirdColumn;
    thirdColumn.MaxWidth(100);
    ColumnDefinition fourthColumn;
    fourthColumn.MaxWidth(100);
    shortcutTable.Margin({ 10, 10, 10, 20 });
    shortcutTable.HorizontalAlignment(HorizontalAlignment::Stretch);
    shortcutTable.ColumnSpacing(10);
    shortcutTable.ColumnDefinitions().Append(firstColumn);
    shortcutTable.ColumnDefinitions().Append(secondColumn);
    shortcutTable.ColumnDefinitions().Append(thirdColumn);
    shortcutTable.ColumnDefinitions().Append(fourthColumn);
    shortcutTable.RowDefinitions().Append(RowDefinition());

    // First header textblock in the header row of the shortcut table
    TextBlock originalShortcutHeader;
    originalShortcutHeader.Text(L"Original Shortcut:");
    originalShortcutHeader.FontWeight(Text::FontWeights::Bold());
    originalShortcutHeader.Margin({ 0, 0, 0, 10 });

    // Second header textblock in the header row of the shortcut table
    TextBlock newShortcutHeader;
    newShortcutHeader.Text(L"New Shortcut:");
    newShortcutHeader.FontWeight(Text::FontWeights::Bold());
    newShortcutHeader.Margin({ 0, 0, 0, 10 });

    shortcutTable.SetColumn(originalShortcutHeader, 0);
    shortcutTable.SetRow(originalShortcutHeader, 0);
    shortcutTable.SetColumn(newShortcutHeader, 1);
    shortcutTable.SetRow(newShortcutHeader, 0);

    shortcutTable.Children().Append(originalShortcutHeader);
    shortcutTable.Children().Append(newShortcutHeader);

    // Message to display success/failure of saving settings.
    Flyout applyFlyout;
    TextBlock settingsMessage;
    applyFlyout.Content(settingsMessage);

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
    applyButton.Content(winrt::box_value(L"Apply"));
    header.SetAlignRightWithPanel(applyButton, true);
    header.SetLeftOf(cancelButton, applyButton);
    applyButton.Flyout(applyFlyout);
    applyButton.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
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
                // Show tooltip warning on the problematic row
                uint32_t warningIndex;
                // 2 at start, 4 in each row, and last element of each row
                warningIndex = 1 + (i + 1) * 4;
                FontIcon warning = shortcutTable.Children().GetAt(warningIndex).as<FontIcon>();
                ToolTip t = ToolTipService::GetToolTip(warning).as<ToolTip>();
                t.Content(box_value(KeyboardManagerHelper::GetErrorMessage(KeyboardManagerHelper::ErrorType::MissingKey)));
                warning.Visibility(Visibility::Visible);
            }
        }

        // Save the updated key remaps to file.
        bool saveResult = keyboardManagerState.SaveConfigToFile();
        if (!saveResult)
        {
            isSuccess = KeyboardManagerHelper::ErrorType::SaveFailed;
        }
        Trace::OSLevelShortcutRemapCount(successfulRemapCount);
        settingsMessage.Text(KeyboardManagerHelper::GetErrorMessage(isSuccess));
    });

    header.Children().Append(headerText);
    header.Children().Append(cancelButton);
    header.Children().Append(applyButton);

    // Add shortcut button
    Windows::UI::Xaml::Controls::Button addShortcut;
    FontIcon plusSymbol;
    plusSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    plusSymbol.Glyph(L"\xE109");
    addShortcut.Content(plusSymbol);
    addShortcut.Margin({ 10, 0, 0, 25 });
    addShortcut.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        ShortcutControl::AddNewShortcutControlRow(shortcutTable, keyboardRemapControlObjects);
    });

    StackPanel mappingsPanel;
    mappingsPanel.Children().Append(shortcutRemapInfoHeader);
    mappingsPanel.Children().Append(shortcutRemapInfoExample);
    mappingsPanel.Children().Append(shortcutTable);
    mappingsPanel.Children().Append(addShortcut);

    ScrollViewer scrollViewer;
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
