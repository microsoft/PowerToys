#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <dwmapi.h>

#include <array>
#include <optional>
#include <functional>
#include <unordered_map>

// Initializes and runs windows message loop
inline int run_message_loop(const bool until_idle = false,
                            const std::optional<uint32_t> timeout_ms = {},
                            std::unordered_map<DWORD, std::function<void()>> wm_app_msg_callbacks = {})
{
    MSG msg{};
    bool stop = false;
    UINT_PTR timerId = 0;
    if (timeout_ms.has_value())
    {
        timerId = SetTimer(nullptr, 0, *timeout_ms, nullptr);
    }

    while (!stop && (until_idle ? PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE) : GetMessageW(&msg, nullptr, 0, 0)))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
        stop = until_idle && !PeekMessageW(&msg, nullptr, 0, 0, PM_NOREMOVE);
        stop = stop || (msg.message == WM_TIMER && msg.wParam == timerId);

        if (auto it = wm_app_msg_callbacks.find(msg.message); it != end(wm_app_msg_callbacks))
            it->second();
    }

    if (timeout_ms.has_value())
    {
        KillTimer(nullptr, timerId);
    }

    return static_cast<int>(msg.wParam);
}

// Handles WM_QUERYENDSESSION / WM_ENDSESSION for processes that have no
// unsaved user state of their own (the PowerToys runner and most modules).
//
// WndProcs should call this at the top of their dispatch and return early
// when it reports the message was handled. Without this, the OS quiesce
// handshake on shutdown / sign-out / restart / suspend has nothing to wait
// on except the full timeout, producing APPLICATION_HANG_QUIESCE failure
// buckets even though the process is idle in GetMessage.
//
// On WM_QUERYENDSESSION we always allow the session to end (returns TRUE).
// On WM_ENDSESSION with wparam == TRUE we DestroyWindow(window), which then
// drives the existing WM_DESTROY -> PostQuitMessage(0) path and lets
// run_message_loop unwind cleanly. wparam == FALSE means the session was
// cancelled by another application's WM_QUERYENDSESSION response, in which
// case we must not tear down.
//
// Returns true if the message was an end-session message and out_result has
// been set; the caller should return out_result without further dispatch.
inline bool handle_session_end_message(HWND window, UINT message, WPARAM wparam, LRESULT& out_result)
{
    switch (message)
    {
    case WM_QUERYENDSESSION:
        out_result = TRUE;
        return true;
    case WM_ENDSESSION:
        if (wparam)
        {
            DestroyWindow(window);
        }
        out_result = 0;
        return true;
    }
    return false;
}

// Check if window is part of the shell or the taskbar.
inline bool is_system_window(HWND hwnd, const char* class_name)
{
    // We compare the HWND against HWND of the desktop and shell windows,
    // we also filter out some window class names know to belong to the taskbar.
    constexpr std::array system_classes = { "SysListView32", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman" };
    const std::array system_hwnds = { GetDesktopWindow(), GetShellWindow() };
    for (auto system_hwnd : system_hwnds)
    {
        if (hwnd == system_hwnd)
        {
            return true;
        }
    }
    for (const auto system_class : system_classes)
    {
        if (!strcmp(system_class, class_name))
        {
            return true;
        }
    }
    return false;
}

template<typename T>
inline T GetWindowCreateParam(LPARAM lparam)
{
    static_assert(sizeof(T) <= sizeof(void*));
    T data{ static_cast<T>(reinterpret_cast<CREATESTRUCT*>(lparam)->lpCreateParams) };
    return data;
}

template<typename T>
inline void StoreWindowParam(HWND window, T data)
{
    static_assert(sizeof(T) <= sizeof(void*));
    SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(data));
}

template<typename T>
inline T GetWindowParam(HWND window)
{
    return reinterpret_cast<T>(GetWindowLongPtrW(window, GWLP_USERDATA));
}
