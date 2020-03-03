#include "HelloWindowsDesktop.h"
#include "Dialog.h"

using namespace winrt;
using namespace Windows::UI;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::Foundation::Numerics;
using namespace Windows::Foundation;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;

HWND _hWnd;
HWND _childhWnd;
HINSTANCE _hInstance;
// This Hwnd will be the window handler for the Xaml Island: A child window that contains Xaml.
HWND hWndXamlIsland = nullptr;
HWND retsom()
{
    return createDialog(_hInstance);
}
int APIENTRY wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ LPWSTR lpCmdLine, _In_ int nCmdShow)
{
    UILogic(hInstance, nullptr);
    return 0;
}

void UILogic(HINSTANCE hInstance, bool* ptr)
{
    _hInstance = hInstance;
    registerWinClass(_hInstance);
    // The main window class name.
    const wchar_t szWindowClass[] = L"Win32DesktopApp";
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
        //return 0;
    }

    _hWnd = CreateWindow(
        szWindowClass,
        L"Windows c++ Win32 Desktop App",
        WS_OVERLAPPEDWINDOW | WS_VISIBLE,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        NULL,
        NULL,
        hInstance,
        NULL);
    if (_hWnd == NULL)
    {
        MessageBox(NULL, L"Call to CreateWindow failed!", L"Error", NULL);
        //return 0;
    }

    //XAML Island section

    // The call to winrt::init_apartment initializes COM; by default, in a multithreaded apartment.
    //winrt::init_apartment(apartment_type::single_threaded);

    //// Initialize the Xaml Framework's corewindow for current thread
    //WindowsXamlManager winxamlmanager = WindowsXamlManager::InitializeForCurrentThread();

    // This DesktopWindowXamlSource is the object that enables a non-UWP desktop application
    // to host UWP controls in any UI element that is associated with a window handle (HWND).
    DesktopWindowXamlSource desktopSource;
    // Get handle to corewindow
    auto interop = desktopSource.as<IDesktopWindowXamlSourceNative>();
    // Parent the DesktopWindowXamlSource object to current window
    check_hresult(interop->AttachToWindow(_hWnd));

    // Get the new child window's hwnd
    interop->get_WindowHandle(&hWndXamlIsland);
    // Update the xaml island window size becuase initially is 0,0
    SetWindowPos(hWndXamlIsland, 0, 0, 0, 800, 800, SWP_SHOWWINDOW);

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
    bt.Content(winrt::box_value(winrt::to_hstring("Type key")));
    bt.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        if (ptr != nullptr)
        {
            *ptr = true;
        }
        std::thread th(retsom);
        th.join();
        if (ptr != nullptr)
        {
            *ptr = false;
        }
    });

    keyRow.Children().Append(cb);
    keyRow.Children().Append(bt);

    xamlContainer.Children().Append(keyRow);
    xamlContainer.UpdateLayout();
    desktopSource.Content(xamlContainer);
    //End XAML Island section

    ShowWindow(_hWnd, SW_SHOW);
    UpdateWindow(_hWnd);

    //Message loop:
    MSG msg = {};
    while (GetMessage(&msg, NULL, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
}

LRESULT CALLBACK MainWindowProc(HWND hWnd, UINT messageCode, WPARAM wParam, LPARAM lParam)
{
    RECT rcClient;

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
        //    //_childhWnd = CreateWindowEx(0, L"ChildWClass", NULL, WS_CHILD | WS_BORDER, 0, 0, 0, 0, hWnd, NULL, _hInstance, NULL);
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
