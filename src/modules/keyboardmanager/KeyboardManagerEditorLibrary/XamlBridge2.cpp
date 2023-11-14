#include "pch.h"
#include "XamlBridge2.h"
#include <CoreWindow.h> // ICoreWindowInterop

#include <winrt/Microsoft.Toolkit.Win32.UI.XamlHost.h>
#include <winrt/Microsoft.UI.Xaml.XamlTypeInfo.h>

namespace wac = Windows::ApplicationModel::Core;

// Stubbed implementation for frameworkView.Initialize()
struct XamlBridgeCoreAppViewImpl : implements<XamlBridgeCoreAppViewImpl, wac::ICoreApplicationView>
{
    auto CoreWindow() { return Core::CoreWindow::GetForCurrentThread(); }
    auto Activated(Windows::Foundation::TypedEventHandler<wac::CoreApplicationView, Windows::ApplicationModel::Activation::IActivatedEventArgs> const&) { return event_token(); }
    auto Activated(event_token const&) {}
    auto IsMain() { return true; }
    auto IsHosted() { return false; }
};

// Function to run the message loop for the xaml window
void XamlBridge2::MessageLoop()
{
    Logger::trace("XamlBridge2::MessageLoop()");
    frameworkView.Run();
    Logger::trace("XamlBridge2::MessageLoop() stopped");
}

// Function to initialize the xaml bridge
HWND XamlBridge2::InitBridge()
{
    Logger::trace("XamlBridge2::InitBridge()");
    HRESULT hr = S_OK;
    winrt::init_apartment(apartment_type::single_threaded);

    auto windowsUIHandle = LoadLibrary(TEXT("Windows.UI.dll"));
    auto pfnPrivateCreateCoreWindow = reinterpret_cast<fnPrivateCreateCoreWindow>(GetProcAddress(windowsUIHandle, MAKEINTRESOURCEA(1500)));

    // Create the core window to host the XAML content
    void* pCoreWindow;
    hr = pfnPrivateCreateCoreWindow(IMMERSIVE_HOSTED, L"", 0, 0, 0, 0, 0, parentWindow, winrt::guid_of<Core::ICoreWindow>(), &pCoreWindow);
    winrt::check_hresult(hr);
    coreWindow = Core::CoreWindow(pCoreWindow, winrt::take_ownership_from_abi);

    // Prep for the WinUI resources
    auto app = Microsoft::Toolkit::Win32::UI::XamlHost::XamlApplication({ Microsoft::UI::Xaml::XamlTypeInfo::XamlControlsXamlMetaDataProvider() });

    // Initialize the XAML framework
    frameworkView.Initialize(*reinterpret_cast<wac::CoreApplicationView*>(&make<XamlBridgeCoreAppViewImpl>()));
    frameworkView.SetWindow(coreWindow);

    // Add the WinUI resources
    app.Resources().MergedDictionaries().Append(muxc::XamlControlsResources());

    auto coreWindowInterop = coreWindow.as<ICoreWindowInterop>();
    hr = coreWindowInterop->get_WindowHandle(&coreWindowHwnd);
    winrt::check_hresult(hr);

    SetParent(coreWindowHwnd, parentWindow);
    SetWindowLong(coreWindowHwnd, GWL_STYLE, WS_CHILD | WS_VISIBLE);

    return coreWindowHwnd;
}

// Message Handler function for Xaml windows
LRESULT XamlBridge2::MessageHandler(UINT const message, WPARAM const wParam, LPARAM const lParam) noexcept
{
    switch (message)
    {
    case WM_ACTIVATE:
    case WM_MOVE:
        SendMessage(coreWindowHwnd, message, wParam, lParam);
        break;
    }

    return DefWindowProc(parentWindow, message, wParam, lParam);
}
