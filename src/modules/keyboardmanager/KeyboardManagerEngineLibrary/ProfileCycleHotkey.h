#pragma once

#include <atomic>
#include <functional>
#include <thread>

#include <Windows.h>

// Registers a global hotkey (on its own hidden-window thread) that cycles the active per-keyboard
// profile. The hotkey definition comes from deviceProfiles.json ("cycleHotkey"); when absent the
// hotkey is unregistered and the feature is inert. RegisterHotKey needs a thread with a message
// loop, and the engine's main loop is thread-message-only, so this owns a window like the tracker.
class ProfileCycleHotkey
{
public:
    using Callback = std::function<void()>;

    explicit ProfileCycleHotkey(Callback callback);
    ~ProfileCycleHotkey();

    ProfileCycleHotkey(const ProfileCycleHotkey&) = delete;
    ProfileCycleHotkey& operator=(const ProfileCycleHotkey&) = delete;

    // Starts the listener thread. Safe to call once; subsequent calls are ignored.
    void Start();

    // Signals the listener thread to exit and joins it. Safe to call multiple times.
    void Stop();

    // Applies a new hotkey (fsModifiers per RegisterHotKey semantics; vk == 0 unregisters).
    // Thread-safe; takes effect on the listener thread.
    void Update(UINT modifiers, UINT vk);

private:
    static const inline UINT ApplyHotkeyMessage = WM_APP + 2;
    static const inline int HotkeyId = 1;

    static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);

    void ThreadMain();
    void ApplyPendingRegistration(HWND hwnd);

    Callback m_callback;
    std::thread m_thread;
    std::atomic<HWND> m_hwnd{ nullptr };
    std::atomic<DWORD> m_threadId{ 0 };
    std::atomic_bool m_started{ false };
    std::atomic<UINT> m_pendingModifiers{ 0 };
    std::atomic<UINT> m_pendingVk{ 0 };
    bool m_registered = false; // listener-thread only
};
