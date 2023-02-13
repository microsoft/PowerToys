#include "pch.h"
#include "XamlBridge.h"
#include <windowsx.h>
#include <string>

bool XamlBridge::FilterMessage(const MSG* msg)
{
    // When multiple child windows are present it is needed to pre dispatch messages to all
    // DesktopWindowXamlSource instances so keyboard accelerators and
    // keyboard focus work correctly.
    BOOL xamlSourceProcessedMessage = FALSE;
    {
        for (auto xamlSource : m_xamlSources)
        {
            auto xamlSourceNative2 = xamlSource.as<IDesktopWindowXamlSourceNative2>();
            const auto hr = xamlSourceNative2->PreTranslateMessage(msg, &xamlSourceProcessedMessage);
            winrt::check_hresult(hr);
            if (xamlSourceProcessedMessage)
            {
                break;
            }
        }
    }

    return !!xamlSourceProcessedMessage;
}

const auto static invalidReason = static_cast<winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason>(-1);

winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason GetReasonFromKey(WPARAM key)
{
    auto reason = invalidReason;
    if (key == VK_TAB)
    {
        byte keyboardState[256] = {};
        WINRT_VERIFY(::GetKeyboardState(keyboardState));
        reason = (keyboardState[VK_SHIFT] & 0x80) ?
                     winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Last :
                     winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::First;
    }
    else if (key == VK_LEFT)
    {
        reason = winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Left;
    }
    else if (key == VK_RIGHT)
    {
        reason = winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Right;
    }
    else if (key == VK_UP)
    {
        reason = winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Up;
    }
    else if (key == VK_DOWN)
    {
        reason = winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Down;
    }
    return reason;
}

// Function to return the next xaml island in focus
winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource XamlBridge::GetNextFocusedIsland(MSG* msg)
{
    if (msg->message == WM_KEYDOWN)
    {
        const auto key = msg->wParam;
        auto reason = GetReasonFromKey(key);
        if (reason != invalidReason)
        {
            const BOOL previous =
                (reason == winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::First ||
                 reason == winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Down ||
                 reason == winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Right) ?
                    false :
                    true;

            const auto currentFocusedWindow = ::GetFocus();
            const auto nextElement = ::GetNextDlgTabItem(parentWindow, currentFocusedWindow, previous);
            for (auto xamlSource : m_xamlSources)
            {
                const auto nativeIsland = xamlSource.as<IDesktopWindowXamlSourceNative>();
                HWND islandWnd = nullptr;
                winrt::check_hresult(nativeIsland->get_WindowHandle(&islandWnd));
                if (nextElement == islandWnd)
                {
                    return xamlSource;
                }
            }
        }
    }

    return nullptr;
}

// Function to return the xaml island currently in focus
winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource XamlBridge::GetFocusedIsland()
{
    for (auto xamlSource : m_xamlSources)
    {
        if (xamlSource.HasFocus())
        {
            return xamlSource;
        }
    }
    return nullptr;
}

// Function to handle focus navigation
bool XamlBridge::NavigateFocus(MSG* msg)
{
    if (const auto nextFocusedIsland = GetNextFocusedIsland(msg))
    {
        const auto previousFocusedWindow = ::GetFocus();
        RECT rect = {};
        WINRT_VERIFY(::GetWindowRect(previousFocusedWindow, &rect));
        const auto nativeIsland = nextFocusedIsland.as<IDesktopWindowXamlSourceNative>();
        HWND islandWnd = nullptr;
        winrt::check_hresult(nativeIsland->get_WindowHandle(&islandWnd));
        POINT pt = { rect.left, rect.top };
        SIZE size = { rect.right - rect.left, rect.bottom - rect.top };
        ::ScreenToClient(islandWnd, &pt);
        const auto hintRect = winrt::Windows::Foundation::Rect({ static_cast<float>(pt.x), static_cast<float>(pt.y), static_cast<float>(size.cx), static_cast<float>(size.cy) });
        const auto reason = GetReasonFromKey(msg->wParam);
        const auto request = winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationRequest(reason, hintRect);
        lastFocusRequestId = request.CorrelationId();
        const auto result = nextFocusedIsland.NavigateFocus(request);
        return result.WasFocusMoved();
    }
    else
    {
        const bool islandIsFocused = GetFocusedIsland() != nullptr;
        byte keyboardState[256] = {};
        WINRT_VERIFY(::GetKeyboardState(keyboardState));
        const bool isMenuModifier = keyboardState[VK_MENU] & 0x80;
        if (islandIsFocused && !isMenuModifier)
        {
            return false;
        }
        const bool isDialogMessage = !!IsDialogMessage(parentWindow, msg);
        return isDialogMessage;
    }
}

// Function to run the message loop for the xaml island window
WPARAM XamlBridge::MessageLoop()
{
    MSG msg = {};
    HRESULT hr = S_OK;
    Logger::trace("XamlBridge::MessageLoop()");
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        const bool xamlSourceProcessedMessage = FilterMessage(&msg);
        if (!xamlSourceProcessedMessage)
        {
            if (!NavigateFocus(&msg))
            {
                TranslateMessage(&msg);
                DispatchMessage(&msg);
            }
        }
    }

    Logger::trace("XamlBridge::MessageLoop() stopped");
    return msg.wParam;
}

static const WPARAM invalidKey = 0xFFFFFFFFFFFFFFFF;

constexpr WPARAM GetKeyFromReason(winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason reason)
{
    auto key = invalidKey;
    if (reason == winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Last || reason == winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::First)
    {
        key = VK_TAB;
    }
    else if (reason == winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Left)
    {
        key = VK_LEFT;
    }
    else if (reason == winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Right)
    {
        key = VK_RIGHT;
    }
    else if (reason == winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Up)
    {
        key = VK_UP;
    }
    else if (reason == winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Down)
    {
        key = VK_DOWN;
    }
    return key;
}

// Event triggered when focus is requested
void XamlBridge::OnTakeFocusRequested(winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource const& sender, winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSourceTakeFocusRequestedEventArgs const& args)
{
    Logger::trace("XamlBridge::OnTakeFocusRequested()");
    if (args.Request().CorrelationId() != lastFocusRequestId)
    {
        const auto reason = args.Request().Reason();
        const BOOL previous =
            (reason == winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::First ||
             reason == winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Down ||
             reason == winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Right) ?
                false :
                true;

        const auto nativeXamlSource = sender.as<IDesktopWindowXamlSourceNative>();
        HWND senderHwnd = nullptr;
        winrt::check_hresult(nativeXamlSource->get_WindowHandle(&senderHwnd));

        MSG msg = {};
        msg.hwnd = senderHwnd;
        msg.message = WM_KEYDOWN;
        msg.wParam = GetKeyFromReason(reason);
        if (!NavigateFocus(&msg))
        {
            const auto nextElement = ::GetNextDlgTabItem(parentWindow, senderHwnd, previous);
            ::SetFocus(nextElement);
        }
    }
    else
    {
        const auto request = winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationRequest(winrt::Windows::UI::Xaml::Hosting::XamlSourceFocusNavigationReason::Restore);
        lastFocusRequestId = request.CorrelationId();
        sender.NavigateFocus(request);
    }
}

// Function to initialise the xaml source object
HWND XamlBridge::InitDesktopWindowsXamlSource(winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource desktopSource)
{
    Logger::trace("XamlBridge::InitDesktopWindowsXamlSource()");
    HRESULT hr = S_OK;
    winrt::init_apartment(apartment_type::single_threaded);
    winxamlmanager = WindowsXamlManager::InitializeForCurrentThread();

    auto interop = desktopSource.as<IDesktopWindowXamlSourceNative>();
    // Parent the DesktopWindowXamlSource object to current window
    hr = interop->AttachToWindow(parentWindow);
    winrt::check_hresult(hr);

    // Get the new child window's hwnd
    HWND hWndXamlIsland = nullptr;
    hr = interop->get_WindowHandle(&hWndXamlIsland);
    winrt::check_hresult(hr);

    m_takeFocusEventRevokers.push_back(desktopSource.TakeFocusRequested(winrt::auto_revoke, { this, &XamlBridge::OnTakeFocusRequested }));
    m_xamlSources.push_back(desktopSource);

    return hWndXamlIsland;
}

// Function to close and delete all the xaml source objects
void XamlBridge::ClearXamlIslands()
{
    Logger::trace("XamlBridge::ClearXamlIslands() {} focus event revokers", m_takeFocusEventRevokers.size());
    for (auto& takeFocusRevoker : m_takeFocusEventRevokers)
    {
        takeFocusRevoker.revoke();
    }
    m_takeFocusEventRevokers.clear();

    for (auto xamlSource : m_xamlSources)
    {
        xamlSource.Close();
    }
    m_xamlSources.clear();

    winxamlmanager.Close();
}

// Function invoked when the window is destroyed
void XamlBridge::OnDestroy(HWND)
{
    Logger::trace("XamlBridge::OnDestroy()");
    PostQuitMessage(0);
}

// Function invoked when the window is activated
void XamlBridge::OnActivate(HWND, UINT state, HWND hwndActDeact, BOOL fMinimized)
{
    if (state == WA_INACTIVE)
    {
        Logger::trace("XamlBridge::OnActivate()");
        m_hwndLastFocus = GetFocus();
    }
}

// Function invoked when the window is set to focus
void XamlBridge::OnSetFocus(HWND, HWND hwndOldFocus)
{
    if (m_hwndLastFocus)
    {
        Logger::trace("XamlBridge::OnSetFocus()");
        SetFocus(m_hwndLastFocus);
    }
}


std::wstring getMessageString(const UINT message)
{
    switch (message)
    {
    case WM_NCDESTROY:
        return L"WM_NCDESTROY";
    case WM_ACTIVATE:
        return L"WM_ACTIVATE";
    case WM_SETFOCUS:
        return L"WM_SETFOCUS";
    default:
        return L"";
    }
}

// Message Handler function for Xaml Island windows
LRESULT XamlBridge::MessageHandler(UINT const message, WPARAM const wParam, LPARAM const lParam) noexcept
{
    auto msg = getMessageString(message);
    if (msg != L"")
    {
        Logger::trace(L"XamlBridge::MessageHandler() message: {}", msg);
    }
    
    switch (message)
    {
        HANDLE_MSG(parentWindow, WM_NCDESTROY, OnDestroy);
        HANDLE_MSG(parentWindow, WM_ACTIVATE, OnActivate);
        HANDLE_MSG(parentWindow, WM_SETFOCUS, OnSetFocus);
    }

    return DefWindowProc(parentWindow, message, wParam, lParam);
}
