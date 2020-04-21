#include "pch.h"
#include "EditKeyboardWindow.h"
#include "SingleKeyRemapControl.h"
#include "KeyDropDownControl.h"

LRESULT CALLBACK EditKeyboardWindowProc(HWND, UINT, WPARAM, LPARAM);

// This Hwnd will be the window handler for the Xaml Island: A child window that contains Xaml.
HWND hWndXamlIslandEditKeyboardWindow = nullptr;
// This variable is used to check if window registration has been done to avoid repeated registration leading to an error.
bool isEditKeyboardWindowRegistrationCompleted = false;
// Holds the native window handle of EditKeyboard Window.
HWND hwndEditKeyboardNativeWindow = nullptr;
std::mutex editKeyboardWindowMutex;

// Function to create the Edit Keyboard Window
void createEditKeyboardWindow(HINSTANCE hInst, KeyboardManagerState& keyboardManagerState)
{
    // Window Registration
    const wchar_t szWindowClass[] = L"EditKeyboardWindow";
    if (!isEditKeyboardWindowRegistrationCompleted)
    {
        WNDCLASSEX windowClass = {};
        windowClass.cbSize = sizeof(WNDCLASSEX);
        windowClass.lpfnWndProc = EditKeyboardWindowProc;
        windowClass.hInstance = hInst;
        windowClass.lpszClassName = szWindowClass;
        windowClass.hbrBackground = (HBRUSH)(COLOR_WINDOW);
        windowClass.hIconSm = LoadIcon(windowClass.hInstance, IDI_APPLICATION);
        if (RegisterClassEx(&windowClass) == NULL)
        {
            MessageBox(NULL, L"Windows registration failed!", L"Error", NULL);
            return;
        }

        isEditKeyboardWindowRegistrationCompleted = true;
    }

    // Window Creation
    HWND _hWndEditKeyboardWindow = CreateWindow(
        szWindowClass,
        L"Remap Keyboard",
        WS_OVERLAPPEDWINDOW | WS_VISIBLE,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        NULL,
        NULL,
        hInst,
        NULL);
    if (_hWndEditKeyboardWindow == NULL)
    {
        MessageBox(NULL, L"Call to CreateWindow failed!", L"Error", NULL);
        return;
    }

    // Store the newly created Edit Keyboard window's handle.
    std::unique_lock<std::mutex> hwndLock(editKeyboardWindowMutex);
    hwndEditKeyboardNativeWindow = _hWndEditKeyboardWindow;
    hwndLock.unlock();

    // This DesktopWindowXamlSource is the object that enables a non-UWP desktop application
    // to host UWP controls in any UI element that is associated with a window handle (HWND).
    DesktopWindowXamlSource desktopSource;
    // Get handle to corewindow
    auto interop = desktopSource.as<IDesktopWindowXamlSourceNative>();
    // Parent the DesktopWindowXamlSource object to current window
    check_hresult(interop->AttachToWindow(_hWndEditKeyboardWindow));

    // Get the new child window's hwnd
    interop->get_WindowHandle(&hWndXamlIslandEditKeyboardWindow);
    // Update the xaml island window size becuase initially is 0,0
    SetWindowPos(hWndXamlIslandEditKeyboardWindow, 0, 0, 0, 400, 400, SWP_SHOWWINDOW);

    // Creating the Xaml content. xamlContainer is the parent UI element
    Windows::UI::Xaml::Controls::StackPanel xamlContainer;

    // Header for the window
    Windows::UI::Xaml::Controls::StackPanel header;
    header.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);
    header.Margin({ 10, 10, 10, 30 });
    header.Spacing(10);

    // Header text
    TextBlock headerText;
    headerText.Text(winrt::to_hstring("Remap Keyboard"));
    headerText.FontSize(30);
    headerText.Margin({ 0, 0, 1000, 0 });

    // Header Cancel button
    Button cancelButton;
    cancelButton.Content(winrt::box_value(winrt::to_hstring("Cancel")));
    cancelButton.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        // Close the window since settings do not need to be saved
        PostMessage(_hWndEditKeyboardWindow, WM_CLOSE, 0, 0);
    });

    //  Text block for information about remap key section.
    TextBlock keyRemapInfoHeader;
    keyRemapInfoHeader.Text(winrt::to_hstring("Select the key you want to remap, original key, and it's new output when pressed, the new key"));
    keyRemapInfoHeader.Margin({ 10, 0, 0, 10 });

    // Table to display the key remaps
    Windows::UI::Xaml::Controls::StackPanel keyRemapTable;
    keyRemapTable.Margin({ 10, 10, 10, 20 });
    keyRemapTable.Spacing(10);

    // Header row of the keys remap table
    Windows::UI::Xaml::Controls::StackPanel tableHeaderRow;
    tableHeaderRow.Spacing(100);
    tableHeaderRow.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);

    // First header textblock in the header row of the keys remap table
    TextBlock originalKeyRemapHeader;
    originalKeyRemapHeader.Text(winrt::to_hstring("Original Key:"));
    originalKeyRemapHeader.FontWeight(Text::FontWeights::Bold());
    originalKeyRemapHeader.Margin({ 0, 0, 0, 10 });
    tableHeaderRow.Children().Append(originalKeyRemapHeader);

    // Second header textblock in the header row of the keys remap table
    TextBlock newKeyRemapHeader;
    newKeyRemapHeader.Text(winrt::to_hstring("New Key:"));
    newKeyRemapHeader.FontWeight(Text::FontWeights::Bold());
    newKeyRemapHeader.Margin({ 0, 0, 0, 10 });
    tableHeaderRow.Children().Append(newKeyRemapHeader);

    keyRemapTable.Children().Append(tableHeaderRow);

    // Message to display success/failure of saving settings.
    Flyout applyFlyout;
    TextBlock settingsMessage;
    applyFlyout.Content(settingsMessage);

    // Store handle of edit keyboard window
    SingleKeyRemapControl::EditKeyboardWindowHandle = _hWndEditKeyboardWindow;
    // Store keyboard manager state
    SingleKeyRemapControl::keyboardManagerState = &keyboardManagerState;
    KeyDropDownControl::keyboardManagerState = &keyboardManagerState;
    // Clear the single key remap buffer
    SingleKeyRemapControl::singleKeyRemapBuffer.clear();
    // Vector to store dynamically allocated control objects to avoid early destruction
    std::vector<std::vector<std::unique_ptr<SingleKeyRemapControl>>> keyboardRemapControlObjects;

    // Load existing remaps into UI
    std::unique_lock<std::mutex> lock(keyboardManagerState.singleKeyReMap_mutex);
    std::unordered_map<DWORD, DWORD> singleKeyRemapCopy = keyboardManagerState.singleKeyReMap;
    lock.unlock();

    // Pre process the table to combine L and R versions of Ctrl/Alt/Shift that are mapped to the same key
    if (singleKeyRemapCopy.find(VK_LCONTROL) != singleKeyRemapCopy.end() && singleKeyRemapCopy.find(VK_RCONTROL) != singleKeyRemapCopy.end())
    {
        // If they are mapped to the same key, delete those entries and set the common version
        if (singleKeyRemapCopy[VK_LCONTROL] == singleKeyRemapCopy[VK_RCONTROL])
        {
            singleKeyRemapCopy[VK_CONTROL] = singleKeyRemapCopy[VK_LCONTROL];
            singleKeyRemapCopy.erase(VK_LCONTROL);
            singleKeyRemapCopy.erase(VK_RCONTROL);
        }
    }
    if (singleKeyRemapCopy.find(VK_LMENU) != singleKeyRemapCopy.end() && singleKeyRemapCopy.find(VK_RMENU) != singleKeyRemapCopy.end())
    {
        // If they are mapped to the same key, delete those entries and set the common version
        if (singleKeyRemapCopy[VK_LMENU] == singleKeyRemapCopy[VK_RMENU])
        {
            singleKeyRemapCopy[VK_MENU] = singleKeyRemapCopy[VK_LMENU];
            singleKeyRemapCopy.erase(VK_LMENU);
            singleKeyRemapCopy.erase(VK_RMENU);
        }
    }
    if (singleKeyRemapCopy.find(VK_LSHIFT) != singleKeyRemapCopy.end() && singleKeyRemapCopy.find(VK_RSHIFT) != singleKeyRemapCopy.end())
    {
        // If they are mapped to the same key, delete those entries and set the common version
        if (singleKeyRemapCopy[VK_LSHIFT] == singleKeyRemapCopy[VK_RSHIFT])
        {
            singleKeyRemapCopy[VK_SHIFT] = singleKeyRemapCopy[VK_LSHIFT];
            singleKeyRemapCopy.erase(VK_LSHIFT);
            singleKeyRemapCopy.erase(VK_RSHIFT);
        }
    }

    for (const auto& it : singleKeyRemapCopy)
    {
        SingleKeyRemapControl::AddNewControlKeyRemapRow(keyRemapTable, keyboardRemapControlObjects, it.first, it.second);
    }

    // Main Header Apply button
    Button applyButton;
    applyButton.Content(winrt::box_value(winrt::to_hstring("Apply")));
    applyButton.Flyout(applyFlyout);
    applyButton.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        bool isSuccess = true;
        // Clear existing Key Remaps
        keyboardManagerState.ClearSingleKeyRemaps();

        for (int i = 0; i < SingleKeyRemapControl::singleKeyRemapBuffer.size(); i++)
        {
            DWORD originalKey = SingleKeyRemapControl::singleKeyRemapBuffer[i][0];
            DWORD newKey = SingleKeyRemapControl::singleKeyRemapBuffer[i][1];

            if (originalKey != NULL && newKey != NULL)
            {
                // If Ctrl/Alt/Shift are added, add their L and R versions instead to the same key
                bool result = false;
                bool res1, res2;
                switch (originalKey)
                {
                case VK_CONTROL:
                    res1 = keyboardManagerState.AddSingleKeyRemap(VK_LCONTROL, newKey);
                    res2 = keyboardManagerState.AddSingleKeyRemap(VK_RCONTROL, newKey);
                    result = res1 && res2;
                    break;
                case VK_MENU:
                    res1 = keyboardManagerState.AddSingleKeyRemap(VK_LMENU, newKey);
                    res2 = keyboardManagerState.AddSingleKeyRemap(VK_RMENU, newKey);
                    result = res1 && res2;
                    break;
                case VK_SHIFT:
                    res1 = keyboardManagerState.AddSingleKeyRemap(VK_LSHIFT, newKey);
                    res2 = keyboardManagerState.AddSingleKeyRemap(VK_RSHIFT, newKey);
                    result = res1 && res2;
                    break;
                default:
                    result = keyboardManagerState.AddSingleKeyRemap(originalKey, newKey);
                }

                if (!result)
                {
                    isSuccess = false;
                }
            }
            else
            {
                isSuccess = false;
            }
        }

        // Save the updated shortcuts remaps to file.
        auto saveResult = keyboardManagerState.SaveConfigToFile();

        if (isSuccess && saveResult)
        {
            settingsMessage.Text(winrt::to_hstring("Remapping successful"));
        }
        else if (!isSuccess && saveResult)
        {
            settingsMessage.Text(winrt::to_hstring("All remappings were not successfully applied"));
        }
        else
        {
            settingsMessage.Text(L"Failed to save the remappings.");
        }
    });

    header.Children().Append(headerText);
    header.Children().Append(cancelButton);
    header.Children().Append(applyButton);

    // Add remap key button
    Windows::UI::Xaml::Controls::Button addRemapKey;
    FontIcon plusSymbol;
    plusSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    plusSymbol.Glyph(L"\xE109");
    addRemapKey.Content(plusSymbol);
    addRemapKey.Margin({ 10 });
    addRemapKey.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        SingleKeyRemapControl::AddNewControlKeyRemapRow(keyRemapTable, keyboardRemapControlObjects);
    });

    xamlContainer.Children().Append(header);
    xamlContainer.Children().Append(keyRemapInfoHeader);
    xamlContainer.Children().Append(keyRemapTable);
    xamlContainer.Children().Append(addRemapKey);
    xamlContainer.UpdateLayout();
    desktopSource.Content(xamlContainer);

    ////End XAML Island section
    if (_hWndEditKeyboardWindow)
    {
        ShowWindow(_hWndEditKeyboardWindow, SW_SHOW);
        UpdateWindow(_hWndEditKeyboardWindow);
    }

    // Message loop:
    MSG msg = {};
    while (GetMessage(&msg, NULL, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
    desktopSource.Close();

    hWndXamlIslandEditKeyboardWindow = nullptr;
    hwndLock.lock();
    hwndEditKeyboardNativeWindow = nullptr;
}

LRESULT CALLBACK EditKeyboardWindowProc(HWND hWnd, UINT messageCode, WPARAM wParam, LPARAM lParam)
{
    RECT rcClient;
    switch (messageCode)
    {
    case WM_PAINT:
        GetClientRect(hWnd, &rcClient);
        SetWindowPos(hWndXamlIslandEditKeyboardWindow, 0, rcClient.left, rcClient.top, rcClient.right, rcClient.bottom, SWP_SHOWWINDOW);
        break;
    case WM_DESTROY:
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProc(hWnd, messageCode, wParam, lParam);
        break;
    }

    return 0;
}

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
