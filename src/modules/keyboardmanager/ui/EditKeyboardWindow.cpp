#include "pch.h"
#include "EditKeyboardWindow.h"
#include "SingleKeyRemapControl.h"

LRESULT CALLBACK EditKeyboardWindowProc(HWND, UINT, WPARAM, LPARAM);

// This Hwnd will be the window handler for the Xaml Island: A child window that contains Xaml.
HWND hWndXamlIslandEditKeyboardWindow = nullptr;
// This variable is used to check if window registration has been done to avoid repeated registration leading to an error.
bool isEditKeyboardWindowRegistrationCompleted = false;

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
        L"PowerKeys Remap Keyboard",
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
    xamlContainer.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::AliceBlue() });

    // Header for the window
    Windows::UI::Xaml::Controls::StackPanel header;
    header.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);
    header.Margin({ 10, 10, 10, 30 });
    header.Spacing(10);

    // Header text
    TextBlock headerText;
    headerText.Text(winrt::to_hstring("Remap Keyboard"));
    headerText.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Black() });
    headerText.FontSize(30);
    headerText.Margin({ 0, 0, 1000, 0 });

    // Header Cancel button
    Button cancelButton;
    cancelButton.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    cancelButton.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Black() });
    cancelButton.Content(winrt::box_value(winrt::to_hstring("Cancel")));
    cancelButton.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        // Close the window since settings do not need to be saved
        PostMessage(_hWndEditKeyboardWindow, WM_CLOSE, 0, 0);
    });

    //  Text block for information about remap key section.
    TextBlock keyRemapInfoHeader;
    keyRemapInfoHeader.Text(winrt::to_hstring("Select the key you want to remap, original key, and it's new output when pressed, the new key"));
    keyRemapInfoHeader.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Black() });
    keyRemapInfoHeader.Margin({ 0, 0, 0, 10 });

    // Table to display the key remaps
    Windows::UI::Xaml::Controls::StackPanel keyRemapTable;
    keyRemapTable.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    keyRemapTable.Margin({ 10, 10, 10, 20 });
    keyRemapTable.Spacing(10);

    // Header row of the keys remap table
    Windows::UI::Xaml::Controls::StackPanel tableHeaderRow;
    tableHeaderRow.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::AliceBlue() });
    tableHeaderRow.Spacing(100);
    tableHeaderRow.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);

    // First header textblock in the header row of the keys remap table
    TextBlock originalKeyRemapHeader;
    originalKeyRemapHeader.Text(winrt::to_hstring("Original Key:"));
    originalKeyRemapHeader.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Black() });
    originalKeyRemapHeader.FontWeight(Text::FontWeights::Bold());
    originalKeyRemapHeader.Margin({ 0, 0, 0, 10 });
    tableHeaderRow.Children().Append(originalKeyRemapHeader);

    // Second header textblock in the header row of the keys remap table
    TextBlock newKeyRemapHeader;
    newKeyRemapHeader.Text(winrt::to_hstring("New Key:"));
    newKeyRemapHeader.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Black() });
    newKeyRemapHeader.FontWeight(Text::FontWeights::Bold());
    newKeyRemapHeader.Margin({ 0, 0, 0, 10 });
    tableHeaderRow.Children().Append(newKeyRemapHeader);

    keyRemapTable.Children().Append(tableHeaderRow);

    // Message to display success/failure of saving settings.
    TextBlock settingsMessage;

    // Main Header Apply button
    Button applyButton;
    applyButton.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    applyButton.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Black() });
    applyButton.Content(winrt::box_value(winrt::to_hstring("Apply")));
    applyButton.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        bool isSuccess = true;
        // Clear existing Key Remaps
        keyboardManagerState.ClearSingleKeyRemaps();

        // Save the keys that are valid and report if any of them were invalid
        for (unsigned int i = 1; i < keyRemapTable.Children().Size(); i++)
        {
            StackPanel currentRow = keyRemapTable.Children().GetAt(i).as<StackPanel>();
            hstring originalKeyString = currentRow.Children().GetAt(0).as<StackPanel>().Children().GetAt(1).as<TextBlock>().Text();
            hstring newKeyString = currentRow.Children().GetAt(1).as<StackPanel>().Children().GetAt(1).as<TextBlock>().Text();
            if (!originalKeyString.empty() && !newKeyString.empty())
            {
                DWORD originalKey = std::stoi(originalKeyString.c_str());
                DWORD newKey = std::stoi(newKeyString.c_str());
                keyboardManagerState.AddSingleKeyRemap(originalKey, newKey);
            }
            else
            {
                isSuccess = false;
            }
        }

        if (isSuccess)
        {
            settingsMessage.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Green() });
            settingsMessage.Text(winrt::to_hstring("Remapping successful!"));
        }
        else
        {
            settingsMessage.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Red() });
            settingsMessage.Text(winrt::to_hstring("All remappings were not successfully applied."));
        }
    });

    header.Children().Append(headerText);
    header.Children().Append(cancelButton);
    header.Children().Append(applyButton);
    header.Children().Append(settingsMessage);

    // Store handle of edit keyboard window
    SingleKeyRemapControl::EditKeyboardWindowHandle = _hWndEditKeyboardWindow;
    // Store keyboard manager state
    SingleKeyRemapControl::keyboardManagerState = &keyboardManagerState;

    // Load existing remaps into UI
    for (const auto& it : keyboardManagerState.singleKeyReMap)
    {
        SingleKeyRemapControl::AddNewControlKeyRemapRow(keyRemapTable, it.first, it.second);
    }

    // Add remap key button
    Windows::UI::Xaml::Controls::Button addRemapKey;
    addRemapKey.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    addRemapKey.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Black() });
    FontIcon plusSymbol;
    plusSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    plusSymbol.Glyph(L"\xE109");
    addRemapKey.Content(plusSymbol);
    addRemapKey.Margin({ 10 });
    addRemapKey.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        SingleKeyRemapControl::AddNewControlKeyRemapRow(keyRemapTable);
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
