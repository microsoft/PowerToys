#include "EditShortcutsWindow.h"
#include "ShortcutControl.h"

LRESULT CALLBACK EditShortcutsWindowProc(HWND, UINT, WPARAM, LPARAM);
LRESULT CALLBACK DetectShortcutWindowProc(HWND, UINT, WPARAM, LPARAM);

HWND hWndXamlIslandEditShortcutsWindow = nullptr;
bool isEditShortcutsWindowRegistrationCompleted = false;

void createEditShortcutsWindow(HINSTANCE hInst, KeyboardManagerState& keyboardManagerState)
{
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

    HWND _hWndEditShortcutsWindow = CreateWindow(
        szWindowClass,
        L"PowerKeys Edit Shortcuts",
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

    // This DesktopWindowXamlSource is the object that enables a non-UWP desktop application
    // to host UWP controls in any UI element that is associated with a window handle (HWND).
    DesktopWindowXamlSource desktopSource;
    // Get handle to corewindow
    auto interop = desktopSource.as<IDesktopWindowXamlSourceNative>();
    // Parent the DesktopWindowXamlSource object to current window
    check_hresult(interop->AttachToWindow(_hWndEditShortcutsWindow));

    // Get the new child window's hwnd
    interop->get_WindowHandle(&hWndXamlIslandEditShortcutsWindow);
    // Update the xaml island window size becuase initially is 0,0
    SetWindowPos(hWndXamlIslandEditShortcutsWindow, 0, 0, 0, 400, 400, SWP_SHOWWINDOW);

    //Creating the Xaml content
    Windows::UI::Xaml::Controls::StackPanel xamlContainer;
    xamlContainer.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });

    Windows::UI::Xaml::Controls::StackPanel header;
    header.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    header.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);
    header.Margin({ 10, 10, 10, 30 });
    header.Spacing(10);

    TextBlock headerText;
    headerText.Text(winrt::to_hstring("Edit Shortcuts"));
    headerText.FontSize(30);
    headerText.Margin({ 0, 0, 100, 0 });

    Button cancelButton;
    cancelButton.Content(winrt::box_value(winrt::to_hstring("Cancel")));
    cancelButton.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        // Close the window since settings do not need to be saved
        PostMessage(_hWndEditShortcutsWindow, WM_CLOSE, 0, 0);
    });

    Windows::UI::Xaml::Controls::StackPanel shortcutTable;
    shortcutTable.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    shortcutTable.Margin({ 10, 10, 10, 20 });
    shortcutTable.Spacing(10);

    Windows::UI::Xaml::Controls::StackPanel tableHeaderRow;
    tableHeaderRow.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    tableHeaderRow.Spacing(100);
    tableHeaderRow.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);

    TextBlock originalShortcutHeader;
    originalShortcutHeader.Text(winrt::to_hstring("Original Shortcut:"));
    originalShortcutHeader.FontWeight(Text::FontWeights::Bold());
    originalShortcutHeader.Margin({ 0, 0, 0, 10 });
    tableHeaderRow.Children().Append(originalShortcutHeader);

    TextBlock newShortcutHeader;
    newShortcutHeader.Text(winrt::to_hstring("New Shortcut:"));
    newShortcutHeader.FontWeight(Text::FontWeights::Bold());
    newShortcutHeader.Margin({ 0, 0, 0, 10 });
    tableHeaderRow.Children().Append(newShortcutHeader);

    shortcutTable.Children().Append(tableHeaderRow);

    TextBlock settingsMessage;
    Button applyButton;
    applyButton.Content(winrt::box_value(winrt::to_hstring("Apply")));
    applyButton.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        bool isSuccess = true;
        keyboardManagerState.ClearOSLevelShortcuts();
        for (int i = 1; i < shortcutTable.Children().Size(); i++)
        {
            StackPanel currentRow = shortcutTable.Children().GetAt(i).as<StackPanel>();
            hstring originalShortcut = currentRow.Children().GetAt(0).as<StackPanel>().Children().GetAt(1).as<TextBlock>().Text();
            hstring newShortcut = currentRow.Children().GetAt(1).as<StackPanel>().Children().GetAt(1).as<TextBlock>().Text();
            if (!originalShortcut.empty() && !newShortcut.empty())
            {
                std::vector<DWORD> originalKeys = convertWStringVectorToNumberType<DWORD>(splitwstring(originalShortcut.c_str(), L' '));
                std::vector<WORD> newKeys = convertWStringVectorToNumberType<WORD>(splitwstring(newShortcut.c_str(), L' '));

                // Check if number of keys is two since only that is currently implemented
                if (originalKeys.size() == 2 && newKeys.size() == 2)
                {
                    keyboardManagerState.AddOSLevelShortcut(originalKeys, newKeys);
                }
                else
                {
                    isSuccess = false;
                }
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

    ShortcutControl::_hWndEditShortcutsWindow = _hWndEditShortcutsWindow;
    ShortcutControl::keyboardManagerState = &keyboardManagerState;

    // Load existing shortcuts into UI
    for (const auto& it: keyboardManagerState.osLevelShortcutReMap)
    {
        ShortcutControl::AddNewShortcutControlRow(shortcutTable, it.first, it.second.first);
    }

    Windows::UI::Xaml::Controls::Button addShortcut;
    FontIcon plusSymbol;
    plusSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    plusSymbol.Glyph(L"\xE109");
    addShortcut.Content(plusSymbol);
    addShortcut.Margin({ 10 });
    addShortcut.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        ShortcutControl::AddNewShortcutControlRow(shortcutTable);
    });

    xamlContainer.Children().Append(header);
    xamlContainer.Children().Append(shortcutTable);
    xamlContainer.Children().Append(addShortcut);
    xamlContainer.UpdateLayout();
    desktopSource.Content(xamlContainer);

    ////End XAML Island section
    if (_hWndEditShortcutsWindow)
    {
        ShowWindow(_hWndEditShortcutsWindow, SW_SHOW);
        UpdateWindow(_hWndEditShortcutsWindow);
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

LRESULT CALLBACK EditShortcutsWindowProc(HWND hWnd, UINT messageCode, WPARAM wParam, LPARAM lParam)
{
    RECT rcClient;
    switch (messageCode)
    {
    case WM_PAINT:
        GetClientRect(hWnd, &rcClient);
        SetWindowPos(hWndXamlIslandEditShortcutsWindow, 0, rcClient.left, rcClient.top, rcClient.right, rcClient.bottom, SWP_SHOWWINDOW);
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
