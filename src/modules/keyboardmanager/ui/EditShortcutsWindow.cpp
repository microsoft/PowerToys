#include "EditShortcutsWindow.h"
#include "ShortcutControl.h"
LRESULT CALLBACK EditShortcutsWindowProc(HWND, UINT, WPARAM, LPARAM);
LRESULT CALLBACK DetectShortcutWindowProc(HWND, UINT, WPARAM, LPARAM);
//void createDetectShortcutWindow(IInspectable const&, XamlRoot, KeyboardManagerState&);

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

    Button applyButton;
    applyButton.Content(winrt::box_value(winrt::to_hstring("Apply")));
    applyButton.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
    });

    header.Children().Append(headerText);
    header.Children().Append(cancelButton);
    header.Children().Append(applyButton);

    Windows::UI::Xaml::Controls::StackPanel originalShortcutColumn;
    originalShortcutColumn.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    originalShortcutColumn.Margin({ 10 });
    originalShortcutColumn.Spacing(10);

    TextBlock header2;
    header2.Text(winrt::to_hstring("Original Shortcut:"));
    header2.FontWeight(Text::FontWeights::Bold());
    header2.Margin({ 0, 0, 0, 10 });
    ShortcutControl::_hWndEditShortcutsWindow = _hWndEditShortcutsWindow;

    Windows::UI::Xaml::Controls::Button addShortcut;
    FontIcon plusSymbol;
    plusSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    plusSymbol.Glyph(L"\xE109");
    addShortcut.Content(plusSymbol);
    addShortcut.Margin({ 10 });
    addShortcut.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        ShortcutControl sc(keyboardManagerState);
        sc.AddToParent(originalShortcutColumn);
    });

    xamlContainer.Children().Append(header);
    originalShortcutColumn.Children().Append(header2);
    xamlContainer.Children().Append(originalShortcutColumn);
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

//IInspectable getSiblingElement(IInspectable const& element)
//{
//    FrameworkElement frameworkElement = element.as<FrameworkElement>();
//    StackPanel parentElement = frameworkElement.Parent().as<StackPanel>();
//    uint32_t index;
//
//    parentElement.Children().IndexOf(frameworkElement, index);
//    return parentElement.Children().GetAt(index + 1);
//}

//void createDetectShortcutWindow(IInspectable const& sender, XamlRoot xamlRoot, KeyboardManagerState& keyboardManagerState)
//{
//    ContentDialog detectShortcutBox;
//
//    // ContentDialog requires manually setting the XamlRoot (https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
//    detectShortcutBox.XamlRoot(xamlRoot);
//    detectShortcutBox.Title(box_value(L"Press the keys in shortcut:"));
//    detectShortcutBox.PrimaryButtonText(to_hstring(L"OK"));
//    detectShortcutBox.IsSecondaryButtonEnabled(false);
//    detectShortcutBox.CloseButtonText(to_hstring(L"Cancel"));
//    detectShortcutBox.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
//
//    TextBlock linkedShortcutText = getSiblingElement(sender).as<TextBlock>();
//
//    detectShortcutBox.PrimaryButtonClick([=, &keyboardManagerState](Windows::UI::Xaml::Controls::ContentDialog const& sender, ContentDialogButtonClickEventArgs const&) {
//        hstring shortcutString;
//        for (int i = 0; i < detectedShortcuts.size(); i++)
//        {
//            if (VKCodeToKeyName.find(detectedShortcuts[i]) != VKCodeToKeyName.end())
//            {
//                shortcutString = shortcutString + to_hstring(VKCodeToKeyName[detectedShortcuts[i]]) + to_hstring(L" ");
//            }
//            else
//            {
//                shortcutString = shortcutString + to_hstring((unsigned int)detectedShortcuts[i]) + to_hstring(L" ");
//            }
//        }
//        linkedShortcutText.Text(shortcutString);
//        keyboardManagerState.ResetUIState();
//    });
//    detectShortcutBox.CloseButtonClick([=, &keyboardManagerState](Windows::UI::Xaml::Controls::ContentDialog const& sender, ContentDialogButtonClickEventArgs const&) {
//        keyboardManagerState.ResetUIState();
//    });
//
//    Windows::UI::Xaml::Controls::StackPanel stackPanel;
//    stackPanel.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
//
//    TextBlock text;
//    text.Text(winrt::to_hstring("Keys Pressed:"));
//    text.Margin({ 0, 0, 0, 10 });
//    TextBlock shortcutKeys;
//    detectShortcutTextBlock = shortcutKeys;
//
//    stackPanel.Children().Append(text);
//    stackPanel.Children().Append(shortcutKeys);
//    stackPanel.UpdateLayout();
//    detectShortcutBox.Content(stackPanel);
//
//    detectShortcutBox.ShowAsync();
//}
//
//void updateDetectShortcutTextBlock(std::vector<DWORD>& shortcutKeys)
//{
//    if (detectShortcutTextBlock == nullptr)
//    {
//        return;
//    }
//
//    detectedShortcuts = shortcutKeys;
//
//    hstring shortcutString;
//    for (int i = 0; i < shortcutKeys.size(); i++)
//    {
//        if (VKCodeToKeyName.find(shortcutKeys[i]) != VKCodeToKeyName.end())
//        {
//            shortcutString = shortcutString + to_hstring(VKCodeToKeyName[shortcutKeys[i]]) + to_hstring(L" ");
//        }
//        else
//        {
//            shortcutString = shortcutString + to_hstring((unsigned int)shortcutKeys[i]) + to_hstring(L" ");
//        }
//    }
//
//    detectShortcutTextBlock.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [=]() {
//        detectShortcutTextBlock.Text(shortcutString);
//    });
//}