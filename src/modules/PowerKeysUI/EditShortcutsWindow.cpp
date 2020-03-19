#include "EditShortcutsWindow.h"

LRESULT CALLBACK EditShortcutsWindowProc(HWND, UINT, WPARAM, LPARAM);
LRESULT CALLBACK DetectShortcutWindowProc(HWND, UINT, WPARAM, LPARAM);
void createDetectShortcutWindow(XamlRoot, TextBlock&, int*, HWND*);

HWND hWndXamlIslandEditShortcutsWindow = nullptr;
bool isEditShortcutsWindowRegistrationCompleted = false;
TextBlock detectShortcutTextBlock = nullptr;
std::vector<DWORD> detectedShortcuts;
std::unordered_map<DWORD, std::string> VKCodeToKeyName({ { 0x41, "A" }, { 0x53, "S" }, { 0x44, "D" }, { VK_LWIN, "LWin" }, { VK_LCONTROL, "LCtrl" }, { VK_LMENU, "LAlt" }, { VK_LSHIFT, "LShift" } });

void createEditShortcutsWindow(HINSTANCE hInst, int* uiFlag, HWND* detectWindowHandle)
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

    TextBlock header;
    header.Text(winrt::to_hstring("Edit Shortcuts"));
    header.FontSize(30);
    header.Margin({ 10, 10, 10, 30 });

    TextBlock header2;
    header2.Text(winrt::to_hstring("Original Shortcut:"));
    header2.FontWeight(Text::FontWeights::Bold());
    header2.Margin({ 10, 10, 10, 20 });

    TextBlock shortcutText;
    shortcutText.Margin({ 10 });
    Windows::UI::Xaml::Controls::Button bt;
    bt.Content(winrt::box_value(winrt::to_hstring("Type Shortcut")));
    bt.Margin({ 10 });
    bt.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        *detectWindowHandle = _hWndEditShortcutsWindow;
        // Using the XamlRoot of the bt to get the root of the XAML host
        createDetectShortcutWindow(bt.XamlRoot(), shortcutText, uiFlag, detectWindowHandle);
    });

    Windows::UI::Xaml::Controls::Button addShortcut;
    FontIcon plusSymbol;
    plusSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    plusSymbol.Glyph(L"\xE109");
    addShortcut.Content(plusSymbol);
    addShortcut.Margin({ 10 });
    addShortcut.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
    });

    xamlContainer.Children().Append(header);
    xamlContainer.Children().Append(header2);
    xamlContainer.Children().Append(bt);
    xamlContainer.Children().Append(shortcutText);
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

void createDetectShortcutWindow(XamlRoot xamlRoot, TextBlock& shortcutText, int* uiFlag, HWND* detectWindowHandle)
{
    ContentDialog detectShortcutBox;

    // ContentDialog requires manually setting the XamlRoot (https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
    detectShortcutBox.XamlRoot(xamlRoot);
    detectShortcutBox.Title(box_value(L"Press the keys in shortcut:"));
    detectShortcutBox.PrimaryButtonText(to_hstring(L"OK"));
    detectShortcutBox.IsSecondaryButtonEnabled(false);
    detectShortcutBox.CloseButtonText(to_hstring(L"Cancel"));
    detectShortcutBox.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    detectShortcutBox.PrimaryButtonClick([=](Windows::UI::Xaml::Controls::ContentDialog const& sender, ContentDialogButtonClickEventArgs const&) {
        hstring shortcutString;
        for (int i = 0; i < detectedShortcuts.size(); i++)
        {
            if (VKCodeToKeyName.find(detectedShortcuts[i]) != VKCodeToKeyName.end())
            {
                shortcutString = shortcutString + to_hstring(VKCodeToKeyName[detectedShortcuts[i]]) + to_hstring(L" ");
            }
            else
            {
                shortcutString = shortcutString + to_hstring((unsigned int)detectedShortcuts[i]) + to_hstring(L" ");
            }
        }

        shortcutText.Text(shortcutString);
        if (uiFlag != nullptr)
        {
            *uiFlag = 0;
        }
        *detectWindowHandle = nullptr;
    });
    detectShortcutBox.CloseButtonClick([=](Windows::UI::Xaml::Controls::ContentDialog const& sender, ContentDialogButtonClickEventArgs const&) {
        if (uiFlag != nullptr)
        {
            *uiFlag = 0;
        }
        *detectWindowHandle = nullptr;
    });

    Windows::UI::Xaml::Controls::StackPanel stackPanel;
    stackPanel.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });

    TextBlock text;
    text.Text(winrt::to_hstring("Keys Pressed:"));
    text.Margin({ 0, 0, 0, 10 });
    TextBlock shortcutKeys;
    detectShortcutTextBlock = shortcutKeys;

    stackPanel.Children().Append(text);
    stackPanel.Children().Append(shortcutKeys);
    stackPanel.UpdateLayout();
    detectShortcutBox.Content(stackPanel);
    if (uiFlag != nullptr)
    {
        *uiFlag = 2;
    }

    detectShortcutBox.ShowAsync();
}

void updateDetectShortcutTextBlock(std::vector<DWORD>& shortcutKeys)
{
    if (detectShortcutTextBlock == nullptr)
    {
        return;
    }

    detectedShortcuts = shortcutKeys;

    hstring shortcutString;
    for (int i = 0; i < shortcutKeys.size(); i++)
    {
        if (VKCodeToKeyName.find(shortcutKeys[i]) != VKCodeToKeyName.end())
        {
            shortcutString = shortcutString + to_hstring(VKCodeToKeyName[shortcutKeys[i]]) + to_hstring(L" ");
        }
        else
        {
            shortcutString = shortcutString + to_hstring((unsigned int)shortcutKeys[i]) + to_hstring(L" ");
        }
    }

    detectShortcutTextBlock.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [=]() {
        detectShortcutTextBlock.Text(shortcutString);
    });
}