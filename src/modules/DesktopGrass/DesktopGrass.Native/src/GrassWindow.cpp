// GrassWindow.cpp

#include "GrassWindow.h"

#include <shellscalingapi.h>
#pragma comment(lib, "Shcore.lib")
#pragma comment(lib, "User32.lib")

namespace desktopgrass {

namespace {
constexpr UINT_PTR kProp = 0; // placeholder; we use SetWindowLongPtr(GWLP_USERDATA)
} // anonymous

bool GrassWindow::RegisterWindowClass(HINSTANCE hInst) {
    WNDCLASSEXW wc{};
    wc.cbSize        = sizeof(wc);
    wc.style         = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc   = GrassWindow::WndProc;
    wc.hInstance     = hInst;
    wc.lpszClassName = kWindowClassName;
    wc.hCursor       = LoadCursorW(nullptr, IDC_ARROW);
    wc.hbrBackground = nullptr; // we paint everything; never let GDI clear

    ATOM atom = RegisterClassExW(&wc);
    if (atom == 0) {
        DWORD err = GetLastError();
        if (err != ERROR_CLASS_ALREADY_EXISTS) {
            return false;
        }
    }
    return true;
}

GrassWindow::~GrassWindow() {
    Destroy();
}

bool GrassWindow::Create(HINSTANCE hInst,
                         const RECT& monitorBounds, UINT dpi,
                         uint64_t seed, double density,
                         double swaySpeed, double swayAmplitude)
{
    dpi_     = dpi == 0 ? 96 : dpi;
    seed_    = seed;
    density_ = density;
    monitorBounds_ = monitorBounds;

    // Compute window dims in pixels: full monitor width × (STRIP_HEIGHT +
    // HEADROOM) DIP. Bottom-aligned to the monitor.
    const int monitorW = monitorBounds.right  - monitorBounds.left;
    const int heightPx = static_cast<int>(
        ((STRIP_HEIGHT + HEADROOM) * dpi_ / 96.0) + 0.5);

    screenBounds_.left   = monitorBounds.left;
    screenBounds_.right  = monitorBounds.left + monitorW;
    screenBounds_.bottom = monitorBounds.bottom;
    screenBounds_.top    = monitorBounds.bottom - heightPx;

    const DWORD exStyle =
        WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST |
        WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
    const DWORD style = WS_POPUP;

    hwnd_ = CreateWindowExW(
        exStyle, kWindowClassName, L"Desktop Grass",
        style,
        screenBounds_.left,  screenBounds_.top,
        monitorW,            heightPx,
        nullptr, nullptr, hInst, this);

    if (!hwnd_) {
        return false;
    }

    if (!renderer_.Initialize(hwnd_, monitorW, heightPx, dpi_, seed, density,
                              swaySpeed, swayAmplitude)) {
        DestroyWindow(hwnd_);
        hwnd_ = nullptr;
        return false;
    }

    renderer_.SetWindowOriginScreen(screenBounds_.left, screenBounds_.top);
    return true;
}

void GrassWindow::Show() {
    if (hwnd_) {
        ShowWindow(hwnd_, SW_SHOWNOACTIVATE);
    }
}

void GrassWindow::Destroy() {
    if (hwnd_) {
        DestroyWindow(hwnd_);
        hwnd_ = nullptr;
    }
}

void GrassWindow::RenderFrame(double dt,
                              const InputEvent* events, std::size_t numEvents)
{
    renderer_.RenderFrame(dt, events, numEvents);
}

LRESULT CALLBACK GrassWindow::WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp) {
    GrassWindow* self = nullptr;
    if (msg == WM_NCCREATE) {
        auto* cs = reinterpret_cast<CREATESTRUCTW*>(lp);
        self = reinterpret_cast<GrassWindow*>(cs->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
        if (self) self->hwnd_ = hwnd;
    } else {
        self = reinterpret_cast<GrassWindow*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    }
    if (self) return self->HandleMessage(msg, wp, lp);
    return DefWindowProcW(hwnd, msg, wp, lp);
}

LRESULT GrassWindow::HandleMessage(UINT msg, WPARAM wp, LPARAM lp) {
    switch (msg) {
        case WM_CLOSE:
            // The smoke harness sends WM_CLOSE. Forward to the main thread as
            // a request to terminate the message loop.
            PostQuitMessage(0);
            return 0;

        case WM_DPICHANGED: {
            const UINT newDpi = HIWORD(wp);
            auto* rect = reinterpret_cast<const RECT*>(lp);
            if (rect) {
                SetWindowPos(hwnd_, nullptr,
                             rect->left, rect->top,
                             rect->right - rect->left,
                             rect->bottom - rect->top,
                             SWP_NOZORDER | SWP_NOACTIVATE);
                renderer_.Resize(rect->right - rect->left,
                                 rect->bottom - rect->top, newDpi);
                dpi_ = newDpi;
                screenBounds_ = *rect;
                renderer_.SetWindowOriginScreen(rect->left, rect->top);
                // Mirror the Win2D rebuild: regenerate the blade layout for the
                // new DIP width using the same per-monitor seed so the result is
                // identical to a fresh launch at this DPI. Reuses the stored
                // seed_/density_; sway scales and scene/critter/cut state are
                // preserved inside RegenerateForDpi.
                renderer_.RegenerateForDpi(seed_, density_);
            }
            return 0;
        }

        case WM_DESTROY:
            return 0;

        default:
            return DefWindowProcW(hwnd_, msg, wp, lp);
    }
}

} // namespace desktopgrass
