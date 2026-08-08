#pragma once

#include <atomic>
#include <functional>
#include <string>
#include <thread>

#include <Windows.h>

// Receives raw keyboard input on a dedicated hidden-window thread so the engine can tell which
// physical keyboard produced a keystroke. This is the detection half of per-keyboard profile
// auto-switching; the switching policy lives in the callback owner (KeyboardManager).
//
// The engine's main loop is a thread-message loop (no window), and WM_INPUT is only delivered to a
// window's WndProc, so this runs its own window + message pump on a separate thread. Uses
// RIDEV_INPUTSINK to observe keystrokes regardless of foreground focus, and never suppresses input.
class RawInputKeyboardTracker
{
public:
    struct KeyEvent
    {
        // RIDI_DEVICENAME device interface path: stable, unique per physical device, and the
        // identity key we match against the device->profile map. Empty when injected.
        std::wstring devicePath;
        USHORT vkey = 0;
        bool keyDown = false;

        // hDevice == NULL: synthetic/injected input (including KBM's own remap output). The policy
        // layer must ignore these for switching, or self-injected keys would trigger switches.
        bool injected = false;
    };

    using Callback = std::function<void(const KeyEvent&)>;

    explicit RawInputKeyboardTracker(Callback callback);
    ~RawInputKeyboardTracker();

    RawInputKeyboardTracker(const RawInputKeyboardTracker&) = delete;
    RawInputKeyboardTracker& operator=(const RawInputKeyboardTracker&) = delete;

    // Starts the listener thread. Safe to call once; subsequent calls are ignored.
    void Start();

    // Signals the listener thread to exit and joins it. Safe to call multiple times.
    void Stop();

private:
    static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);

    void ThreadMain();
    void HandleRawInput(HRAWINPUT hRawInput);

    Callback m_callback;
    std::thread m_thread;
    std::atomic<DWORD> m_threadId{ 0 };
    std::atomic_bool m_started{ false };
};
