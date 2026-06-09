// GrassWindow.h
//
// One HWND + one Renderer per monitor. Layered, click-through, topmost,
// no-activate, tool-window — see WS_EX flags listed in the plan and asserted
// by tests/smoke.

#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <memory>

#include "Renderer.h"
#include "MouseHook.h"

namespace desktopgrass {

class GrassWindow {
public:
    static constexpr const wchar_t* kWindowClassName = L"DesktopGrass.Native.Window";
    static constexpr UINT           kWmAppQuit       = WM_APP + 1;

    static bool RegisterWindowClass(HINSTANCE hInst);

    GrassWindow() = default;
    ~GrassWindow();

    GrassWindow(const GrassWindow&)            = delete;
    GrassWindow& operator=(const GrassWindow&) = delete;

    // Creates the HWND, attaches a Renderer, generates blades using `seed`.
    bool Create(HINSTANCE hInst,
                const RECT& monitorBounds, UINT dpi,
                uint64_t seed, double density,
                double swaySpeed = 1.0, double swayAmplitude = 1.0);

    void Show();
    void Destroy();
    void RenderFrame(double dt,
                     const InputEvent* events, std::size_t numEvents);

    HWND      GetHwnd()  const { return hwnd_; }
    Renderer& GetRenderer()     { return renderer_; }
    const RECT& GetScreenBounds() const { return screenBounds_; }
    const RECT& GetMonitorBounds() const { return monitorBounds_; }

private:
    static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp);
    LRESULT HandleMessage(UINT msg, WPARAM wp, LPARAM lp);

    HWND       hwnd_ = nullptr;
    Renderer   renderer_;
    RECT       screenBounds_{}; // window screen-rect (left, top, right, bottom)
    RECT       monitorBounds_{}; // monitor work-area rect used for persistence keys
    UINT       dpi_   = 96;
    uint64_t   seed_  = 0;
    double     density_ = 1.0;
};

} // namespace desktopgrass
