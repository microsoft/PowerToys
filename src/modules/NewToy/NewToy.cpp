#include "pch.h"
#include "NewToy.h"

IFACEMETHODIMP_(void)
NewToyCOM::Run() noexcept
{
    std::unique_lock writeLock(m_lock);

    // Registers the window class
    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = s_WndProc;
    wcex.hInstance = m_hinstance;
    wcex.lpszClassName = L"SuperNewToy";
    RegisterClassExW(&wcex);

    // Creates the window
    m_window = CreateWindowExW(0, L"SuperNewToy", L"New Toy", WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, nullptr, nullptr, m_hinstance, this);
    // If window creation fails, return
    if (!m_window)
        return;

    // Win + /
    // Note: Cannot overwrite existing Windows shortcuts
    RegisterHotKey(m_window, 1, MOD_WIN | MOD_NOREPEAT, VK_OEM_2);

    m_dpiUnawareThread.submit(OnThreadExecutor::task_t{ [] {
                          SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_UNAWARE);
                          SetThreadDpiHostingBehavior(DPI_HOSTING_BEHAVIOR_MIXED);
                      } })
        .wait();
}

IFACEMETHODIMP_(void)
NewToyCOM::Destroy() noexcept
{
    // Locks the window
    std::unique_lock writeLock(m_lock);
    if (m_window)
    {
        DestroyWindow(m_window);
        m_window = nullptr;
    }
}

LRESULT CALLBACK NewToyCOM::s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    auto thisRef = reinterpret_cast<NewToyCOM*>(GetWindowLongPtr(window, GWLP_USERDATA));
    if (!thisRef && (message == WM_CREATE))
    {
        const auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
        thisRef = reinterpret_cast<NewToyCOM*>(createStruct->lpCreateParams);
        SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
    }
    return thisRef ? thisRef->WndProc(window, message, wparam, lparam) : DefWindowProc(window, message, wparam, lparam);
}

LRESULT NewToyCOM::WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
    case WM_CLOSE: {
        // Don't destroy - hide instead
        ShowWindow(window, SW_HIDE);
        return 0;
    }
    break;
    case WM_HOTKEY: {
        if (wparam == 1)
        {
            ShowWindow(window, SW_SHOW);
            return 0;
        }
    }
    break;
    default: {
        return DefWindowProc(window, message, wparam, lparam);
    }
    break;
    }
    return 0;
}

IFACEMETHODIMP_(bool)
NewToyCOM::OnKeyDown(PKBDLLHOOKSTRUCT info) noexcept
{
    // Return true to swallow the keyboard event
    bool const win = GetAsyncKeyState(VK_LWIN) & 0x8000;
    // Note Win+L cannot be overriden. Requires WinLock to be disabled.
    // Trigger on Win+Z
    if (win && (info->vkCode == 0x5A))
    {
        if (m_window)
        {
            ShowWindow(m_window, SW_SHOW);
            return true;
        }
    }
    return false;
}

winrt::com_ptr<INewToy> MakeNewToy(HINSTANCE hinstance) noexcept
{
    return winrt::make_self<NewToyCOM>(hinstance);
}