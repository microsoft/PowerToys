#include "Dialog.h"

LRESULT CALLBACK DialogWindowProc(HWND, UINT, WPARAM, LPARAM);

HWND hWndXamlIslandDialog = nullptr;

void registerWinClass(HINSTANCE& hInst)
{
    // The main window class name.
    const wchar_t szWindowClass[] = L"DialogWindow";
    WNDCLASSEX windowClass = {};

    windowClass.cbSize = sizeof(WNDCLASSEX);
    windowClass.lpfnWndProc = DialogWindowProc;
    windowClass.hInstance = hInst;
    windowClass.lpszClassName = szWindowClass;
    windowClass.hbrBackground = (HBRUSH)(COLOR_WINDOW);

    windowClass.hIconSm = LoadIcon(windowClass.hInstance, IDI_APPLICATION);

    if (RegisterClassEx(&windowClass) == NULL)
    {
        MessageBox(NULL, L"Windows registration failed!", L"Error", NULL);
        return;
    }
}

void createDialog(HINSTANCE hInst)
{
    // The main window class name.
    const wchar_t szWindowClass[] = L"DialogWindow";
    HWND _hWndDialog = CreateWindow(
        szWindowClass,
        L"PowerKeys Remap Keyboard",
        WS_OVERLAPPEDWINDOW | WS_VISIBLE,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        400,
        400,
        NULL,
        NULL,
        hInst,
        NULL);
    if (_hWndDialog == NULL)
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
    check_hresult(interop->AttachToWindow(_hWndDialog));

    // Get the new child window's hwnd
    interop->get_WindowHandle(&hWndXamlIslandDialog);
    // Update the xaml island window size becuase initially is 0,0
    SetWindowPos(hWndXamlIslandDialog, 0, 0, 0, 400, 400, SWP_SHOWWINDOW);

    //Creating the Xaml content
    Windows::UI::Xaml::Controls::StackPanel xamlContainer;
    xamlContainer.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    Windows::UI::Xaml::Controls::Button bt;
    bt.Content(winrt::box_value(winrt::to_hstring("Don't Type key")));
    xamlContainer.Children().Append(bt);
    xamlContainer.UpdateLayout();
    desktopSource.Content(xamlContainer);
    ////End XAML Island section
    if (_hWndDialog)
    {
        ShowWindow(_hWndDialog, SW_SHOW);
        UpdateWindow(_hWndDialog);
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

LRESULT CALLBACK DialogWindowProc(HWND hWnd, UINT messageCode, WPARAM wParam, LPARAM lParam)
{
    RECT rcClient;
    switch (messageCode)
    {
    case WM_PAINT:
        GetClientRect(hWnd, &rcClient);
        SetWindowPos(hWndXamlIslandDialog, 0, rcClient.left, rcClient.top, rcClient.right, rcClient.bottom, SWP_SHOWWINDOW);
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
