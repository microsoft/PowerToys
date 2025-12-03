

#include <iostream>
#include <windows.h>
#include <map>
#include <deque>
// New inclusions
#include <cstdint>
#include <array>

#include "KeystrokeEvent.h"
#include "EventQueue.h" 
#include "Batcher.h"  

// Old compilation command saved for reference
// Compilation: x86_64-w64-mingw32-g++ KeyboardListener.cpp -o KeyboardHookTest.exe -static -luser32 -lgdi32 (need to compile with "static" because of lack of DLL's)

// Global handle for the hook
static HHOOK g_hook = nullptr;

static SpscRing<KeystrokeEvent, 1024> g_q;

// Batcher worker (drains g_q -> JSON -> named pipe)
static Batcher g_batcher(g_q);

// Timestamping
static inline uint64_t now_micros() {
    static LARGE_INTEGER freq = []{ LARGE_INTEGER f; QueryPerformanceFrequency(&f); return f; }();
    LARGE_INTEGER c; QueryPerformanceCounter(&c);
    return static_cast<uint64_t>((c.QuadPart * 1'000'000) / freq.QuadPart);
}

// Modifier snapshot (Ctrl, Alt, Shift, Win)
static inline std::array<bool,4> snapshot_mods() {
    auto down = [](int vk){ return (GetKeyState(vk) & 0x8000) != 0; };
    return std::array<bool,4>{
        down(VK_CONTROL),  // Ctrl
        down(VK_MENU),     // Alt
        down(VK_SHIFT),    // Shift
        (down(VK_LWIN) || down(VK_RWIN)) // Win
    };
}

// Translate vk to printable character
static inline char32_t vk_to_char(UINT vk, UINT sc) {
    BYTE keystate[256]; 
    if (!GetKeyboardState(keystate)) return 0;

    WCHAR buf[4] = {0};
    HKL layout = GetKeyboardLayout(0);

    // Note: ToUnicodeEx can have side-effects for dead keys, this is "good enough" for an overlay.
    int rc = ToUnicodeEx(vk, sc, keystate, buf, 4, 0, layout);
    if (rc <= 0) return 0;
    if (!iswprint(buf[0])) return 0;
    return static_cast<char32_t>(buf[0]);
}

// Push helpers
static inline void emit_down(UINT vk, UINT sc) {
    KeystrokeEvent e{};
    e.type = KeystrokeEvent::Type::Down;
    e.vk   = static_cast<uint16_t>(vk);
    e.ch   = 0;
    e.mods = snapshot_mods();
    e.ts_micros = now_micros();
    g_q.try_push(e);

    // Also emit a Char if there is a printable character for this Down
    if (char32_t ch = vk_to_char(vk, sc)) {
        KeystrokeEvent c = e;
        c.type = KeystrokeEvent::Type::Char;
        c.ch   = ch;
        g_q.try_push(c);
    }
}

static inline void emit_up(UINT vk) {
    KeystrokeEvent e{};
    e.type = KeystrokeEvent::Type::Up;
    e.vk   = static_cast<uint16_t>(vk);
    e.ch   = 0;
    e.mods = snapshot_mods();
    e.ts_micros = now_micros();
    g_q.try_push(e);
}

// LL keyboard hook callback
static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam) {
    if (nCode == HC_ACTION) {
        const KBDLLHOOKSTRUCT* p = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
        switch (wParam) {
            case WM_KEYDOWN:
            case WM_SYSKEYDOWN:
                emit_down(p->vkCode, p->scanCode);
                break;
            case WM_KEYUP:
            case WM_SYSKEYUP:
                emit_up(p->vkCode);
                break;
        }
    }
    return CallNextHookEx(nullptr, nCode, wParam, lParam);
}

// Install/uninstall hook
static void SetGlobalHook() {
    g_hook = SetWindowsHookExW(WH_KEYBOARD_LL, LowLevelKeyboardProc, GetModuleHandleW(nullptr), 0);
}

static void UnsetGlobalHook() {
    if (g_hook) { UnhookWindowsHookEx(g_hook); g_hook = nullptr; }
}

// Main
int main() {
    // Start the batcher FIRST so frames have somewhere to go
    g_batcher.Start();

    SetGlobalHook();
    if (!g_hook) {
        // If hook fails, stop batcher and exit
        g_batcher.Stop();
        return 1;
    }

    // Required message loop for WH_KEYBOARD_LL owner thread
    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    UnsetGlobalHook();
    g_batcher.Stop();
    return 0;
}