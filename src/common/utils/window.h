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
