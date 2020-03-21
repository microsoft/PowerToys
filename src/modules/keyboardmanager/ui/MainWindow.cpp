#include "pch.h"
#include "MainWindow.h"
#include "EditKeyboardWindow.h"
#include "EditShortcutsWindow.h"

HWND _hWndMain;
HINSTANCE _hInstance;
// This Hwnd will be the window handler for the Xaml Island: A child window that contains Xaml.
HWND hWndXamlIslandMain = nullptr;
// This variable is used to check if window registration has been done to avoid repeated registration leading to an error.
bool isMainWindowRegistrationCompleted = false;

// Function to create the Main Window
void createMainWindow(HINSTANCE hInstance, KeyboardManagerState& keyboardManagerState)
{
    _hInstance = hInstance;

    // Window Registration
    const wchar_t szWindowClass[] = L"MainWindowClass";
    if (!isMainWindowRegistrationCompleted)
    {
        WNDCLASSEX windowClass = {};
        windowClass.cbSize = sizeof(WNDCLASSEX);
        windowClass.lpfnWndProc = MainWindowProc;
        windowClass.hInstance = hInstance;
        windowClass.lpszClassName = szWindowClass;
        windowClass.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
        windowClass.hIconSm = LoadIcon(windowClass.hInstance, IDI_APPLICATION);
        if (RegisterClassEx(&windowClass) == NULL)
        {
            MessageBox(NULL, L"Windows registration failed!", L"Error", NULL);
            return;
        }

        isMainWindowRegistrationCompleted = true;
    }

    // Window Creation
    _hWndMain = CreateWindow(
        szWindowClass,
        L"PowerKeys Settings",
        WS_OVERLAPPEDWINDOW | WS_VISIBLE,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        NULL,
        NULL,
        hInstance,
        NULL);
    if (_hWndMain == NULL)
    {
        MessageBox(NULL, L"Call to CreateWindow failed!", L"Error", NULL);
        return;
    }

    //XAML Island section
    // This DesktopWindowXamlSource is the object that enables a non-UWP desktop application
    // to host UWP controls in any UI element that is associated with a window handle (HWND).
    DesktopWindowXamlSource desktopSource;
    // Get handle to corewindow
    auto interop = desktopSource.as<IDesktopWindowXamlSourceNative>();
    // Parent the DesktopWindowXamlSource object to current window
    check_hresult(interop->AttachToWindow(_hWndMain));

    // Get the new child window's hwnd
    interop->get_WindowHandle(&hWndXamlIslandMain);
    // Update the xaml island window size becuase initially is 0,0
    SetWindowPos(hWndXamlIslandMain, 0, 0, 0, 800, 800, SWP_SHOWWINDOW);

    //Creating the Xaml content
    Windows::UI::Xaml::Controls::StackPanel xamlContainer;
    xamlContainer.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });

    Windows::UI::Xaml::Controls::StackPanel keyRow;
    keyRow.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);
    keyRow.Spacing(10);
    keyRow.Margin({ 10 });

    Windows::Foundation::Collections::IVector<Windows::Foundation::IInspectable> keyNames{ single_threaded_vector<Windows::Foundation::IInspectable>(
        { winrt::box_value(L"Alt"),
          winrt::box_value(L"Delete"),
          winrt::box_value(L"LAlt"),
          winrt::box_value(L"LWin"),
          winrt::box_value(L"Shift"),
          winrt::box_value(L"NumLock"),
          winrt::box_value(L"LCtrl") }) };
    Windows::UI::Xaml::Controls::ComboBox cb;
    cb.IsEditable(true);
    cb.Width(200);
    cb.ItemsSource(keyNames);

    Windows::UI::Xaml::Controls::Button bt;
    bt.Content(winrt::box_value(winrt::to_hstring("Edit Keyboard")));
    bt.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        //keyboardManagerState.SetUIState(KeyboardManagerUIState::DetectKeyWindowActivated);
        std::thread th(createEditKeyboardWindow, _hInstance, std::ref(keyboardManagerState));
        th.join();
        //keyboardManagerState.ResetUIState();
    });

    Windows::UI::Xaml::Controls::Button bt2;
    bt2.Content(winrt::box_value(winrt::to_hstring("Edit Shortcuts")));
    bt2.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        std::thread th(createEditShortcutsWindow, _hInstance, std::ref(keyboardManagerState));
        th.join();
    });

    keyRow.Children().Append(cb);
    keyRow.Children().Append(bt);
    keyRow.Children().Append(bt2);

    xamlContainer.Children().Append(keyRow);
    xamlContainer.UpdateLayout();
    desktopSource.Content(xamlContainer);
    //End XAML Island section
    if (_hWndMain)
    {
        ShowWindow(_hWndMain, SW_SHOW);
        UpdateWindow(_hWndMain);
    }

    //Message loop:
    MSG msg = {};
    while (GetMessage(&msg, NULL, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
    desktopSource.Close();
}

LRESULT CALLBACK MainWindowProc(HWND hWnd, UINT messageCode, WPARAM wParam, LPARAM lParam)
{
    RECT rcClient;

    switch (messageCode)
    {
    case WM_PAINT:
        GetClientRect(hWnd, &rcClient);
        SetWindowPos(hWndXamlIslandMain, 0, rcClient.left, rcClient.top, rcClient.right, rcClient.bottom, SWP_SHOWWINDOW);
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
