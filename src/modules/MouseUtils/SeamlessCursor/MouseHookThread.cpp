// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "MouseHookThread.h"

#include "CalibrationStore.h"
#include "EdgeClaims.h"
#include "Monitors.h"

#include <common/logger/logger.h>

#include <algorithm>
#include <dbt.h>
#include <system_error>
#include <wtsapi32.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace seamless_cursor
{
    namespace
    {
        constexpr wchar_t windowClassName[] = L"PowerToys.SeamlessCursor.HookWindow";
        constexpr UINT requestMessage = WM_APP + 1;
        constexpr UINT stopMessage = WM_APP + 2;
        constexpr UINT_PTR rebuildTimer = 1;
        constexpr UINT_PTR watchdogTimer = 2;
        constexpr UINT rebuildDelayMs = 500;
        constexpr UINT retryDelayMs = 5000;
        constexpr UINT maxRetryDelayMs = 60000;
        constexpr UINT watchdogIntervalMs = 2000;

        // Mouse messages synthesized from pen or touch input carry this signature in dwExtraInfo.
        constexpr ULONG_PTR penSignatureMask = 0xFFFFFF00;
        constexpr ULONG_PTR penSignature = 0xFF515700;

        // Injected moves at least this long (physical pixels, along either axis) are jumps to a target point.
        constexpr LONG injectedJumpThreshold = 128;

        // The hook procedure has no context parameter.
        MouseHookThread* g_instance = nullptr;

        HINSTANCE ModuleInstance() noexcept
        {
            return reinterpret_cast<HINSTANCE>(&__ImageBase);
        }

        constexpr bool IsPenOrTouch(ULONG_PTR extraInfo) noexcept
        {
            return (extraInfo & penSignatureMask) == penSignature;
        }

        // Automation and remote-control tools inject a jump straight to the point they want; remapping would move
        // their clicks off target. Some touchpads also inject, but only small relative moves, which stay remapped.
        bool IsInjectedJump(const MSLLHOOKSTRUCT& info, POINT cursor) noexcept
        {
            return (info.flags & LLMHF_INJECTED) != 0 &&
                   (std::abs(info.pt.x - cursor.x) >= injectedJumpThreshold || std::abs(info.pt.y - cursor.y) >= injectedJumpThreshold);
        }

        // Games and other apps that confine or hide the cursor manage it themselves.
        bool IsCursorManagedByApp() noexcept
        {
            CURSORINFO cursor{};
            cursor.cbSize = sizeof(cursor);
            if (GetCursorInfo(&cursor) && (cursor.flags & CURSOR_SHOWING) == 0)
            {
                return true;
            }

            RECT clip{};
            if (!GetClipCursor(&clip))
            {
                return false;
            }

            return clip.right - clip.left < GetSystemMetrics(SM_CXVIRTUALSCREEN) ||
                   clip.bottom - clip.top < GetSystemMetrics(SM_CYVIRTUALSCREEN);
        }
    }

    MouseHookThread::~MouseHookThread()
    {
        Stop();
    }

    bool MouseHookThread::Start()
    {
        if (m_thread.joinable())
        {
            return true;
        }

        const HANDLE ready = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!ready)
        {
            Logger::error(L"Failed to create the Seamless Multi-Screen Cursor startup event. Error: {}", GetLastError());
            return false;
        }

        // A request that raced with the last Stop, a suspension or lines being tried must not outlive that run.
        {
            std::scoped_lock lock{ m_requestMutex };
            m_requests.clear();
        }

        m_suspended = false;
        m_preview.reset();
        m_calibrations.clear();
        m_readPending = true;
        m_retries = 0;

        g_instance = this;
        try
        {
            m_thread = std::thread([this, ready]() { Run(ready); });
        }
        catch (const std::system_error& error)
        {
            Logger::error("Failed to start the Seamless Multi-Screen Cursor hook thread: {}", error.what());
            g_instance = nullptr;
            CloseHandle(ready);
            return false;
        }

        WaitForSingleObject(ready, INFINITE);
        CloseHandle(ready);

        if (!m_window)
        {
            m_thread.join();
            g_instance = nullptr;
            return false;
        }

        return true;
    }

    void MouseHookThread::Stop()
    {
        if (!m_thread.joinable())
        {
            return;
        }

        PostMessageW(m_window, stopMessage, 0, 0);
        m_thread.join();
        m_window = nullptr;
        g_instance = nullptr;
    }

    void MouseHookThread::ReloadCalibration()
    {
        Post({ Request::Kind::Reload, {} });
    }

    void MouseHookThread::Preview(std::vector<BorderCalibration> calibrations)
    {
        Post({ Request::Kind::Preview, std::move(calibrations) });
    }

    void MouseHookThread::Suspend()
    {
        Post({ Request::Kind::Suspend, {} });
    }

    void MouseHookThread::SetOptions(EngineOptions options)
    {
        if (!m_thread.joinable())
        {
            m_options = options;
            return;
        }

        Post({ Request::Kind::Options, {}, options });
    }

    void MouseHookThread::Post(Request request)
    {
        if (!m_window)
        {
            return;
        }

        {
            std::scoped_lock lock{ m_requestMutex };
            m_requests.push_back(std::move(request));
        }

        PostMessageW(m_window, requestMessage, 0, 0);
    }

    void MouseHookThread::ProcessRequests()
    {
        std::deque<Request> requests;
        {
            std::scoped_lock lock{ m_requestMutex };
            requests.swap(m_requests);
        }

        for (const auto& request : requests)
        {
            switch (request.kind)
            {
            case Request::Kind::Reload:
                m_readPending = true;
                m_retries = 0;
                m_preview.reset();
                m_suspended = false;
                Rebuild();
                break;
            case Request::Kind::Preview:
                // Monitors are cached by the last rebuild, so trying lines while adjusting stays cheap.
                m_preview = request.calibrations;
                m_suspended = false;
                m_engine.Build(m_monitors, *m_preview, m_options);
                UpdateHook();
                break;
            case Request::Kind::Options:
                m_options = request.options;
                Rebuild();
                break;
            default:
                m_suspended = true;
                PublishEdgeClaims(nullptr);
                break;
            }
        }
    }

    void MouseHookThread::Run(HANDLE ready)
    {
        // Hook points, monitor rectangles and cursor positions must all be physical pixels.
        SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        WNDCLASSEXW windowClass{};
        windowClass.cbSize = sizeof(windowClass);
        windowClass.lpfnWndProc = WindowProc;
        windowClass.hInstance = ModuleInstance();
        windowClass.lpszClassName = windowClassName;
        RegisterClassExW(&windowClass);

        // A hidden top-level window, because message-only windows do not receive display-change broadcasts.
        m_window = CreateWindowExW(WS_EX_TOOLWINDOW, windowClassName, L"", WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, ModuleInstance(), nullptr);
        const DWORD createError = GetLastError();
        SetEvent(ready);
        if (!m_window)
        {
            Logger::error(L"Failed to create the Seamless Multi-Screen Cursor window. Error: {}", createError);
            UnregisterClassW(windowClassName, ModuleInstance());
            return;
        }

        // A rebuild that failed while the session was locked is tried again shortly after unlocking. Without the
        // notification, only the slower retry timer recovers.
        if (!WTSRegisterSessionNotification(m_window, NOTIFY_FOR_THIS_SESSION))
        {
            Logger::warn(L"Seamless Multi-Screen Cursor cannot watch for session unlocks. Error: {}", GetLastError());
        }

        Rebuild();
        SetTimer(m_window, watchdogTimer, watchdogIntervalMs, nullptr);

        MSG message;
        while (GetMessageW(&message, nullptr, 0, 0) > 0)
        {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }

        if (m_hook)
        {
            UnhookWindowsHookEx(m_hook);
            m_hook = nullptr;
        }

        PublishEdgeClaims(nullptr);
        UnregisterClassW(windowClassName, ModuleInstance());
    }

    void MouseHookThread::Rebuild()
    {
        // Mouse input waits while this runs, so skip the physical sizes, which only the overlay needs, and read the
        // calibration file only after a start or a Reload, until a read succeeds. The read does not wait while another
        // program holds the file; the lines read before stay in use, and the retry timer below tries again.
        if (m_readPending)
        {
            if (auto calibrations = TryLoadCalibrations())
            {
                m_calibrations = std::move(*calibrations);
                m_readPending = false;
            }
        }

        const auto monitors = EnumerateMonitors(false);
        m_monitors = ToEngineMonitors(monitors);
        m_engine.Build(m_monitors, m_preview ? *m_preview : m_calibrations, m_options);

        // Calibrations name monitors by device path. While Windows cannot tell it, nothing would match, so wait as
        // after a display change.
        const bool identified = std::all_of(monitors.begin(), monitors.end(), [](const MonitorDetails& m) { return m.identified; });
        m_layoutStale = !identified;
        UpdateHook();

        // Unknown identities (a locked session) and a locked calibration file usually pass, so try again, less and less
        // often.
        if (!identified || m_readPending)
        {
            SetTimer(m_window, rebuildTimer, std::min(retryDelayMs << std::min(m_retries, 4u), maxRetryDelayMs), nullptr);
            ++m_retries;
        }
        else
        {
            // Nothing left to retry; a pending retry would only rebuild once more.
            m_retries = 0;
            KillTimer(m_window, rebuildTimer);
        }

        if (!identified)
        {
            if (!m_identityFailureLogged)
            {
                Logger::warn(L"Seamless Multi-Screen Cursor cannot identify the monitors yet; retrying.");
                m_identityFailureLogged = true;
            }

            return;
        }

        m_identityFailureLogged = false;
        Logger::info(L"Seamless Multi-Screen Cursor layout rebuilt for {} monitors; remapping is {}.", m_monitors.size(), m_hook ? L"active" : L"inactive");
    }

    void MouseHookThread::UpdateHook()
    {
        // Without a calibrated border the hook is not installed at all, so it costs nothing.
        const bool needed = !m_engine.IsEmpty();
        if (needed && !m_hook)
        {
            m_hook = SetWindowsHookExW(WH_MOUSE_LL, MouseProc, ModuleInstance(), 0);
            if (m_hook)
            {
                // Start the watchdog from now, so moves made before the hook existed do not count against it.
                GetPhysicalCursorPos(&m_cursorAtLastCheck);
                m_hookCallsAtLastCheck = m_hookCalls;
                m_installFailureLogged = false;
            }
            else if (!m_installFailureLogged)
            {
                // The watchdog keeps retrying; one log line is enough.
                Logger::error(L"Failed to install the Seamless Multi-Screen Cursor mouse hook. Error: {}", GetLastError());
                m_installFailureLogged = true;
            }
        }
        else if (!needed && m_hook)
        {
            UnhookWindowsHookEx(m_hook);
            m_hook = nullptr;
        }

        // CursorWrap leaves claimed edges alone, so claim them only while this hook remaps there.
        PublishEdgeClaims(m_hook && !m_layoutStale && !m_suspended ? std::make_shared<const Engine>(m_engine) : nullptr);
    }

    void MouseHookThread::CheckHookAlive()
    {
        if (!m_hook)
        {
            // An earlier install failed; try again.
            if (!m_engine.IsEmpty())
            {
                UpdateHook();
            }

            return;
        }

        POINT cursor{};
        if (!GetPhysicalCursorPos(&cursor))
        {
            return;
        }

        // Windows silently removes a low-level hook that keeps exceeding LowLevelHooksTimeout. If the cursor
        // moved but the hook saw nothing, install it again. Moves made with SetCursorPos also trigger this, and so
        // does every move while an elevated window has focus and PowerToys is not elevated, because Windows sends
        // the hook no input then. A reinstall is cheap, but only the first one is logged.
        const bool moved = cursor.x != m_cursorAtLastCheck.x || cursor.y != m_cursorAtLastCheck.y;
        if (moved && m_hookCalls == m_hookCallsAtLastCheck)
        {
            if (!m_reinstallLogged)
            {
                Logger::warn("Seamless Multi-Screen Cursor mouse hook received no input while the cursor moved; reinstalling it. Later reinstalls are not logged.");
                m_reinstallLogged = true;
            }

            UnhookWindowsHookEx(m_hook);
            m_hook = nullptr;
            UpdateHook();
        }

        m_cursorAtLastCheck = cursor;
        m_hookCallsAtLastCheck = m_hookCalls;
    }

    LRESULT CALLBACK MouseHookThread::MouseProc(int code, WPARAM wParam, LPARAM lParam)
    {
        auto* const self = g_instance;
        if (code == HC_ACTION && wParam == WM_MOUSEMOVE && self)
        {
            ++self->m_hookCalls;
            const auto* const info = reinterpret_cast<const MSLLHOOKSTRUCT*>(lParam);
            POINT cursor{};
            if (!self->m_suspended && !self->m_layoutStale && !IsPenOrTouch(info->dwExtraInfo) && GetPhysicalCursorPos(&cursor) &&
                !IsInjectedJump(*info, cursor))
            {
                // Before the move is applied, the cursor is still on the monitor the move starts from.
                const auto remap = self->m_engine.Resolve(cursor, info->pt);
                if (remap && !IsCursorManagedByApp())
                {
                    if (remap->outerWall)
                    {
                        // At an outer desktop edge, CursorWrap or Mouse Without Borders may take the cursor; only
                        // if none of the other hooks did, it stops here.
                        const LRESULT result = CallNextHookEx(nullptr, code, wParam, lParam);
                        return result != 0 ? result : (SetPhysicalCursorPos(remap->point.x, remap->point.y) ? 1 : 0);
                    }

                    if (SetPhysicalCursorPos(remap->point.x, remap->point.y))
                    {
                        return 1;
                    }
                }
            }
        }

        return CallNextHookEx(nullptr, code, wParam, lParam);
    }

    LRESULT CALLBACK MouseHookThread::WindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
    {
        auto* const self = g_instance;
        switch (message)
        {
        case WM_DISPLAYCHANGE:
            // Until the rebuild, the engine still has the old monitor rectangles: let moves through, claim nothing.
            if (self)
            {
                self->m_layoutStale = true;
                self->m_retries = 0;
                PublishEdgeClaims(nullptr);
            }

            // Several notifications arrive per change; restarting the timer rebuilds once they settle.
            SetTimer(window, rebuildTimer, rebuildDelayMs, nullptr);
            break;
        case WM_DEVICECHANGE:
        case WM_SETTINGCHANGE:
            // While the layout is stale, a rebuild is already pending; restarting its timer on every device
            // notification (a dock enumerating, a flapping USB device) could put it off indefinitely.
            if ((message == WM_DEVICECHANGE ? wParam == DBT_DEVNODES_CHANGED : wParam == SPI_SETWORKAREA) &&
                !(self && self->m_layoutStale))
            {
                SetTimer(window, rebuildTimer, rebuildDelayMs, nullptr);
            }
            break;
        case requestMessage:
            if (self)
            {
                self->ProcessRequests();
            }
            return 0;
        case stopMessage:
            DestroyWindow(window);
            return 0;
        case WM_CLOSE:
            // Only Stop ends this thread. A WM_CLOSE from outside (taskkill without /f) would leave the module enabled
            // without remapping.
            return 0;
        case WM_TIMER:
            if (wParam == rebuildTimer)
            {
                KillTimer(window, rebuildTimer);
                if (self)
                {
                    self->Rebuild();
                }
            }
            else if (wParam == watchdogTimer && self)
            {
                self->CheckHookAlive();
            }
            return 0;
        case WM_WTSSESSION_CHANGE:
            if (wParam == WTS_SESSION_UNLOCK && self && (self->m_layoutStale || self->m_readPending))
            {
                self->m_retries = 0;
                SetTimer(window, rebuildTimer, rebuildDelayMs, nullptr);
            }
            return 0;
        case WM_DESTROY:
            WTSUnRegisterSessionNotification(window);
            PostQuitMessage(0);
            return 0;
        }

        return DefWindowProcW(window, message, wParam, lParam);
    }
}
