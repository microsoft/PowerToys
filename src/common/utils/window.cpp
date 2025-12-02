#include "pch.h"
#include "window.h"

int run_message_loop(bool until_idle,
                     std::optional<uint32_t> timeout_ms,
                     std::unordered_map<DWORD, std::function<void()>> wm_app_msg_callbacks)
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
        {
            it->second();
        }
    }

    if (timeout_ms.has_value())
    {
        KillTimer(nullptr, timerId);
    }

    return static_cast<int>(msg.wParam);
}

bool is_system_window(HWND hwnd, const char* class_name)
{
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
