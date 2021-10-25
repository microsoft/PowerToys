#pragma once

#include <unknwn.h> // To enable support for non-WinRT interfaces, unknwn.h must be included before any C++/WinRT headers.
#include <winrt/Windows.System.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#pragma push_macro("GetCurrentTime")
#undef GetCurrentTime
#include <winrt/Windows.UI.Xaml.Hosting.h>
#pragma pop_macro("GetCurrentTime")
#include <winrt/Windows.UI.Xaml.Markup.h>
#include <windows.ui.xaml.hosting.desktopwindowxamlsource.h>
#include <windowsx.h>
#include <wil/resource.h>

class DesktopWindow
{
protected:
    int MessageLoop(HACCEL accelerators);
    HWND CreateDesktopWindowsXamlSource(DWORD extraStyles, const winrt::Windows::UI::Xaml::UIElement& content);
    void ClearXamlIslands();

    HWND WindowHandle() const
    {
        return m_window.get();
    }

    static void OnNCCreate(HWND window, LPARAM lparam) noexcept
    {
        auto cs = reinterpret_cast<CREATESTRUCT*>(lparam);
        auto that = static_cast<DesktopWindow*>(cs->lpCreateParams);
        that->m_window.reset(window); // take ownership of the window
        SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(that));
    }

private:
    winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource GetFocusedIsland();
    bool FilterMessage(const MSG* msg);
    void OnTakeFocusRequested(winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource const& sender, winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSourceTakeFocusRequestedEventArgs const& args);
    winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource GetNextFocusedIsland(const MSG* msg);
    bool NavigateFocus(MSG* msg);

    wil::unique_hwnd m_window;
    winrt::guid m_lastFocusRequestId;
    std::vector<winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource::TakeFocusRequested_revoker> m_takeFocusEventRevokers;
    std::vector<winrt::Windows::UI::Xaml::Hosting::DesktopWindowXamlSource> m_xamlSources;
};

template<typename T>
struct DesktopWindowT : public DesktopWindow
{
protected:
    using base_type = DesktopWindowT<T>;

    static LRESULT __stdcall WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
    {
        if (message == WM_NCCREATE)
        {
            OnNCCreate(window, lparam);
        }
        else if (message == WM_NCDESTROY)
        {
            SetWindowLongPtrW(window, GWLP_USERDATA, 0);
        }
        else if (auto that = reinterpret_cast<T*>(GetWindowLongPtrW(window, GWLP_USERDATA)))
        {
            return that->MessageHandler(message, wparam, lparam);
        }

        return DefWindowProcW(window, message, wparam, lparam);
    }

    LRESULT MessageHandler(UINT message, WPARAM wParam, LPARAM lParam) noexcept
    {
        switch (message)
        {
            HANDLE_MSG(WindowHandle(), WM_DESTROY, OnDestroy);
            HANDLE_MSG(WindowHandle(), WM_ACTIVATE, OnActivate);
            HANDLE_MSG(WindowHandle(), WM_SETFOCUS, OnSetFocus);
        }
        return DefWindowProcW(WindowHandle(), message, wParam, lParam);
    }

    void OnDestroy(HWND)
    {
        ClearXamlIslands();
        PostQuitMessage(0);
    }

private:
    void OnActivate(HWND, UINT state, HWND hwndActDeact, BOOL fMinimized)
    {
        if (state == WA_INACTIVE)
        {
            m_hwndLastFocus = GetFocus();
        }
    }

    void OnSetFocus(HWND, HWND hwndOldFocus)
    {
        if (m_hwndLastFocus)
        {
            SetFocus(m_hwndLastFocus);
        }
    }

    HWND m_hwndLastFocus = nullptr;
};
