// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <windows.h>
#include <atomic>
#include <mutex>
#include <thread>
#include <unordered_map>

// Minimum window size threshold (width and height in pixels)
constexpr int MIN_WINDOW_SIZE = 100;

// Custom window message to stop the message loop
constexpr UINT WM_CURSORFOCUS_STOP = WM_USER + 1;

// Core cursor focus engine using WinEventHook
class CursorFocusCore
{
public:
    CursorFocusCore();
    ~CursorFocusCore();

    // Start/stop the focus monitoring
    void Start();
    void Stop();

    // Settings (must be called before Start(), or will take effect on restart)
    void SetFocusChangeDelayMs(int delayMs);
    void SetTargetPosition(int targetPosition); // 0=Center of window, 1=Center of title bar
    void SetDisableOnFullScreen(bool disable);
    void SetDisableOnGameMode(bool disable);

    bool IsRunning() const { return m_running; }

private:
    // Thread entry point with message loop
    void ThreadProc();

    // WinEventHook callback
    static void CALLBACK WinEventProc(
        HWINEVENTHOOK hWinEventHook,
        DWORD event,
        HWND hwnd,
        LONG idObject,
        LONG idChild,
        DWORD dwEventThread,
        DWORD dwmsEventTime);

    // Handle focus change
    void HandleFocusChange(HWND hwnd);

    // Move cursor to target position
    void MoveCursorToWindow(HWND hwnd);

    // Check if window is valid for cursor movement
    bool IsValidWindow(HWND hwnd);

    // Check if window is full screen
    bool IsFullScreenWindow(HWND hwnd);

    // Check if Game Mode is active
    bool IsGameModeActive();

    // Get the target point for cursor movement
    POINT GetTargetPoint(HWND hwnd);

    // Get DPI-aware window rect
    RECT GetDpiAwareWindowRect(HWND hwnd);

    // Get title bar height for the window
    int GetTitleBarHeight(HWND hwnd);

    // Check if cursor is already within the window bounds
    bool IsCursorWithinWindow(HWND hwnd);

    // Static instance for callback
    static CursorFocusCore* s_instance;

    // Worker thread with message loop
    std::thread m_thread;
    DWORD m_threadId = 0;

    // WinEventHook handle
    HWINEVENTHOOK m_hook = nullptr;

    // Running state
    std::atomic<bool> m_running{ false };

    // Settings
    std::atomic<int> m_focusChangeDelayMs{ 200 };
    std::atomic<int> m_targetPosition{ 0 }; // 0=Center of window, 1=Center of title bar
    std::atomic<bool> m_disableOnFullScreen{ false };
    std::atomic<bool> m_disableOnGameMode{ false };

    // Delayed cursor move
    std::atomic<HWND> m_pendingWindow{ nullptr };
    UINT_PTR m_timerId = 0;

    // Timer callback
    static void CALLBACK TimerProc(HWND hwnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime);

    // Mutex for thread safety
    std::mutex m_mutex;
};
