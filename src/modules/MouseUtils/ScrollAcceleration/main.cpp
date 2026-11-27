#include "pch.h"

#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/ProcessWaiter.h>
#include <common/interop/shared_constants.h>

#include "resource.h"
#include "AccelerationCalculator.h"
#include "MomentumCalculator.h"
#include "SmoothingCalculator.h"

namespace
{
    constexpr wchar_t CLASS_NAME[] = L"PowerToys.ScrollAcceleration.MessageWindow";
    constexpr wchar_t APP_TITLE[] = L"PowerToys Scroll Acceleration";
    constexpr wchar_t INSTANCE_MUTEX_NAME[] = L"Local\\PowerToys-ScrollAcceleration-InstanceMutex-5d1b8e47-2a93-4c06-b7f1-8e0c3a96d452";

    // Marker placed in dwExtraInfo of the events we re-inject, so the hook ignores them.
    constexpr ULONG_PTR INJECTED_MARKER = 0x53435241; // 'SCRA'

    constexpr UINT WM_APPLY_SETTINGS = WM_APP + 1;

    struct SettingsSnapshot
    {
        // Defaults used until settings.json is read; keep in sync with ScrollAccelerationProperties.cs.
        ScrollAcceleration::AccelerationSettings acceleration{ 100, 1.0, 8.0, 0.15 };
        bool applyToHorizontalWheel = false;
        bool ignoreWithCtrl = true;
        // Lower-case executable names (for example "chrome.exe") for which the foreground app is left untouched.
        std::vector<std::wstring> excludedApps;
        ScrollAcceleration::MomentumSettings momentum;
        ScrollAcceleration::SmoothingSettings smoothing;
    };

    HHOOK g_hhkMouse = nullptr;
    HWND g_hMsgWnd = nullptr;
    HANDLE g_hReloadSettingsEvent = nullptr;
    HANDLE g_hExitEvent = nullptr;
    HANDLE g_hToggleEvent = nullptr;
    HANDLE g_hExcludeAppEvent = nullptr;

    // Set by the activation hotkey: the hook passes every wheel event through untouched while true.
    std::atomic<bool> g_paused{ false };
    constexpr UINT WM_TOGGLE_PAUSE = WM_APP + 4;
    constexpr UINT WM_TOGGLE_EXCLUDE_APP = WM_APP + 5;
    HANDLE g_hStopSettingsEvent = nullptr;
    HANDLE g_hInstanceMutex = nullptr;
    std::thread g_settingsThread;

    // Only touched from the main (hook) thread after startup.
    SettingsSnapshot g_settings;
    ScrollAcceleration::AccelerationCalculator g_vertical;
    ScrollAcceleration::AccelerationCalculator g_horizontal;

    // Shared between the hook thread (OnTick/Cancel) and the injector thread (Step), guarded by g_momentumMutex.
    std::mutex g_momentumMutex;
    ScrollAcceleration::MomentumCalculator g_verticalMomentum;
    ScrollAcceleration::MomentumCalculator g_horizontalMomentum;
    ScrollAcceleration::SmoothingCalculator g_verticalSmoothing;
    ScrollAcceleration::SmoothingCalculator g_horizontalSmoothing;

    std::mutex g_pendingMutex;
    SettingsSnapshot g_pendingSettings;

    std::vector<std::wstring> ParseExcludedApps(const std::wstring& text)
    {
        std::vector<std::wstring> result;
        std::wstring current;
        auto flush = [&]() {
            const auto first = current.find_first_not_of(L" \t");
            if (first != std::wstring::npos)
            {
                const auto last = current.find_last_not_of(L" \t");
                std::wstring name = current.substr(first, last - first + 1);
                std::transform(name.begin(), name.end(), name.begin(), [](wchar_t c) { return static_cast<wchar_t>(towlower(c)); });
                result.push_back(std::move(name));
            }
            current.clear();
        };

        for (const wchar_t c : text)
        {
            if (c == L'\n' || c == L'\r' || c == L';')
            {
                flush();
            }
            else
            {
                current.push_back(c);
            }
        }
        flush();
        return result;
    }

    // Lower-case file name of the executable that owns the window, or empty if it cannot be determined.
    std::wstring GetExecutableName(HWND hwnd)
    {
        DWORD pid = 0;
        GetWindowThreadProcessId(hwnd, &pid);
        if (pid == 0)
        {
            return {};
        }

        HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
        if (!process)
        {
            return {};
        }

        wchar_t path[MAX_PATH * 2];
        DWORD length = ARRAYSIZE(path);
        std::wstring name;
        if (QueryFullProcessImageNameW(process, 0, path, &length))
        {
            name.assign(path, length);
            const auto slash = name.find_last_of(L"\\/");
            if (slash != std::wstring::npos)
            {
                name.erase(0, slash + 1);
            }
        }
        CloseHandle(process);

        std::transform(name.begin(), name.end(), name.begin(), [](wchar_t c) { return static_cast<wchar_t>(towlower(c)); });
        return name;
    }

    // Foreground window the exclusion check was last computed for (hook thread only).
    HWND g_cachedForeground = nullptr;
    bool g_cachedExcluded = false;

    bool IsForegroundAppExcluded()
    {
        if (g_settings.excludedApps.empty())
        {
            return false;
        }

        const HWND foreground = GetForegroundWindow();
        if (!foreground)
        {
            return false;
        }

        if (foreground != g_cachedForeground)
        {
            const std::wstring name = GetExecutableName(foreground);
            g_cachedExcluded = !name.empty() && std::find(g_settings.excludedApps.begin(), g_settings.excludedApps.end(), name) != g_settings.excludedApps.end();
            g_cachedForeground = foreground;
        }

        return g_cachedExcluded;
    }

    bool TryLoadSettings(const SettingsSnapshot& current, SettingsSnapshot& updated)
    {
        try
        {
            updated = current;
            auto values = PowerToysSettings::PowerToyValues::load_from_settings_file(L"ScrollAcceleration");

            if (auto v = values.get_int_value(L"time_threshold_ms"))
            {
                updated.acceleration.timeThresholdMs = static_cast<uint32_t>((std::clamp)(*v, 10, 1000));
            }

            if (auto v = values.get_int_value(L"acceleration_percent"))
            {
                updated.acceleration.acceleration = (std::clamp)(*v, 0, 500) / 100.0;
            }

            if (auto v = values.get_int_value(L"slow_scroll_percent"))
            {
                updated.acceleration.baseMultiplier = (std::clamp)(*v, 5, 100) / 100.0;
            }

            if (auto v = values.get_int_value(L"max_multiplier"))
            {
                updated.acceleration.maxMultiplier = static_cast<double>((std::clamp)(*v, 1, 50));
            }

            if (auto v = values.get_bool_value(L"apply_to_horizontal_wheel"))
            {
                updated.applyToHorizontalWheel = *v;
            }

            if (auto v = values.get_bool_value(L"ignore_with_ctrl"))
            {
                updated.ignoreWithCtrl = *v;
            }

            if (auto v = values.get_string_value(L"excluded_apps"))
            {
                updated.excludedApps = ParseExcludedApps(*v);
            }
            else
            {
                updated.excludedApps.clear();
            }

            if (auto v = values.get_bool_value(L"momentum_enabled"))
            {
                updated.momentum.enabled = *v;
            }

            if (auto v = values.get_int_value(L"momentum_duration_ms"))
            {
                updated.momentum.durationMs = static_cast<uint32_t>((std::clamp)(*v, 100, 3000));
            }

            updated.momentum.startDelayMs = updated.acceleration.timeThresholdMs;

            // "Smooth scroll" in the UI: one switch turns on both the glide of every tick and the inertia.
            updated.smoothing.enabled = updated.momentum.enabled;

            return true;
        }
        catch (...)
        {
            return false;
        }
    }

    void ApplySettings(const SettingsSnapshot& settings)
    {
        g_settings = settings;
        g_cachedForeground = nullptr;
        g_vertical.SetSettings(settings.acceleration);
        g_horizontal.SetSettings(settings.acceleration);

        std::scoped_lock lock(g_momentumMutex);
        g_verticalMomentum.SetSettings(settings.momentum);
        g_horizontalMomentum.SetSettings(settings.momentum);
        g_verticalSmoothing.SetSettings(settings.smoothing);
        g_horizontalSmoothing.SetSettings(settings.smoothing);
    }

    void SettingsWatcherThread(SettingsSnapshot current)
    {
        HANDLE events[] = { g_hReloadSettingsEvent, g_hExitEvent, g_hStopSettingsEvent, g_hToggleEvent, g_hExcludeAppEvent };

        for (;;)
        {
            const DWORD wait = WaitForMultipleObjects(ARRAYSIZE(events), events, FALSE, INFINITE);
            if (wait == WAIT_OBJECT_0)
            {
                SettingsSnapshot updated;
                if (TryLoadSettings(current, updated))
                {
                    current = updated;
                    {
                        std::scoped_lock lock(g_pendingMutex);
                        g_pendingSettings = updated;
                    }
                    PostMessageW(g_hMsgWnd, WM_APPLY_SETTINGS, 0, 0);
                }
            }
            else if (wait == WAIT_OBJECT_0 + 1)
            {
                PostMessageW(g_hMsgWnd, WM_CLOSE, 0, 0);
                break;
            }
            else if (wait == WAIT_OBJECT_0 + 3)
            {
                PostMessageW(g_hMsgWnd, WM_TOGGLE_PAUSE, 0, 0);
            }
            else if (wait == WAIT_OBJECT_0 + 4)
            {
                PostMessageW(g_hMsgWnd, WM_TOGGLE_EXCLUDE_APP, 0, 0);
            }
            else
            {
                break;
            }
        }
    }

    void InjectWheel(DWORD flags, int delta)
    {
        INPUT input{};
        input.type = INPUT_MOUSE;
        input.mi.mouseData = static_cast<DWORD>(delta);
        input.mi.dwFlags = flags;
        input.mi.dwExtraInfo = INJECTED_MARKER;
        SendInput(1, &input, sizeof(INPUT));
    }

    // SendInput must NOT be called from inside the hook callback: the injected event has to pass through
    // this thread's own hook, which cannot run while the callback is blocked, so every call would stall
    // until the hook timeout. A dedicated thread performs the injection instead; the hook swallows the
    // original tick and hands the scaled delta over via a thread message (FIFO, so order is preserved).
    constexpr UINT WM_INJECT_WHEEL = WM_APP + 2;

    std::thread g_injectorThread;
    std::atomic<DWORD> g_injectorThreadId{ 0 };
    std::atomic<int> g_pendingInjections{ 0 };

    // Posted by the hook after a wheel tick so the injector thread starts its smooth scroll timer.
    constexpr UINT WM_MOMENTUM_WAKE = WM_APP + 3;
    constexpr UINT MOMENTUM_INTERVAL_MS = 16;

    void InjectorThreadMain(HANDLE ready)
    {
        UINT_PTR momentumTimer = 0;
        MSG msg;
        PeekMessageW(&msg, nullptr, WM_USER, WM_USER, PM_NOREMOVE); // force creation of the message queue
        g_injectorThreadId = GetCurrentThreadId();
        SetEvent(ready);

        while (GetMessageW(&msg, nullptr, 0, 0) > 0)
        {
            if (msg.message == WM_INJECT_WHEEL)
            {
                InjectWheel(static_cast<DWORD>(msg.wParam), static_cast<int>(msg.lParam));
                --g_pendingInjections;
            }
            else if (msg.message == WM_MOMENTUM_WAKE)
            {
                // With a null window and a non-zero id, SetTimer replaces the existing timer instead of adding one.
                momentumTimer = SetTimer(nullptr, momentumTimer, MOMENTUM_INTERVAL_MS, nullptr);
            }
            else if (msg.message == WM_TIMER && momentumTimer != 0 && msg.wParam == momentumTimer)
            {
                const uint32_t now = static_cast<uint32_t>(GetTickCount());
                ScrollAcceleration::MomentumCalculator::StepResult vertical;
                ScrollAcceleration::MomentumCalculator::StepResult horizontal;
                ScrollAcceleration::SmoothingCalculator::StepResult verticalSmooth;
                ScrollAcceleration::SmoothingCalculator::StepResult horizontalSmooth;
                {
                    std::scoped_lock lock(g_momentumMutex);
                    vertical = g_verticalMomentum.Step(now);
                    horizontal = g_horizontalMomentum.Step(now);
                    verticalSmooth = g_verticalSmoothing.Step(now);
                    horizontalSmooth = g_horizontalSmoothing.Step(now);
                }

                // One combined wheel event per axis and frame: the glide of the current tick plus any inertia.
                const int verticalDelta = (std::clamp)(vertical.delta + verticalSmooth.delta, -32767, 32767);
                const int horizontalDelta = (std::clamp)(horizontal.delta + horizontalSmooth.delta, -32767, 32767);
                if (verticalDelta != 0)
                {
                    InjectWheel(MOUSEEVENTF_WHEEL, verticalDelta);
                }
                if (horizontalDelta != 0)
                {
                    InjectWheel(MOUSEEVENTF_HWHEEL, horizontalDelta);
                }

                if (!vertical.active && !horizontal.active && !verticalSmooth.active && !horizontalSmooth.active)
                {
                    KillTimer(nullptr, momentumTimer);
                    momentumTimer = 0;
                }
            }
        }
    }

    bool StartInjectorThread()
    {
        HANDLE ready = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!ready)
        {
            return false;
        }

        try
        {
            g_injectorThread = std::thread(InjectorThreadMain, ready);
        }
        catch (...)
        {
            CloseHandle(ready);
            return false;
        }

        WaitForSingleObject(ready, 5000);
        CloseHandle(ready);
        return g_injectorThreadId != 0;
    }

    void StopInjectorThread()
    {
        if (g_injectorThread.joinable())
        {
            PostThreadMessageW(g_injectorThreadId, WM_QUIT, 0, 0);
            g_injectorThread.join();
        }
    }

    // Returns true if the tick was handed to the injector thread (caller then swallows the original).
    bool QueueInjection(DWORD flags, int delta)
    {
        ++g_pendingInjections;
        if (!PostThreadMessageW(g_injectorThreadId, WM_INJECT_WHEEL, flags, delta))
        {
            --g_pendingInjections;
            return false;
        }

        return true;
    }

    // Hot path: no logging, no allocations.
    LRESULT CALLBACK MouseProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode == HC_ACTION && !g_paused.load(std::memory_order_relaxed) && (wParam == WM_MOUSEWHEEL || (wParam == WM_MOUSEHWHEEL && g_settings.applyToHorizontalWheel)))
        {
            const auto* info = reinterpret_cast<const MSLLHOOKSTRUCT*>(lParam);

            // Always ignore our own re-injected events. Other injected input (remote sessions, other tools)
            // is ignored too, except in the test-only build used by the end-to-end smoke test.
#ifdef SCROLLACCELERATION_TEST_ACCEPT_INJECTED
            const bool ignore = info->dwExtraInfo == INJECTED_MARKER;
#else
            const bool ignore = info->dwExtraInfo == INJECTED_MARKER || (info->flags & (LLMHF_INJECTED | LLMHF_LOWER_IL_INJECTED));
#endif
            if (!ignore && !IsForegroundAppExcluded())
            {
                if (g_settings.ignoreWithCtrl && (GetAsyncKeyState(VK_CONTROL) & 0x8000))
                {
                    if (g_settings.momentum.enabled)
                    {
                        std::scoped_lock lock(g_momentumMutex);
                        g_verticalMomentum.Cancel();
                        g_horizontalMomentum.Cancel();
                        g_verticalSmoothing.Reset();
                        g_horizontalSmoothing.Reset();
                    }
                }
                else
                {
                    const int delta = static_cast<short>(HIWORD(info->mouseData));
                    const bool vertical = wParam == WM_MOUSEWHEEL;
                    auto& calculator = vertical ? g_vertical : g_horizontal;

                    const int scaled = calculator.Scale(delta, info->time);
                    if (g_settings.momentum.enabled)
                    {
                        {
                            std::scoped_lock lock(g_momentumMutex);
                            (vertical ? g_verticalMomentum : g_horizontalMomentum).OnTick(scaled, info->time);
                        }
                        // Smooth scroll: hand the tick to the glide queue instead of delivering it in one jump.
                        // Ordering is kept because every tick of this axis goes through the same queue.
                        {
                            std::scoped_lock lock(g_momentumMutex);
                            (vertical ? g_verticalSmoothing : g_horizontalSmoothing).Add(scaled, static_cast<uint32_t>(GetTickCount()));
                        }
                        PostThreadMessageW(g_injectorThreadId, WM_MOMENTUM_WAKE, 0, 0);
                        return 1; // swallow the original event
                    }
                    // Also route unscaled ticks through the injector while earlier ones are still queued,
                    // so the event order seen by applications never changes.
                    if (scaled != delta || g_pendingInjections.load() > 0)
                    {
                        if (QueueInjection(vertical ? MOUSEEVENTF_WHEEL : MOUSEEVENTF_HWHEEL, scaled))
                        {
                            return 1; // swallow the original event
                        }
                    }
                }
            }
        }

        return CallNextHookEx(nullptr, nCode, wParam, lParam);
    }

    LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
        case WM_APPLY_SETTINGS:
        {
            SettingsSnapshot snapshot;
            {
                std::scoped_lock lock(g_pendingMutex);
                snapshot = g_pendingSettings;
            }
            ApplySettings(snapshot);
            return 0;
        }
        case WM_TOGGLE_PAUSE:
        {
            const bool paused = !g_paused.load();
            g_paused = paused;
            if (paused)
            {
                // Stop any momentum already in flight.
                std::scoped_lock lock(g_momentumMutex);
                g_verticalMomentum.Cancel();
                g_horizontalMomentum.Cancel();
                g_verticalSmoothing.Reset();
                g_horizontalSmoothing.Reset();
            }
            return 0;
        }
        case WM_TOGGLE_EXCLUDE_APP:
        {
            // Adds the foreground app to the exclusion list, or removes it if it is already there.
            const std::wstring name = GetExecutableName(GetForegroundWindow());
            if (name.empty())
            {
                return 0;
            }

            auto list = g_settings.excludedApps;
            const auto it = std::find(list.begin(), list.end(), name);
            const bool nowExcluded = it == list.end();
            if (nowExcluded)
            {
                list.push_back(name);
            }
            else
            {
                list.erase(it);
            }

            std::wstring text;
            for (const auto& app : list)
            {
                text += app;
                text += L"\r\n";
            }

            try
            {
                auto values = PowerToysSettings::PowerToyValues::load_from_settings_file(L"ScrollAcceleration");
                values.add_property(L"excluded_apps", text);
                values.save_to_settings_file();
            }
            catch (...)
            {
                return 0;
            }

            g_settings.excludedApps = std::move(list);
            g_cachedForeground = nullptr;
            MessageBeep(nowExcluded ? MB_ICONEXCLAMATION : MB_OK);
            return 0;
        }
        case WM_CLOSE:
            DestroyWindow(hwnd);
            return 0;
        case WM_DESTROY:
            PostQuitMessage(0);
            return 0;
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    void CleanupRuntime()
    {
        if (g_hhkMouse)
        {
            UnhookWindowsHookEx(g_hhkMouse);
            g_hhkMouse = nullptr;
        }

        StopInjectorThread();

        if (g_hStopSettingsEvent)
        {
            SetEvent(g_hStopSettingsEvent);
        }

        if (g_settingsThread.joinable())
        {
            g_settingsThread.join();
        }

        for (HANDLE* handle : { &g_hReloadSettingsEvent, &g_hExitEvent, &g_hToggleEvent, &g_hExcludeAppEvent, &g_hStopSettingsEvent, &g_hInstanceMutex })
        {
            if (*handle)
            {
                CloseHandle(*handle);
                *handle = nullptr;
            }
        }
    }
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE, LPWSTR lpCmdLine, int)
{
    // Prevent multiple instances.
    g_hInstanceMutex = CreateMutexW(nullptr, TRUE, INSTANCE_MUTEX_NAME);
    if (!g_hInstanceMutex || GetLastError() == ERROR_ALREADY_EXISTS)
    {
        CleanupRuntime();
        return 1;
    }

    // Require the runner PID on the command line; refuse to run standalone.
    const std::wstring pid(lpCmdLine);
    if (pid.empty())
    {
        MessageBoxW(nullptr, L"Scroll Acceleration can't run as a standalone. Start it from PowerToys.", APP_TITLE, MB_ICONERROR);
        CleanupRuntime();
        return 1;
    }

    SettingsSnapshot initial;
    if (TryLoadSettings(g_settings, initial))
    {
        g_settings = initial;
    }
    ApplySettings(g_settings);

    g_hReloadSettingsEvent = CreateEventW(nullptr, FALSE, FALSE, CommonSharedConstants::SCROLLACCELERATION_REFRESH_SETTINGS_EVENT);
    g_hExitEvent = CreateEventW(nullptr, FALSE, FALSE, CommonSharedConstants::SCROLLACCELERATION_EXIT_EVENT);
    g_hToggleEvent = CreateEventW(nullptr, FALSE, FALSE, CommonSharedConstants::SCROLLACCELERATION_TOGGLE_EVENT);
    g_hExcludeAppEvent = CreateEventW(nullptr, FALSE, FALSE, CommonSharedConstants::SCROLLACCELERATION_EXCLUDE_APP_EVENT);
    g_hStopSettingsEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!g_hReloadSettingsEvent || !g_hExitEvent || !g_hToggleEvent || !g_hExcludeAppEvent || !g_hStopSettingsEvent)
    {
        CleanupRuntime();
        return 1;
    }

    WNDCLASSEXW wc{};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = CLASS_NAME;
    if (!RegisterClassExW(&wc))
    {
        CleanupRuntime();
        return 1;
    }

    g_hMsgWnd = CreateWindowExW(0, CLASS_NAME, APP_TITLE, 0, 0, 0, 0, 0, HWND_MESSAGE, nullptr, hInstance, nullptr);
    if (!g_hMsgWnd)
    {
        CleanupRuntime();
        return 1;
    }

    if (!StartInjectorThread())
    {
        CleanupRuntime();
        return 1;
    }

    g_hhkMouse = SetWindowsHookExW(WH_MOUSE_LL, MouseProc, hInstance, 0);
    if (!g_hhkMouse)
    {
        CleanupRuntime();
        return 1;
    }

    try
    {
        g_settingsThread = std::thread(SettingsWatcherThread, g_settings);
    }
    catch (...)
    {
        CleanupRuntime();
        return 1;
    }

    const HWND messageWindow = g_hMsgWnd;
    ProcessWaiter::OnProcessTerminate(pid, [messageWindow](DWORD) {
        PostMessageW(messageWindow, WM_CLOSE, 0, 0);
    });

    // The message loop is required for low-level hooks to work.
    MSG msg{};
    int exitCode = 0;
    for (;;)
    {
        const BOOL result = GetMessageW(&msg, nullptr, 0, 0);
        if (result == 0)
        {
            exitCode = static_cast<int>(msg.wParam);
            break;
        }
        if (result == -1)
        {
            exitCode = 1;
            break;
        }

        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    CleanupRuntime();
    return exitCode;
}
