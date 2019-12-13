#pragma once
#include "pch.h"
#include "common/dpi_aware.h"
#include "common/on_thread_executor.h"

interface __declspec(uuid("{50D3F0F5-736E-4186-BDF4-3D6BEE150C3B}")) INewToy : public IUnknown
{
    IFACEMETHOD_(void, Run)() = 0;
    IFACEMETHOD_(void, Destroy)() = 0;
};

//interface __declspec(uuid("{2CB37E8F-87E6-4AEC-B4B2-E0FDC873343C}")) INewToyCallback : public IUnknown
//{
//    IFACEMETHOD_(bool, InMoveSize)
//    () = 0;
//    IFACEMETHOD_(void, MoveSizeStart)
//    (HWND window, HMONITOR monitor, POINT const& ptScreen) = 0;
//    IFACEMETHOD_(void, MoveSizeUpdate)
//    (HMONITOR monitor, POINT const& ptScreen) = 0;
//    IFACEMETHOD_(void, MoveSizeEnd)
//    (HWND window, POINT const& ptScreen) = 0;
//    IFACEMETHOD_(void, VirtualDesktopChanged)
//    () = 0;
//    IFACEMETHOD_(void, WindowCreated)
//    (HWND window) = 0;
//    IFACEMETHOD_(bool, OnKeyDown)
//    (PKBDLLHOOKSTRUCT info) = 0;
//    IFACEMETHOD_(void, ToggleEditor)
//    () = 0;
//    IFACEMETHOD_(void, SettingsChanged)
//    () = 0;
//};


struct NewToyCOM : public winrt::implements<NewToyCOM, INewToy>
{
public:
    NewToyCOM(HINSTANCE hinstance) noexcept : m_hinstance(hinstance) {}

    // INewToy methods
    IFACEMETHODIMP_(void) Run() noexcept;
    IFACEMETHODIMP_(void) Destroy() noexcept;

    //IFACEMETHODIMP_(bool) OnKeyDown(PKBDLLHOOKSTRUCT info) noexcept;
protected:
    static LRESULT CALLBACK s_WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;

private:
    struct require_read_lock
    {
        template<typename T>
        require_read_lock(const std::shared_lock<T>& lock)
        {
            lock;
        }

        template<typename T>
        require_read_lock(const std::unique_lock<T>& lock)
        {
            lock;
        }
    };

    struct require_write_lock
    {
        template<typename T>
        require_write_lock(const std::unique_lock<T>& lock)
        {
            lock;
        }
    };

    const HINSTANCE m_hinstance{};

    mutable std::shared_mutex m_lock;
    HWND m_window{};
    HWND m_windowMoveSize{}; // The window that is being moved/sized
    

    OnThreadExecutor m_dpiUnawareThread;

    static UINT WM_PRIV_VDCHANGED; // Message to get back on to the UI thread when virtual desktop changes
    static UINT WM_PRIV_EDITOR; // Message to get back on to the UI thread when the editor exits

    // Did we terminate the editor or was it closed cleanly?
    enum class EditorExitKind : byte
    {
        Exit,
        Terminate
    };

    LRESULT WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;
};

//UINT NewToyCOM::WM_PRIV_VDCHANGED = RegisterWindowMessage(L"{128c2cb0-6bdf-493e-abbe-f8705e04aa95}");
//UINT NewToyCOM::WM_PRIV_EDITOR = RegisterWindowMessage(L"{87543824-7080-4e91-9d9c-0404642fc7b6}");

// IFancyZones
IFACEMETHODIMP_(void) NewToyCOM::Run() noexcept
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
    } }).wait();
}

IFACEMETHODIMP_(void) NewToyCOM::Destroy() noexcept
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

winrt::com_ptr<INewToy> MakeNewToy(HINSTANCE hinstance) noexcept
{
    return winrt::make_self<NewToyCOM>(hinstance);
}