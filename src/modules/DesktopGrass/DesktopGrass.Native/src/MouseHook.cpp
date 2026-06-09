// MouseHook.cpp

#include "MouseHook.h"

#include <atomic>
#include <chrono>

namespace desktopgrass {

namespace {

std::atomic<MouseEventQueue*> g_queue{nullptr};
HHOOK                         g_hook = nullptr;
LARGE_INTEGER                 g_qpcFreq{};
LARGE_INTEGER                 g_qpcStart{};

double now_seconds() noexcept {
    LARGE_INTEGER c;
    QueryPerformanceCounter(&c);
    return static_cast<double>(c.QuadPart - g_qpcStart.QuadPart) /
           static_cast<double>(g_qpcFreq.QuadPart);
}

LRESULT CALLBACK LowLevelMouseProc(int nCode, WPARAM wParam, LPARAM lParam) {
    // Per spec: always pass the event through. Never consume.
    if (nCode == HC_ACTION) {
        MouseEventQueue* q = g_queue.load(std::memory_order_acquire);
        if (q) {
            const MSLLHOOKSTRUCT* m = reinterpret_cast<const MSLLHOOKSTRUCT*>(lParam);
            RawMouseEvent ev{};
            ev.timeSeconds = now_seconds();
            ev.screenX     = m->pt.x;
            ev.screenY     = m->pt.y;

            switch (wParam) {
                case WM_MOUSEMOVE:
                    ev.type = EventType::Move;
                    q->push(ev);
                    break;
                case WM_LBUTTONDOWN:
                    ev.type = EventType::Click;
                    q->push(ev);
                    break;
                default:
                    break;
            }
        }
    }
    return CallNextHookEx(nullptr, nCode, wParam, lParam);
}

} // anonymous

bool install_mouse_hook(MouseEventQueue* queue) noexcept {
    if (g_hook) return false;
    QueryPerformanceFrequency(&g_qpcFreq);
    QueryPerformanceCounter(&g_qpcStart);
    g_queue.store(queue, std::memory_order_release);
    g_hook = SetWindowsHookExW(WH_MOUSE_LL, LowLevelMouseProc,
                               GetModuleHandleW(nullptr), 0);
    return g_hook != nullptr;
}

void uninstall_mouse_hook() noexcept {
    if (g_hook) {
        UnhookWindowsHookEx(g_hook);
        g_hook = nullptr;
    }
    g_queue.store(nullptr, std::memory_order_release);
}

} // namespace desktopgrass
