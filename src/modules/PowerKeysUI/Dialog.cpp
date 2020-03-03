#include "Dialog.h"

LRESULT CALLBACK DialogWindowProc(HWND, UINT, WPARAM, LPARAM);

// This Hwnd will be the window handler for the Xaml Island: A child window that contains Xaml.  
DesktopWindowXamlSource *desktopSourceptr = nullptr;
void registerWinClass(HINSTANCE &hInst)
{
    // The main window class name.
    const wchar_t szWindowClass[] = L"Win32Dialog";
    WNDCLASSEX windowClass = { };

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

HWND createDialog(HINSTANCE &hInst)
{
    // The main window class name.
    const wchar_t szWindowClass[] = L"Win32Dialog";
    HWND hWndXamlIsland1 = nullptr;
    HWND _hWnd1 = CreateWindow(
        szWindowClass,
        L"Windows c++ Win32 Desktop App",
        WS_OVERLAPPEDWINDOW | WS_VISIBLE,
        CW_USEDEFAULT, CW_USEDEFAULT, 400, 400,
        NULL,
        NULL,
        hInst,
        NULL
    );
    if (_hWnd1 == NULL)
    {
        MessageBox(NULL, L"Call to CreateWindow failed!", L"Error", NULL);
        return 0;
    }
    // Initialize the Xaml Framework's corewindow for current thread
    WindowsXamlManager winxamlmanager = WindowsXamlManager::InitializeForCurrentThread();


    // This DesktopWindowXamlSource is the object that enables a non-UWP desktop application 
    // to host UWP controls in any UI element that is associated with a window handle (HWND).
    DesktopWindowXamlSource desktopSource;
    // Get handle to corewindow
    auto interop = desktopSource.as<IDesktopWindowXamlSourceNative>();
    // Parent the DesktopWindowXamlSource object to current window
    check_hresult(interop->AttachToWindow(_hWnd1));


    // Get the new child window's hwnd 
    interop->get_WindowHandle(&hWndXamlIsland1);
    // Update the xaml island window size becuase initially is 0,0
    SetWindowPos(hWndXamlIsland1, 0, 0, 0, 400, 400, SWP_SHOWWINDOW);

    //Creating the Xaml content
    Windows::UI::Xaml::Controls::StackPanel xamlContainer;
    xamlContainer.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    Windows::UI::Xaml::Controls::Button bt;
    bt.Content(winrt::box_value(winrt::to_hstring("Don't Type key")));
    xamlContainer.Children().Append(bt);
    xamlContainer.UpdateLayout();
    desktopSource.Content(xamlContainer);
    ////End XAML Island section



    ShowWindow(_hWnd1, SW_SHOW);
    UpdateWindow(_hWnd1);

    // Message loop:
    MSG msg = { };
    while (GetMessage(&msg, NULL, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
    desktopSource.Close();
    winxamlmanager.Close();

    //return 0;
    return _hWnd1;
}

LRESULT CALLBACK DialogWindowProc(HWND hWnd, UINT messageCode, WPARAM wParam, LPARAM lParam)
{

    switch (messageCode)
    {
    //case WM_PAINT:

    //    GetClientRect(hWnd, &rcClient);
    //    SetWindowPos(hWndXamlIsland, 0, rcClient.left, rcClient.top, rcClient.right, rcClient.bottom, SWP_SHOWWINDOW);
    //    break;
    case WM_DESTROY:
        PostQuitMessage(0);
        break;

    //    //creating main window  
    //case WM_CREATE:
    //    _childhWnd = CreateWindowEx(0, L"ChildWClass", NULL, WS_CHILD | WS_BORDER, 0, 0, 0, 0, hWnd, NULL, _hInstance, NULL);
    //    return 0;
    //    // main window changed size 
    //case WM_SIZE:
    //    // Get the dimensions of the main window's client 
    //    // area, and enumerate the child windows. Pass the 
    //    // dimensions to the child windows during enumeration. 


    //    return 0;

    //    // Process other messages. 


    default:
        return DefWindowProc(hWnd, messageCode, wParam, lParam);
        break;
    }

    return 0;
}

