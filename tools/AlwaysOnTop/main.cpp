#include "pch.h"

#include "AlwaysOnTop.h"

inline int run_message_loop(const bool until_idle = false, const std::optional<uint32_t> timeout_seconds = {})
{
    MSG msg{};
    bool stop = false;
    UINT_PTR timerId = 0;
    if (timeout_seconds.has_value())
    {
        timerId = SetTimer(nullptr, 0, *timeout_seconds * 1000, nullptr);
    }

    while (!stop && GetMessageW(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
        stop = until_idle && !PeekMessageW(&msg, nullptr, 0, 0, PM_NOREMOVE);
        stop = stop || (msg.message == WM_TIMER && msg.wParam == timerId);
    }
    if (timeout_seconds.has_value())
    {
        KillTimer(nullptr, timerId);
    }
    return static_cast<int>(msg.wParam);
}

int WINAPI wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ PWSTR lpCmdLine, _In_ int nCmdShow)
{
    winrt::init_apartment();

    AlwaysOnTop app;

    run_message_loop();

    return 0;
}
