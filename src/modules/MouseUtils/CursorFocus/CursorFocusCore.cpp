// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CursorFocusCore.h"
#include <dwmapi.h>
#include <shellapi.h>
#include <shellscalingapi.h>
#include "../../../common/logger/logger.h"

#pragma comment(lib, "dwmapi.lib")
#pragma comment(lib, "shcore.lib")

// Debug output helper - writes to both VS Output window and log file
#ifdef _DEBUG
#define DEBUG_LOG(msg) do { \
    OutputDebugStringW(msg); \
    OutputDebugStringW(L"\n"); \
    Logger::info(msg); \
} while(0)

#define DEBUG_LOG_FMT(fmt, ...) do { \
    wchar_t _dbg_buf[1024]; \
    swprintf_s(_dbg_buf, fmt, __VA_ARGS__); \
    OutputDebugStringW(_dbg_buf); \
    OutputDebugStringW(L"\n"); \
    Logger::info(_dbg_buf); \
} while(0)
#else
#define DEBUG_LOG(msg) ((void)0)
#define DEBUG_LOG_FMT(fmt, ...) ((void)0)
#endif

// Static instance pointer
CursorFocusCore* CursorFocusCore::s_instance = nullptr;

CursorFocusCore::CursorFocusCore()
{
    s_instance = this;
}

CursorFocusCore::~CursorFocusCore()
{
    Stop();
    s_instance = nullptr;
}

void CursorFocusCore::Start()
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (m_running)
    {
        Logger::info(L"CursorFocusCore::Start() - already running, returning");
        return;
    }

    m_running = true;
    Logger::info(L"CursorFocusCore::Start() - starting worker thread");

    // Start the worker thread with its own message loop
    m_thread = std::thread([this]() {
        ThreadProc();
    });

    Logger::trace(L"CursorFocus: Started focus monitoring");
}

void CursorFocusCore::Stop()
{
    {
        std::lock_guard<std::mutex> lock(m_mutex);

        if (!m_running)
        {
            return;
        }

        m_running = false;

        // Post quit message to the worker thread
        if (m_threadId != 0)
        {
            PostThreadMessage(m_threadId, WM_QUIT, 0, 0);
        }
    }

    // Wait for the thread to finish (outside the lock)
    if (m_thread.joinable())
    {
        m_thread.join();
    }

    m_threadId = 0;
    m_pendingWindow = nullptr;

    Logger::trace(L"CursorFocus: Stopped focus monitoring");
}

void CursorFocusCore::ThreadProc()
{
    // Store thread ID for posting messages
    m_threadId = GetCurrentThreadId();

    Logger::info(L"CursorFocusCore::ThreadProc started on thread {}", m_threadId);
    DEBUG_LOG_FMT(L"CursorFocus: ThreadProc started on thread %u", m_threadId);

    // Set up WinEventHook for foreground window changes
    m_hook = SetWinEventHook(
        EVENT_SYSTEM_FOREGROUND,
        EVENT_SYSTEM_FOREGROUND,
        nullptr,
        WinEventProc,
        0,
        0,
        WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

    if (!m_hook)
    {
        DWORD error = GetLastError();
        Logger::error(L"CursorFocusCore: Failed to set WinEventHook, error {}", error);
        DEBUG_LOG_FMT(L"CursorFocus: Failed to set WinEventHook, error %u", error);
        return;
    }

    Logger::info(L"CursorFocusCore: WinEventHook installed successfully");
    DEBUG_LOG_FMT(L"CursorFocus: WinEventHook installed successfully (hook=0x%p)", m_hook);

    Logger::trace(L"CursorFocus: WinEventHook installed, entering message loop");

    // Message loop - required for WinEventHook and timers to work
    MSG msg;
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        if (msg.message == WM_TIMER)
        {
            DEBUG_LOG(L"CursorFocus: Message loop received WM_TIMER");
        }
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    Logger::info(L"CursorFocusCore: Message loop exiting (WM_QUIT received)");
    DEBUG_LOG(L"CursorFocus: Message loop exiting (WM_QUIT received)");

    // Cleanup
    if (m_timerId != 0)
    {
        KillTimer(nullptr, m_timerId);
        m_timerId = 0;
    }

    if (m_hook)
    {
        UnhookWinEvent(m_hook);
        m_hook = nullptr;
    }

    Logger::trace(L"CursorFocus: Message loop ended, cleanup complete");
}

void CursorFocusCore::SetFocusChangeDelayMs(int delayMs)
{
    m_focusChangeDelayMs.store(std::max(100, std::min(500, delayMs)));
}

void CursorFocusCore::SetTargetPosition(int targetPosition)
{
    m_targetPosition.store(targetPosition);
}

void CursorFocusCore::SetDisableOnFullScreen(bool disable)
{
    m_disableOnFullScreen.store(disable);
}

void CursorFocusCore::SetDisableOnGameMode(bool disable)
{
    m_disableOnGameMode.store(disable);
}

void CALLBACK CursorFocusCore::WinEventProc(
    HWINEVENTHOOK /*hWinEventHook*/,
    DWORD event,
    HWND hwnd,
    LONG idObject,
    LONG /*idChild*/,
    DWORD /*dwEventThread*/,
    DWORD /*dwmsEventTime*/)
{
    if (event != EVENT_SYSTEM_FOREGROUND || idObject != OBJID_WINDOW || !s_instance)
    {
        return;
    }

    // Get window title for debug logging
    wchar_t windowTitle[256] = {};
    GetWindowTextW(hwnd, windowTitle, ARRAYSIZE(windowTitle));
    wchar_t className[256] = {};
    GetClassNameW(hwnd, className, ARRAYSIZE(className));
    
    Logger::info(L"CursorFocusCore: EVENT_SYSTEM_FOREGROUND - HWND: {}, Title: '{}', Class: '{}'", 
        (void*)hwnd, windowTitle, className);
    DEBUG_LOG_FMT(L"CursorFocus: EVENT_SYSTEM_FOREGROUND - HWND: 0x%p, Title: '%s', Class: '%s'", 
        hwnd, windowTitle, className);

    s_instance->HandleFocusChange(hwnd);
}

void CursorFocusCore::HandleFocusChange(HWND hwnd)
{
    if (!m_running.load() || !hwnd)
    {
        DEBUG_LOG_FMT(L"CursorFocus: HandleFocusChange - skipping (running=%d, hwnd=0x%p)", 
            m_running.load(), hwnd);
        return;
    }

    // Check if we should skip this window
    if (!IsValidWindow(hwnd))
    {
        DEBUG_LOG(L"CursorFocus: HandleFocusChange - window is not valid, skipping");
        return;
    }

    // Check Game Mode if enabled
    if (m_disableOnGameMode.load() && IsGameModeActive())
    {
        DEBUG_LOG(L"CursorFocus: Skipping - Game Mode is active");
        return;
    }

    // Check full screen if enabled
    if (m_disableOnFullScreen.load() && IsFullScreenWindow(hwnd))
    {
        DEBUG_LOG(L"CursorFocus: Skipping - Full screen window");
        return;
    }

    // Cancel any pending timer
    if (m_timerId != 0)
    {
        DEBUG_LOG_FMT(L"CursorFocus: Cancelling pending timer (id=%llu)", (unsigned long long)m_timerId);
        KillTimer(nullptr, m_timerId);
        m_timerId = 0;
    }

    // Store the window and set up delayed cursor move
    m_pendingWindow = hwnd;

    // Set timer for delayed cursor move
    int delayMs = m_focusChangeDelayMs.load();
    m_timerId = SetTimer(nullptr, 0, static_cast<UINT>(delayMs), TimerProc);

    DEBUG_LOG_FMT(L"CursorFocus: Timer started (id=%llu, delay=%dms) for HWND 0x%p", 
        (unsigned long long)m_timerId, delayMs, hwnd);
}

void CALLBACK CursorFocusCore::TimerProc(HWND /*hwnd*/, UINT /*uMsg*/, UINT_PTR idEvent, DWORD /*dwTime*/)
{
    if (!s_instance)
    {
        DEBUG_LOG(L"CursorFocus: TimerProc - no instance, aborting");
        return;
    }

    DEBUG_LOG_FMT(L"CursorFocus: TimerProc fired (id=%llu)", (unsigned long long)idEvent);

    KillTimer(nullptr, idEvent);
    s_instance->m_timerId = 0;

    HWND pendingWnd = s_instance->m_pendingWindow.exchange(nullptr);
    if (pendingWnd && IsWindow(pendingWnd))
    {
        // Verify this is still the foreground window
        HWND foreground = GetForegroundWindow();
        if (foreground == pendingWnd)
        {
            // Check if cursor is already within the window - if so, don't move it
            if (s_instance->IsCursorWithinWindow(pendingWnd))
            {
                DEBUG_LOG_FMT(L"CursorFocus: TimerProc - cursor already within HWND 0x%p, skipping move", pendingWnd);
                Logger::info(L"CursorFocusCore: Cursor already within window, skipping move");
            }
            else
            {
                DEBUG_LOG_FMT(L"CursorFocus: TimerProc - moving cursor to HWND 0x%p", pendingWnd);
                s_instance->MoveCursorToWindow(pendingWnd);
            }
        }
        else
        {
            DEBUG_LOG_FMT(L"CursorFocus: TimerProc - foreground changed (pending=0x%p, current=0x%p), skipping",
                pendingWnd, foreground);
        }
    }
    else
    {
        DEBUG_LOG(L"CursorFocus: TimerProc - no pending window or window destroyed");
    }
}

void CursorFocusCore::MoveCursorToWindow(HWND hwnd)
{
    POINT targetPoint = GetTargetPoint(hwnd);

    // Get current cursor position for logging
    POINT currentPos = {};
    GetCursorPos(&currentPos);
    
    wchar_t windowTitle[256] = {};
    GetWindowTextW(hwnd, windowTitle, ARRAYSIZE(windowTitle));
    
    Logger::info(L"CursorFocusCore: MoveCursorToWindow - HWND {} '{}', from ({},{}) to ({},{})", 
        (void*)hwnd, windowTitle, currentPos.x, currentPos.y, targetPoint.x, targetPoint.y);
    DEBUG_LOG_FMT(L"CursorFocus: MoveCursorToWindow - HWND 0x%p '%s', from (%d,%d) to (%d,%d)", 
        hwnd, windowTitle, currentPos.x, currentPos.y, targetPoint.x, targetPoint.y);

    // Move the cursor
    BOOL result = SetCursorPos(targetPoint.x, targetPoint.y);

    if (result)
    {
        Logger::info(L"CursorFocusCore: SetCursorPos succeeded");
        DEBUG_LOG(L"CursorFocus: SetCursorPos succeeded");
    }
    else
    {
        DWORD error = GetLastError();
        Logger::error(L"CursorFocusCore: SetCursorPos FAILED with error {}", error);
        DEBUG_LOG_FMT(L"CursorFocus: SetCursorPos FAILED with error %u", error);
    }

    // TODO: Future enhancement - add optional visual feedback here
    // The code below is commented out for potential future use:
    /*
    // Brief visual feedback at new cursor position (~200ms)
    // Could use a simple overlay window or D2D effect
    */
}

bool CursorFocusCore::IsValidWindow(HWND hwnd)
{
    if (!hwnd || !IsWindow(hwnd))
    {
        DEBUG_LOG(L"CursorFocus: IsValidWindow - invalid or null HWND");
        return false;
    }

    // Must be visible
    if (!IsWindowVisible(hwnd))
    {
        DEBUG_LOG(L"CursorFocus: IsValidWindow - window not visible");
        return false;
    }

    // Get window rect
    RECT rect = GetDpiAwareWindowRect(hwnd);
    int width = rect.right - rect.left;
    int height = rect.bottom - rect.top;

    // Skip windows smaller than minimum size
    if (width < MIN_WINDOW_SIZE || height < MIN_WINDOW_SIZE)
    {
        DEBUG_LOG_FMT(L"CursorFocus: IsValidWindow - skipping small window (%dx%d)", width, height);
        return false;
    }

    // Skip certain window classes that shouldn't trigger cursor movement
    wchar_t className[256] = {};
    if (GetClassNameW(hwnd, className, ARRAYSIZE(className)))
    {
        // Skip tooltip windows
        if (wcscmp(className, L"tooltips_class32") == 0)
        {
            DEBUG_LOG(L"CursorFocus: IsValidWindow - skipping tooltip window");
            return false;
        }

        // Skip popup menus
        if (wcscmp(className, L"#32768") == 0)
        {
            DEBUG_LOG(L"CursorFocus: IsValidWindow - skipping popup menu");
            return false;
        }
    }

    wchar_t windowTitle[256] = {};
    GetWindowTextW(hwnd, windowTitle, ARRAYSIZE(windowTitle));
    DEBUG_LOG_FMT(L"CursorFocus: IsValidWindow - VALID: '%s' (class: '%s', size: %dx%d)", 
        windowTitle, className, width, height);

    return true;
}

bool CursorFocusCore::IsFullScreenWindow(HWND hwnd)
{
    RECT windowRect;
    if (!GetWindowRect(hwnd, &windowRect))
    {
        return false;
    }

    // Get the monitor this window is on
    HMONITOR monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
    MONITORINFO monitorInfo = { sizeof(monitorInfo) };
    if (!GetMonitorInfo(monitor, &monitorInfo))
    {
        return false;
    }

    // Check if window covers the entire monitor
    return windowRect.left <= monitorInfo.rcMonitor.left &&
           windowRect.top <= monitorInfo.rcMonitor.top &&
           windowRect.right >= monitorInfo.rcMonitor.right &&
           windowRect.bottom >= monitorInfo.rcMonitor.bottom;
}

bool CursorFocusCore::IsGameModeActive()
{
    // Query Windows Game Mode status
    // Using the shell API to check if game bar thinks a game is running
    QUERY_USER_NOTIFICATION_STATE state;
    if (SUCCEEDED(SHQueryUserNotificationState(&state)))
    {
        // QUNS_RUNNING_D3D_FULL_SCREEN or QUNS_BUSY typically indicate game mode
        return state == QUNS_RUNNING_D3D_FULL_SCREEN || state == QUNS_BUSY;
    }
    return false;
}

POINT CursorFocusCore::GetTargetPoint(HWND hwnd)
{
    RECT rect = GetDpiAwareWindowRect(hwnd);
    POINT target;

    if (m_targetPosition.load() == 1) // Center of title bar
    {
        int titleBarHeight = GetTitleBarHeight(hwnd);
        target.x = (rect.left + rect.right) / 2;
        target.y = rect.top + (titleBarHeight / 2);

        // Ensure we're not above the window
        if (target.y < rect.top)
        {
            target.y = rect.top + 10;
        }
    }
    else // Center of window (default)
    {
        target.x = (rect.left + rect.right) / 2;
        target.y = (rect.top + rect.bottom) / 2;
    }

    return target;
}

RECT CursorFocusCore::GetDpiAwareWindowRect(HWND hwnd)
{
    RECT rect = {};

    // Try to get the extended frame bounds (more accurate for DWM windows)
    if (FAILED(DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, &rect, sizeof(rect))))
    {
        // Fall back to regular GetWindowRect
        GetWindowRect(hwnd, &rect);
    }

    return rect;
}

int CursorFocusCore::GetTitleBarHeight(HWND hwnd)
{
    // Get the window's non-client metrics
    TITLEBARINFOEX titleBarInfo = { sizeof(titleBarInfo) };
    if (SendMessage(hwnd, WM_GETTITLEBARINFOEX, 0, reinterpret_cast<LPARAM>(&titleBarInfo)))
    {
        return titleBarInfo.rcTitleBar.bottom - titleBarInfo.rcTitleBar.top;
    }

    // Fall back to system metrics
    int dpi = 96;
    HMONITOR monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
    UINT dpiX, dpiY;
    if (SUCCEEDED(GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, &dpiX, &dpiY)))
    {
        dpi = static_cast<int>(dpiY);
    }

    // Scale caption height by DPI
    int captionHeight = GetSystemMetricsForDpi(SM_CYCAPTION, dpi);
    int frameHeight = GetSystemMetricsForDpi(SM_CYFRAME, dpi);
    int padding = GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);

    return captionHeight + frameHeight + padding;
}

bool CursorFocusCore::IsCursorWithinWindow(HWND hwnd)
{
    // Get current cursor position
    POINT cursorPos = {};
    if (!GetCursorPos(&cursorPos))
    {
        return false;
    }

    // Get the window rect
    RECT windowRect = GetDpiAwareWindowRect(hwnd);

    // Check if cursor is within the window bounds
    bool isWithin = PtInRect(&windowRect, cursorPos) != FALSE;

    DEBUG_LOG_FMT(L"CursorFocus: IsCursorWithinWindow - cursor (%d,%d), window rect (%d,%d)-(%d,%d), within=%d",
        cursorPos.x, cursorPos.y,
        windowRect.left, windowRect.top, windowRect.right, windowRect.bottom,
        isWithin ? 1 : 0);

    return isWithin;
}
