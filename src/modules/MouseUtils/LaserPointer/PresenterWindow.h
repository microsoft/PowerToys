#pragma once

#include "pch.h"

#include <functional>
#include <string>

#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>

// Mirrors one window into a second, off-screen window with the laser trail composited
// into it.
//
// Per-window screen sharing (Teams, Zoom) captures only the target window's own visual
// tree, so a separate top-level overlay is never included - which is why the laser is
// invisible to remote viewers unless the whole screen is shared. This window is a
// capturable surface that contains both: the target's pixels, taken with
// Windows.Graphics.Capture, and the trail drawn on top.
//
// It lives off the virtual desktop so it does not clutter the presenter's screen. That
// is deliberate and verified: Windows keeps compositing off-screen windows, and both
// Teams and OBS still list them for sharing.
class PresenterWindow
{
public:
    ~PresenterWindow();

    // Only ordinary application windows are worth mirroring. The taskbar, the desktop,
    // cloaked windows and our own windows are rejected, which is how the caller decides
    // to fall back to a plain screen overlay.
    static bool IsPresentableWindow(HWND window) noexcept;

    // Where to send word that the shell asked for this window to close. The module owns
    // the capture session and the overlay state that go with it, so it has to do the
    // tearing down rather than the window quietly destroying itself.
    void SetCloseNotification(HWND window, UINT message) noexcept
    {
        m_closeNotifyWindow = window;
        m_closeNotifyMessage = message;
    }

    bool Start(HINSTANCE instance, HWND target, ID3D11Device* d3dDevice, ID2D1Device1* d2dDevice);

    // Points an already running mirror at a different window. The host window is kept
    // and only resized, so anything already sharing it carries on across the switch -
    // stopping and starting again would pull the shared surface out from under the
    // viewer and force them to pick it a second time.
    bool Retarget(HWND target);

    void Stop();

    bool Active() const noexcept { return m_hwnd != nullptr; }
    HWND Target() const noexcept { return m_target; }

    // Screen rect of the mirrored window, used to translate the trail into its space.
    RECT TargetRect() const noexcept { return m_targetRect; }
    const std::wstring& TargetTitle() const noexcept { return m_targetTitle; }

    // Takes the newest captured frame and composites. drawTrail is handed a device
    // context whose transform already maps screen coordinates onto the mirror, and is
    // clipped to the target's bounds. Returns false once the target has gone away.
    bool Present(const std::function<void(ID2D1DeviceContext*)>& drawTrail);

private:
    bool CreateHostWindow(HINSTANCE instance);
    bool CreateGraphics(ID3D11Device* d3dDevice, ID2D1Device1* d2dDevice);
    bool StartCapture();
    void StopCapture();
    bool ResizeSurfaces(UINT width, UINT height);

    // The swap chain and D2D target only. Split out because retargeting resizes while
    // the capture is torn down, and the frame pool must not be touched then.
    bool ResizeSwapChain(UINT width, UINT height);
    bool EnsureFrameCopy(UINT width, UINT height);
    bool SeedFromPrintWindow();
    void RefreshTitle();
    static LRESULT CALLBACK WndProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam) noexcept;

    static constexpr auto m_className = L"PowerToysLaserPointerPresenter";

    HWND m_hwnd = nullptr;
    HWND m_target = nullptr;
    HWND m_closeNotifyWindow = nullptr;
    UINT m_closeNotifyMessage = 0;
    RECT m_targetRect{};
    UINT m_width = 0;
    UINT m_height = 0;
    std::wstring m_targetTitle;

    winrt::com_ptr<ID3D11Device> m_d3dDevice;
    winrt::com_ptr<ID2D1Device1> m_d2dDevice;
    winrt::com_ptr<ID2D1DeviceContext> m_d2dContext;
    winrt::com_ptr<IDXGISwapChain1> m_swapChain;

    // The captured frame is copied into a texture we own. The frame pool recycles its
    // surfaces the moment a frame is closed, and frames only arrive when the mirrored
    // window actually changes - so without a copy of our own the mirror would be black
    // on every tick that brought no frame.
    winrt::com_ptr<ID3D11Texture2D> m_frameCopy;
    winrt::com_ptr<ID2D1Bitmap1> m_frameBitmap;

    // A window sitting behind others may never repaint, and capture only produces a
    // frame when it does - so until the first one arrives the target gets nudged.
    bool m_haveFirstFrame = false;
    uint64_t m_lastNudgeMs = 0;
    uint64_t m_captureStartedMs = 0;
    bool m_seedAttempted = false;

    winrt::Windows::Graphics::Capture::GraphicsCaptureItem m_captureItem{ nullptr };
    winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool m_framePool{ nullptr };
    winrt::Windows::Graphics::Capture::GraphicsCaptureSession m_session{ nullptr };
    winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice m_captureDevice{ nullptr };
};
