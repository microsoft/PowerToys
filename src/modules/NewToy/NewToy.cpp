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
    m_window = CreateWindowExW(0, L"SuperNewToy", titleText, WS_OVERLAPPEDWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, CW_USEDEFAULT, nullptr, nullptr, m_hinstance, this);
    // If window creation fails, return
    if (!m_window)
        return;

    // Win + /
    // Note: Cannot overwrite existing Windows shortcuts
    RegisterHotKey(m_window, 1, m_settings->newToyShowHotkey.get_modifiers(), m_settings->newToyShowHotkey.get_code());
    RegisterHotKey(m_window, 2, m_settings->newToyEditHotkey.get_modifiers(), m_settings->newToyEditHotkey.get_code());
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
        DefWindowProc(window, message, wparam, lparam);
    }
    return thisRef ? thisRef->WndProc(window, message, wparam, lparam) : DefWindowProc(window, message, wparam, lparam);
}

LRESULT NewToyCOM::WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
    case WM_CLOSE: {
        // Don't destroy - hide instead
        isWindowShown = false;
        ShowWindow(window, SW_HIDE);
        return 0;
    }
    break;
    case WM_HOTKEY: {
        if (wparam == 1)
        {
            // Show the window if it is hidden
            if (!isWindowShown)
                ShowWindow(window, SW_SHOW);
            // Hide the window if it is shown
            else
                ShowWindow(window, SW_HIDE);
            // Toggle the state of isWindowShown
            isWindowShown = !isWindowShown;
            return 0;
        }
        else if (wparam == 2)
        {
            windowText = L"Hello World, check out this awesome power toy!";
            titleText = L"Awesome Toy";
            // Change title
            SetWindowText(window, titleText);
            // If window is shown, trigger a repaint
            RedrawWindow(window, nullptr, nullptr, RDW_INVALIDATE);
            return 0;
        }
    }
    break;
    case WM_PAINT: {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(window, &ps);
        TextOut(hdc,
                // Location of the text
                10,
                10,
                // Text to print
                windowText,
                // Size of the text, my function gets this for us
                lstrlen(windowText));
        EndPaint(window, &ps);
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
    //int milli_seconds = 1000 * 2;

    //// Storing start time
    //clock_t start_time = clock();

    //// looping till required time is not acheived
    //while (clock() < start_time + milli_seconds)
    //    ;
    bool const win = GetAsyncKeyState(VK_LWIN) & 0x8000;

    // Note Win+L cannot be overriden. Requires WinLock to be disabled.
    // Trigger on Win+Z
    if (win && (info->vkCode == 0x5A))
    {
        if (m_window)
        {
            // Show the window if it is hidden
            if (!isWindowShown)
                ShowWindow(m_window, SW_SHOW);
            // Hide the window if it is shown
            else
                ShowWindow(m_window, SW_HIDE);
            // Toggle the state of isWindowShown
            isWindowShown = !isWindowShown;
            // Return true to swallow the keyboard event
            return true;
        }
    }
    return false;
}

IFACEMETHODIMP_(void)
NewToyCOM::HotkeyChanged() noexcept
{
    // Update the hotkey
    UnregisterHotKey(m_window, 1);
    RegisterHotKey(m_window, 1, m_settings->newToyShowHotkey.get_modifiers(), m_settings->newToyShowHotkey.get_code());
    UnregisterHotKey(m_window, 2);
    RegisterHotKey(m_window, 2, m_settings->newToyEditHotkey.get_modifiers(), m_settings->newToyEditHotkey.get_code());
}

winrt::com_ptr<INewToy> MakeNewToy(HINSTANCE hinstance, ModuleSettings* settings) noexcept
{
    return winrt::make_self<NewToyCOM>(hinstance, settings);
}