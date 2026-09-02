// LaserPointer.cpp : Overlay, input handling and rendering for the Laser Pointer module.
//
// The trail model itself lives in LaserStroke.{h,cpp}; this file owns the transparent
// click-through overlay it is drawn on, the low level mouse hook that feeds it, and the
// render loop.

#include "pch.h"
#include "LaserPointer.h"
#include "LaserStroke.h"
#include "trace.h"
#include "PresenterWindow.h"

#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <unordered_map>
#include <vector>

namespace
{
    // Windows tags mouse messages that were promoted from a pen or touch digitizer with
    // this signature in dwExtraInfo. Bit 0x80 distinguishes touch from pen.
    constexpr ULONG_PTR PEN_OR_TOUCH_SIGNATURE = 0xFF515700;
    constexpr ULONG_PTR PEN_OR_TOUCH_SIGNATURE_MASK = 0xFFFFFF00;
    constexpr ULONG_PTR TOUCH_FLAG = 0x80;

    constexpr UINT_PTR RENDER_TIMER_ID = 201;
    // WM_TIMER is coalesced to the system tick (~15.6 ms), which lands close enough to
    // 60 Hz. The timer - not vsync - is what paces us: the low level mouse hook is
    // dispatched on this same thread, so Present must never block on the display.
    constexpr UINT RENDER_TIMER_INTERVAL_MS = 16;

    // The trail is a solid stroke with an optional soft halo behind it. An earlier
    // version also painted a lightened core down the centre, but at realistic widths
    // that reads as a pale gap splitting the stroke into two lines rather than as a lit
    // beam, so the body is now drawn as a single solid fill.
    constexpr float GLOW_WIDTH_SCALE = 2.4f;
    constexpr float GLOW_ALPHA_SCALE = 0.22f;

    // wParam values for WM_WAKE_RENDER_LOOP.
    constexpr WPARAM WAKE_END = 0;
    constexpr WPARAM WAKE_BEGIN = 1;
    constexpr WPARAM WAKE_RESUME = 2;

    // How long the name of the mirrored window stays on screen. The border itself has
    // no timeout: like the one Teams draws, it stays for as long as sharing is live.
    constexpr uint64_t PRESENTER_NOTICE_MS = 2500;

    // Teams marks a shared window with a red outline; this matches it so the two do not
    // look like different things happening.
    constexpr float PRESENTER_BORDER_R = 0.769f; // #C4314B
    constexpr float PRESENTER_BORDER_G = 0.192f;
    constexpr float PRESENTER_BORDER_B = 0.294f;
    constexpr float PRESENTER_BORDER_WIDTH = 5.0f;

    constexpr bool IsFromPen(ULONG_PTR extraInfo) noexcept
    {
        return ((extraInfo & PEN_OR_TOUCH_SIGNATURE_MASK) == PEN_OR_TOUCH_SIGNATURE) &&
               ((extraInfo & TOUCH_FLAG) == 0);
    }

    // Upper bound on hook samples buffered between two render ticks. A 1000 Hz mouse
    // produces ~16 per tick; this only bites if the render thread stalls badly, and
    // dropping the oldest is the right answer there anyway.
    constexpr size_t MAX_PENDING_POINTS = 512;

    // The sample rate the decay length setting was calibrated against, expressed as
    // samples per render tick. Sampling used to happen once per tick, so one.
    constexpr float REFERENCE_SAMPLES_PER_TICK = 1.0f;

    // How long without pen input before the stroke is considered finished.
    constexpr uint64_t PEN_IDLE_TIMEOUT_MS = 200;

    // How long after the last digitizer report the pen still counts as present. Reports
    // stop the moment it leaves range, so this only has to bridge the gap between two
    // consecutive ones - long enough to survive a stutter, short enough that the overlay
    // stops capturing promptly once the pen is put down.
    constexpr uint64_t PEN_RANGE_TIMEOUT_MS = 350;

    // How far from the pen's last reported position the overlay still answers a hit test.
    // Only has to cover how far the pen can travel between two digitizer reports, which
    // at their rate is a few pixels; the rest is margin.
    constexpr LONG PEN_CAPTURE_RADIUS_PX = 64;

    // Maps a low level mouse message onto the button it belongs to. Returns false for
    // anything that is not a button transition (moves, wheel).
    bool ClassifyButton(WPARAM message, const MSLLHOOKSTRUCT* data, LaserPointerButton& button, bool& isDown) noexcept
    {
        switch (message)
        {
        case WM_LBUTTONDOWN: button = LaserPointerButton::Left; isDown = true; return true;
        case WM_LBUTTONUP: button = LaserPointerButton::Left; isDown = false; return true;
        case WM_RBUTTONDOWN: button = LaserPointerButton::Right; isDown = true; return true;
        case WM_RBUTTONUP: button = LaserPointerButton::Right; isDown = false; return true;
        case WM_MBUTTONDOWN: button = LaserPointerButton::Middle; isDown = true; return true;
        case WM_MBUTTONUP: button = LaserPointerButton::Middle; isDown = false; return true;
        case WM_XBUTTONDOWN:
        case WM_XBUTTONUP:
            button = (HIWORD(data->mouseData) == XBUTTON1) ? LaserPointerButton::X1 : LaserPointerButton::X2;
            isDown = (message == WM_XBUTTONDOWN);
            return true;
        default:
            return false;
        }
    }

    // Left and Right are never swallowed: doing so would make the machine unusable for
    // as long as the module is enabled.
    constexpr bool CanSuppress(LaserPointerButton button) noexcept
    {
        return button == LaserPointerButton::Middle ||
               button == LaserPointerButton::X1 ||
               button == LaserPointerButton::X2;
    }
}

struct LaserPointerOverlay
{
    static LaserPointerOverlay* instance;

    bool MyRegisterClass(HINSTANCE hInstance);
    void Terminate();
    void SwitchActivationMode();
    void SwitchPenActivationMode();
    void SwitchPresenterMode();
    void ApplySettings(LaserPointerSettings settings);
    void QueueSettings(LaserPointerSettings settings);

private:
    static LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept;
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam) noexcept;

    LRESULT HandleMouseInput(WPARAM message, const MSLLHOOKSTRUCT* data) noexcept;

    // Raw input path for the pen. Registered while the pen is armed; sees the digitizer
    // directly, so it keeps working over content that consumes pointer input.
    void RegisterPenRawInput();
    void UnregisterPenRawInput();
    void HandleRawInput(HRAWINPUT handle) noexcept;
    bool ReadPenReport(RAWINPUT* input, POINT& screenPoint, bool& tipDown) noexcept;


    void ArmMouse();
    void TogglePresenter();
    void DisarmMouse();
    void ArmPen();
    void DisarmPen();
    void Disarm();
    void ReleaseIfIdle();
    bool HookNeeded() const noexcept;
    void UpdateHook();
    void ProcessPendingSettings();
    void OnRenderTick();
    void StartRenderLoop();
    void StopRenderLoop();
    void BeginDrawing();
    void EndDrawing();

    bool CreateGraphics();
    void ReleaseGraphics();
    bool EnsureOverlayGeometry();
    void Render(uint64_t nowMs);
    void RenderStroke(ID2D1DeviceContext* context, const LaserPointerCore::LaserStroke& stroke, uint64_t nowMs);
    void FillOutline(ID2D1DeviceContext* context, const LaserPointerCore::LaserStroke& stroke, uint64_t nowMs, float widthScale, const D2D1_COLOR_F& color);
    void DrawTrails(ID2D1DeviceContext* context, uint64_t nowMs);
    void DrawPresenterNotice(uint64_t nowMs);

    // The overlay spans the whole desktop. While it is only showing the sharing border
    // it is cut down to that border, so everything else stays clickable.
    void UpdateOverlayRegion(bool trailVisible);

    // True while the digitizer is actively reporting and the pen is armed.
    bool PenPresent(uint64_t nowMs) const noexcept;

    // Makes the overlay hit-testable while the pen is present, so pen input lands here
    // instead of the window underneath.
    void UpdatePenCapture(uint64_t nowMs);

    // Whether a hit test at this screen point should be treated as the pen.
    bool NearLastPenPoint(POINT screenPoint) const noexcept;

    // While only the border is showing the overlay sits directly above the mirrored
    // window rather than above everything, so other windows can cover it.
    void UpdateOverlayZOrder(bool trailVisible, uint64_t nowMs);
    void ShowOverlay();
    void HideOverlay();

    float DpiScaleForPoint(POINT screenPoint) const;

    static constexpr auto m_className = L"PowerToysLaserPointer";
    static constexpr auto m_windowTitle = L"PowerToys Laser Pointer";
    static constexpr DWORD WM_SWITCH_ACTIVATION_MODE = WM_APP;
    static constexpr DWORD WM_APPLY_SETTINGS = WM_APP + 1;
    static constexpr DWORD WM_WAKE_RENDER_LOOP = WM_APP + 2;
    static constexpr DWORD WM_TERMINATE_OVERLAY = WM_APP + 3;
    static constexpr DWORD WM_SWITCH_PEN_ACTIVATION_MODE = WM_APP + 4;
    static constexpr DWORD WM_SWITCH_PRESENTER_MODE = WM_APP + 5;

    HINSTANCE m_hinstance = nullptr;
    HWND m_hwndOwner = nullptr;
    HWND m_hwnd = nullptr;
    HHOOK m_mouseHook = nullptr;

    // Direct3D / Direct2D / DirectComposition. The overlay is a NOREDIRECTIONBITMAP
    // window with a composition swap chain so we get per-pixel alpha and hardware
    // accelerated per-frame path filling.
    winrt::com_ptr<ID2D1Factory2> m_d2dFactory;
    winrt::com_ptr<ID3D11Device> m_d3dDevice;
    winrt::com_ptr<IDXGIDevice> m_dxgiDevice;
    winrt::com_ptr<IDXGIFactory2> m_dxgiFactory;
    winrt::com_ptr<ID2D1Device1> m_d2dDevice;
    winrt::com_ptr<ID2D1DeviceContext> m_d2dContext;
    winrt::com_ptr<IDXGISwapChain1> m_swapChain;
    winrt::com_ptr<IDCompositionDevice> m_compositionDevice;
    winrt::com_ptr<IDCompositionTarget> m_compositionTarget;
    winrt::com_ptr<IDCompositionVisual> m_compositionVisual;
    winrt::com_ptr<ID2D1SolidColorBrush> m_brush;

    RECT m_overlayBounds{};
    UINT m_overlayWidth = 0;
    UINT m_overlayHeight = 0;

    LaserPointerCore::LaserStroke m_stroke;
    // Trails that were finished while still visible. Starting a new stroke must not
    // erase the one that is still decaying, so the old one is parked here until it
    // fades out - the same reason excalidraw keeps a list of past trails.
    std::vector<LaserPointerCore::LaserStroke> m_pastStrokes;
    std::vector<LaserPointerCore::Vec2> m_outline;

    LaserPointerSettings m_settings{};
    LaserPointerSettings m_pendingSettings{};
    SRWLOCK m_settingsLock = SRWLOCK_INIT;
    SRWLOCK m_hwndLock = SRWLOCK_INIT;
    bool m_settingsPending = false;

    // Set by the hook thread (which is our own message thread) and read by the render
    // tick.
    POINT m_latestPosition{};
    bool m_hasLatestPosition = false;
    bool m_activationButtonHeld = false;
    bool m_alwaysOnButtonHeld = false;
    bool m_penContact = false;

    // Every position the hook saw since the last render tick. The hook fires at the
    // mouse's polling rate (125 Hz to 1000 Hz), far above the ~62 Hz tick, so collapsing
    // these to the most recent one threw away most of the trail's detail.
    struct PendingPoint
    {
        POINT pt{};
        uint64_t t = 0;
    };
    std::vector<PendingPoint> m_pendingPoints;

    // Smoothed samples-per-tick, used to keep the trail the same visible length no
    // matter how fast the mouse reports. See OnRenderTick.
    float m_samplesPerTick = REFERENCE_SAMPLES_PER_TICK;

    // Diagnostic flag, reset on each pen arm. See HandleMouseInput.
    // Pen presence is time-based; see HandleMouseInput.
    uint64_t m_lastPenInputMs = 0;
    UINT m_lastPenMessage = 0;
    int m_penMessagesLogged = 0;
    bool m_penAwaitingFirstSample = false;
    bool m_penCapturing = false;
    POINT m_lastPenPoint{};
    bool m_penStrokeOpen = false;

    // Mirrors the window that was in front when the laser was armed, so per-window
    // screen sharing can see the trail.
    PresenterWindow m_presenter;

    // Short-lived confirmation of which window is being mirrored.
    uint64_t m_presenterNoticeUntilMs = 0;
    RECT m_presenterNoticeRect{};
    std::wstring m_presenterNoticeText;
    bool m_overlayRegionIsFrame = false;
    // What the frame region was last built from, so it can follow a window that moves.
    RECT m_overlayRegionRect{};
    bool m_overlayBelowTopmost = false;
    uint64_t m_lastZOrderMs = 0;
    HWND m_overlayZOrderAfter = nullptr;

    winrt::com_ptr<IDWriteFactory> m_dwriteFactory;
    winrt::com_ptr<IDWriteTextFormat> m_noticeFormat;

    // Raw input state.
    bool m_rawInputRegistered = false;
    std::vector<BYTE> m_rawInputBuffer;
    int m_rawInputLogCount = 0;

    // Per-device HID description, cached because fetching preparsed data on every
    // report would dominate the cost of reading one.
    struct PenDevice
    {
        std::vector<BYTE> preparsed;
        USAGE usagePage = 0;
        USAGE usage = 0;
        LONG minX = 0, maxX = 0, minY = 0, maxY = 0;
        bool haveX = false, haveY = false;
        bool usable = false;
    };
    std::unordered_map<HANDLE, PenDevice> m_penDevices;

    // The two shortcuts arm independently, so the pen can be used without the mouse.
    bool m_mouseArmed = false;
    bool m_penArmed = false;
    bool m_drawing = false;
    bool m_renderLoopRunning = false;
    bool m_overlayVisible = false;
};

LaserPointerOverlay* LaserPointerOverlay::instance = nullptr;

#pragma region Graphics

bool LaserPointerOverlay::CreateGraphics()
{
    try
    {
        const D2D1_FACTORY_OPTIONS factoryOptions = { D2D1_DEBUG_LEVEL_NONE };
        winrt::check_hresult(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, factoryOptions, m_d2dFactory.put()));

        // Only used for the short confirmation of which window is being mirrored.
        winrt::check_hresult(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED,
                                                 __uuidof(IDWriteFactory),
                                                 reinterpret_cast<IUnknown**>(m_dwriteFactory.put())));
        winrt::check_hresult(m_dwriteFactory->CreateTextFormat(L"Segoe UI",
                                                              nullptr,
                                                              DWRITE_FONT_WEIGHT_SEMI_BOLD,
                                                              DWRITE_FONT_STYLE_NORMAL,
                                                              DWRITE_FONT_STRETCH_NORMAL,
                                                              20.0f,
                                                              L"",
                                                              m_noticeFormat.put()));

        UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
        HRESULT hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags, nullptr, 0, D3D11_SDK_VERSION, m_d3dDevice.put(), nullptr, nullptr);
        if (hr == DXGI_ERROR_UNSUPPORTED)
        {
            hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, flags, nullptr, 0, D3D11_SDK_VERSION, m_d3dDevice.put(), nullptr, nullptr);
        }
        winrt::check_hresult(hr);

        m_dxgiDevice = m_d3dDevice.as<IDXGIDevice>();

        winrt::com_ptr<IDXGIAdapter> adapter;
        winrt::check_hresult(m_dxgiDevice->GetParent(winrt::guid_of<IDXGIAdapter>(), adapter.put_void()));
        winrt::check_hresult(adapter->GetParent(winrt::guid_of<IDXGIFactory2>(), m_dxgiFactory.put_void()));

        winrt::check_hresult(m_d2dFactory->CreateDevice(m_dxgiDevice.get(), m_d2dDevice.put()));
        winrt::check_hresult(m_d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, m_d2dContext.put()));
        // Work in raw pixels: the overlay spans the virtual screen, which can mix DPIs,
        // so stroke widths are scaled per monitor instead of by a render target DPI.
        m_d2dContext->SetDpi(96.0f, 96.0f);

        winrt::check_hresult(DCompositionCreateDevice(m_dxgiDevice.get(), winrt::guid_of<IDCompositionDevice>(), m_compositionDevice.put_void()));

        winrt::check_hresult(m_d2dContext->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White), m_brush.put()));

        return true;
    }
    catch (const winrt::hresult_error& e)
    {
        Logger::error(L"Failed to initialize Laser Pointer graphics: {}", e.message());
        ReleaseGraphics();
        return false;
    }
    catch (...)
    {
        Logger::error("Failed to initialize Laser Pointer graphics.");
        ReleaseGraphics();
        return false;
    }
}

void LaserPointerOverlay::ReleaseGraphics()
{
    m_brush = nullptr;
    m_compositionVisual = nullptr;
    m_compositionTarget = nullptr;
    m_swapChain = nullptr;
    if (m_d2dContext)
    {
        m_d2dContext->SetTarget(nullptr);
    }
    m_d2dContext = nullptr;
    m_compositionDevice = nullptr;
    m_d2dDevice = nullptr;
    m_dxgiFactory = nullptr;
    m_dxgiDevice = nullptr;
    m_d3dDevice = nullptr;
    m_d2dFactory = nullptr;

    m_overlayWidth = 0;
    m_overlayHeight = 0;
}

// Sizes the window and the swap chain to the current virtual screen. Called on every
// activation and whenever the display layout changes.
bool LaserPointerOverlay::EnsureOverlayGeometry()
{
    if (!m_d2dContext)
    {
        if (!CreateGraphics())
        {
            return false;
        }
    }

    RECT bounds{};
    // Inset by a pixel: a transparent window covering the whole virtual screen exactly
    // makes Windows glitch the taskbar transparency (same workaround as Mouse
    // Highlighter).
    bounds.left = GetSystemMetrics(SM_XVIRTUALSCREEN) + 1;
    bounds.top = GetSystemMetrics(SM_YVIRTUALSCREEN) + 1;
    bounds.right = bounds.left + GetSystemMetrics(SM_CXVIRTUALSCREEN) - 2;
    bounds.bottom = bounds.top + GetSystemMetrics(SM_CYVIRTUALSCREEN) - 2;

    const UINT width = static_cast<UINT>((std::max)(1L, bounds.right - bounds.left));
    const UINT height = static_cast<UINT>((std::max)(1L, bounds.bottom - bounds.top));

    // The origin matters as well as the size: rearranging monitors can move the virtual
    // screen's top left corner without changing its overall extent.
    const bool geometryChanged = (width != m_overlayWidth) ||
                                 (height != m_overlayHeight) ||
                                 (bounds.left != m_overlayBounds.left) ||
                                 (bounds.top != m_overlayBounds.top);
    m_overlayBounds = bounds;

    if (!geometryChanged && m_swapChain)
    {
        return true;
    }

    try
    {
        SetWindowPos(m_hwnd, HWND_TOPMOST, bounds.left, bounds.top, static_cast<int>(width), static_cast<int>(height), SWP_NOACTIVATE | SWP_NOREDRAW);

        if (m_swapChain)
        {
            m_d2dContext->SetTarget(nullptr);
            winrt::check_hresult(m_swapChain->ResizeBuffers(0, width, height, DXGI_FORMAT_UNKNOWN, 0));
        }
        else
        {
            const DXGI_SWAP_CHAIN_DESC1 desc = {
                .Width = width,
                .Height = height,
                .Format = DXGI_FORMAT_B8G8R8A8_UNORM,
                .SampleDesc = { .Count = 1, .Quality = 0 },
                .BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
                .BufferCount = 2,
                .Scaling = DXGI_SCALING_STRETCH,
                .SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD,
                .AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED,
            };

            winrt::check_hresult(m_dxgiFactory->CreateSwapChainForComposition(m_d3dDevice.get(), &desc, nullptr, m_swapChain.put()));

            winrt::check_hresult(m_compositionDevice->CreateTargetForHwnd(m_hwnd, true, m_compositionTarget.put()));
            winrt::check_hresult(m_compositionDevice->CreateVisual(m_compositionVisual.put()));
            winrt::check_hresult(m_compositionVisual->SetContent(m_swapChain.get()));
            winrt::check_hresult(m_compositionTarget->SetRoot(m_compositionVisual.get()));
            winrt::check_hresult(m_compositionDevice->Commit());
        }

        winrt::com_ptr<IDXGISurface> surface;
        winrt::check_hresult(m_swapChain->GetBuffer(0, winrt::guid_of<IDXGISurface>(), surface.put_void()));

        const D2D1_BITMAP_PROPERTIES1 properties = {
            .pixelFormat = { .format = DXGI_FORMAT_B8G8R8A8_UNORM, .alphaMode = D2D1_ALPHA_MODE_PREMULTIPLIED },
            .dpiX = 96.0f,
            .dpiY = 96.0f,
            .bitmapOptions = D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
        };

        winrt::com_ptr<ID2D1Bitmap1> bitmap;
        winrt::check_hresult(m_d2dContext->CreateBitmapFromDxgiSurface(surface.get(), properties, bitmap.put()));
        m_d2dContext->SetTarget(bitmap.get());

        m_overlayWidth = width;
        m_overlayHeight = height;
        return true;
    }
    catch (const winrt::hresult_error& e)
    {
        Logger::error(L"Failed to size the Laser Pointer overlay: {}", e.message());
        ReleaseGraphics();
        return false;
    }
    catch (...)
    {
        Logger::error("Failed to size the Laser Pointer overlay.");
        ReleaseGraphics();
        return false;
    }
}

void LaserPointerOverlay::FillOutline(ID2D1DeviceContext* context, const LaserPointerCore::LaserStroke& stroke, uint64_t nowMs, float widthScale, const D2D1_COLOR_F& color)
{
    if (stroke.BuildOutline(nowMs, m_outline, widthScale) < 3)
    {
        return;
    }

    winrt::com_ptr<ID2D1PathGeometry> geometry;
    if (FAILED(m_d2dFactory->CreatePathGeometry(geometry.put())))
    {
        return;
    }

    winrt::com_ptr<ID2D1GeometrySink> sink;
    if (FAILED(geometry->Open(sink.put())))
    {
        return;
    }

    sink->SetFillMode(D2D1_FILL_MODE_WINDING);
    sink->BeginFigure(D2D1::Point2F(m_outline[0].x, m_outline[0].y), D2D1_FIGURE_BEGIN_FILLED);
    for (size_t i = 1; i < m_outline.size(); ++i)
    {
        sink->AddLine(D2D1::Point2F(m_outline[i].x, m_outline[i].y));
    }
    sink->EndFigure(D2D1_FIGURE_END_CLOSED);

    if (FAILED(sink->Close()))
    {
        return;
    }

    winrt::com_ptr<ID2D1SolidColorBrush> brush;
    if (FAILED(context->CreateSolidColorBrush(color, brush.put())))
    {
        return;
    }

    context->FillGeometry(geometry.get(), brush.get());
}

// Draws one trail: an optional soft halo, then the solid body. Both are filled outlines
// built from the same centerline rather than overlapping stamps, so a semi transparent
// laser colour keeps a single, even alpha instead of darkening wherever the stroke
// overlaps itself.
void LaserPointerOverlay::RenderStroke(ID2D1DeviceContext* context, const LaserPointerCore::LaserStroke& stroke, uint64_t nowMs)
{
    const auto& c = m_settings.laserColor;
    const float r = c.R / 255.0f;
    const float g = c.G / 255.0f;
    const float b = c.B / 255.0f;
    const float a = c.A / 255.0f;

    if (m_settings.glowEnabled)
    {
        FillOutline(context, stroke, nowMs, GLOW_WIDTH_SCALE, D2D1::ColorF(r, g, b, a * GLOW_ALPHA_SCALE));
    }

    FillOutline(context, stroke, nowMs, 1.0f, D2D1::ColorF(r, g, b, a));
}

void LaserPointerOverlay::DrawTrails(ID2D1DeviceContext* context, uint64_t nowMs)
{
    for (const auto& past : m_pastStrokes)
    {
        RenderStroke(context, past, nowMs);
    }

    RenderStroke(context, m_stroke, nowMs);
}

// The pen is the only thing that can be at the pen's position, so the hit-test point is
// what separates it from the mouse. Capturing the whole screen while the pen was in
// range worked, but it also swallowed mouse clicks anywhere on it; confining that to a
// small patch around the pen leaves the rest of the screen click-through as usual.
bool LaserPointerOverlay::NearLastPenPoint(POINT screenPoint) const noexcept
{
    return std::abs(screenPoint.x - m_lastPenPoint.x) <= PEN_CAPTURE_RADIUS_PX &&
           std::abs(screenPoint.y - m_lastPenPoint.y) <= PEN_CAPTURE_RADIUS_PX;
}

bool LaserPointerOverlay::PenPresent(uint64_t nowMs) const noexcept
{
    return m_penArmed && m_lastPenInputMs != 0 && (nowMs - m_lastPenInputMs) < PEN_RANGE_TIMEOUT_MS;
}

// Suppressing pen input through the mouse hook only works where Windows promotes it to
// mouse messages. Over the taskbar and anything using DirectManipulation the app claims
// the pointer stream first, so promotion is delayed or never happens and the first
// contact scrolls the app instead of drawing - intermittently, depending on whether the
// app decided to take the gesture.
//
// Standing in front of it is the reliable answer: while the pen is armed and in range,
// the overlay stops being click-through, so contact hit-tests to this window and the app
// below never sees it. This needed a trustworthy "is that a pen?" signal, which is what
// failed last time - GetCurrentInputMessageSource reports IMDT_UNAVAILABLE on real
// hardware. The raw digitizer reports supply it directly, and they arrive during hover,
// before the first contact. Outside that window the overlay is click-through as before,
// so the mouse is untouched, and on a machine with no digitizer this never engages.
void LaserPointerOverlay::UpdatePenCapture(uint64_t nowMs)
{
    const bool capture = PenPresent(nowMs);
    if (capture == m_penCapturing || m_hwnd == nullptr)
    {
        return;
    }

    m_penCapturing = capture;

    // WM_NCHITTEST alone is not enough: a WS_EX_TRANSPARENT window is passed over before
    // it is ever asked, so the style has to come off for the duration.
    const LONG_PTR exStyle = GetWindowLongPtr(m_hwnd, GWL_EXSTYLE);
    SetWindowLongPtr(m_hwnd, GWL_EXSTYLE, capture ? (exStyle & ~WS_EX_TRANSPARENT) : (exStyle | WS_EX_TRANSPARENT));
}

// The border belongs to the window being shared, so it should be covered when that
// window is. Sitting at the top of the z-order made it hover over everything, which
// reads as a bug rather than an indicator. Only the trail needs to be above all windows.
void LaserPointerOverlay::UpdateOverlayZOrder(bool trailVisible, uint64_t nowMs)
{
    const bool borderOnly = m_presenter.Active() && !trailVisible;

    if (!borderOnly)
    {
        if (m_overlayBelowTopmost)
        {
            SetWindowPos(m_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            m_overlayBelowTopmost = false;
    m_overlayZOrderAfter = nullptr;
            m_overlayZOrderAfter = nullptr;
        }
        return;
    }

    // Checked periodically rather than every frame: the mirrored window can be raised or
    // lowered by the user at any time, but polling the z-order on every tick is wasteful.
    constexpr uint64_t Z_ORDER_REFRESH_MS = 400;
    if (m_overlayBelowTopmost && (nowMs - m_lastZOrderMs) < Z_ORDER_REFRESH_MS)
    {
        return;
    }

    m_lastZOrderMs = nowMs;

    // SetWindowPos places the window *after* the one it is given, so passing the target
    // would bury the border behind it. What is wanted is the slot the target itself
    // occupies, which means inserting after whatever currently sits above it.
    HWND above = GetWindow(m_presenter.Target(), GW_HWNDPREV);
    while (above != nullptr && (above == m_hwnd || above == m_hwndOwner))
    {
        above = GetWindow(above, GW_HWNDPREV);
    }

    // Nothing to do when the overlay is already in the right slot. Re-issuing the same
    // SetWindowPos on a timer makes the border blink for no reason.
    if (m_overlayBelowTopmost && above == m_overlayZOrderAfter)
    {
        return;
    }

    // Topmost has to be dropped first, or inserting after an ordinary window still
    // leaves this in the topmost band.
    if (!m_overlayBelowTopmost)
    {
        SetWindowPos(m_hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        m_overlayBelowTopmost = true;
    }

    // No window above the target means it is already at the front, so the overlay goes to
    // the front of the ordinary band - still below anything genuinely topmost.
    SetWindowPos(m_hwnd, above != nullptr ? above : HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    m_overlayZOrderAfter = above;
}

// A full-screen window is not click-through in practice, even with WS_EX_TRANSPARENT and
// HTTRANSPARENT: while it was only visible for the instant a trail was being drawn that
// went unnoticed, but keeping it up for a whole sharing session swallows every click.
// Restricting the window to the border itself means there is physically nothing over the
// rest of the screen to intercept anything.
void LaserPointerOverlay::UpdateOverlayRegion(bool trailVisible)
{
    const bool wantFrame = m_presenter.Active() && !trailVisible;

    if (!wantFrame)
    {
        if (m_overlayRegionIsFrame)
        {
            // The trail can be drawn anywhere, so the whole surface is needed again.
            SetWindowRgn(m_hwnd, nullptr, FALSE);
            m_overlayRegionIsFrame = false;
        }
        return;
    }

    // Window-relative, since the overlay is positioned across the virtual desktop.
    const RECT target = m_presenter.TargetRect();

    // Rebuilt whenever the shared window moves or resizes: the border already follows it
    // every frame, and a stale region would leave the clickable hole behind.
    if (m_overlayRegionIsFrame &&
        target.left == m_overlayRegionRect.left && target.top == m_overlayRegionRect.top &&
        target.right == m_overlayRegionRect.right && target.bottom == m_overlayRegionRect.bottom)
    {
        return;
    }

    const int width = static_cast<int>(PRESENTER_BORDER_WIDTH) + 1;
    const int left = target.left - m_overlayBounds.left;
    const int top = target.top - m_overlayBounds.top;
    const int right = target.right - m_overlayBounds.left;
    const int bottom = target.bottom - m_overlayBounds.top;

    const HRGN frame = CreateRectRgn(left, top, right, bottom);
    const HRGN hole = CreateRectRgn(left + width, top + width, right - width, bottom - width);
    CombineRgn(frame, frame, hole, RGN_DIFF);
    DeleteObject(hole);

    // SetWindowRgn takes ownership of the region on success. Redraw is left to the
    // render loop: asking the window manager to repaint on every region change made the
    // border blink.
    SetWindowRgn(m_hwnd, frame, FALSE);
    m_overlayRegionIsFrame = true;
    m_overlayRegionRect = target;
}

// Marks the window being mirrored, the way a conferencing app marks the window it is
// sharing: a border for as long as sharing lasts, plus its name for the first moments so
// a wrong pick is obvious immediately rather than being discovered later.
//
// The border is drawn on the overlay, not into the mirror, so it stays local to this
// machine and never appears in what remote viewers see.
void LaserPointerOverlay::DrawPresenterNotice(uint64_t nowMs)
{
    if (!m_presenter.Active() || !m_brush)
    {
        return;
    }

    // Read the rect every frame: the mirrored window can be moved or resized while it is
    // being shared, and a stale outline would be worse than none.
    const RECT target = m_presenter.TargetRect();
    const auto rect = D2D1::RectF(static_cast<float>(target.left) + PRESENTER_BORDER_WIDTH * 0.5f,
                                  static_cast<float>(target.top) + PRESENTER_BORDER_WIDTH * 0.5f,
                                  static_cast<float>(target.right) - PRESENTER_BORDER_WIDTH * 0.5f,
                                  static_cast<float>(target.bottom) - PRESENTER_BORDER_WIDTH * 0.5f);

    m_brush->SetColor(D2D1::ColorF(PRESENTER_BORDER_R, PRESENTER_BORDER_G, PRESENTER_BORDER_B, 1.0f));
    m_d2dContext->DrawRectangle(rect, m_brush.get(), PRESENTER_BORDER_WIDTH);

    if (nowMs >= m_presenterNoticeUntilMs || !m_noticeFormat)
    {
        return;
    }

    // The name fades out over its last third so it does not simply vanish.
    const uint64_t remaining = m_presenterNoticeUntilMs - nowMs;
    const float fade = (std::min)(1.0f, static_cast<float>(remaining) / (PRESENTER_NOTICE_MS / 3.0f));

    const std::wstring label = L"Capturing: " + m_presenterNoticeText;
    const auto labelRect = D2D1::RectF(rect.left + 12.0f, rect.top + 10.0f, rect.right - 12.0f, rect.top + 44.0f);

    m_brush->SetColor(D2D1::ColorF(0.0f, 0.0f, 0.0f, 0.55f * fade));
    m_d2dContext->FillRectangle(D2D1::RectF(labelRect.left - 8.0f, labelRect.top - 4.0f, labelRect.right, labelRect.bottom + 4.0f), m_brush.get());

    m_brush->SetColor(D2D1::ColorF(1.0f, 1.0f, 1.0f, 0.95f * fade));
    m_d2dContext->DrawTextW(label.c_str(),
                            static_cast<UINT32>(label.length()),
                            m_noticeFormat.get(),
                            labelRect,
                            m_brush.get());
}

void LaserPointerOverlay::Render(uint64_t nowMs)
{
    if (!m_d2dContext || !m_swapChain)
    {
        return;
    }

    m_d2dContext->BeginDraw();
    m_d2dContext->Clear(D2D1::ColorF(0.0f, 0.0f, 0.0f, 0.0f));

    // The stroke is stored in screen coordinates; shift it into overlay-local space.
    m_d2dContext->SetTransform(D2D1::Matrix3x2F::Translation(
        -static_cast<float>(m_overlayBounds.left),
        -static_cast<float>(m_overlayBounds.top)));

    DrawTrails(m_d2dContext.get(), nowMs);
    DrawPresenterNotice(nowMs);

    m_d2dContext->SetTransform(D2D1::Matrix3x2F::Identity());

    const HRESULT endDrawResult = m_d2dContext->EndDraw();
    if (endDrawResult == D2DERR_RECREATE_TARGET || endDrawResult == DXGI_ERROR_DEVICE_REMOVED || endDrawResult == DXGI_ERROR_DEVICE_RESET)
    {
        Logger::warn("Laser Pointer render target lost, recreating.");
        ReleaseGraphics();
        return;
    }

    const HRESULT presentResult = m_swapChain->Present(0, 0);
    if (presentResult == DXGI_ERROR_DEVICE_REMOVED || presentResult == DXGI_ERROR_DEVICE_RESET)
    {
        Logger::warn("Laser Pointer swap chain lost, recreating.");
        ReleaseGraphics();
    }
}

#pragma endregion Graphics

#pragma region Overlay lifetime

void LaserPointerOverlay::ShowOverlay()
{
    if (m_overlayVisible)
    {
        return;
    }

    if (!EnsureOverlayGeometry())
    {
        return;
    }

    ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
    if (!m_overlayBelowTopmost)
    {
        SetWindowPos(m_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }
    m_overlayVisible = true;
}

void LaserPointerOverlay::HideOverlay()
{
    if (!m_overlayVisible)
    {
        return;
    }

    if (m_overlayRegionIsFrame)
    {
        SetWindowRgn(m_hwnd, nullptr, FALSE);
        m_overlayRegionIsFrame = false;
    }
    m_overlayBelowTopmost = false;
    m_overlayZOrderAfter = nullptr;

    ShowWindow(m_hwnd, SW_HIDE);
    m_overlayVisible = false;
}

void LaserPointerOverlay::StartRenderLoop()
{
    if (m_renderLoopRunning)
    {
        return;
    }

    if (SetTimer(m_hwnd, RENDER_TIMER_ID, RENDER_TIMER_INTERVAL_MS, nullptr) != 0)
    {
        m_renderLoopRunning = true;
    }
}

void LaserPointerOverlay::StopRenderLoop()
{
    if (!m_renderLoopRunning)
    {
        return;
    }

    KillTimer(m_hwnd, RENDER_TIMER_ID);
    m_renderLoopRunning = false;
}

void LaserPointerOverlay::BeginDrawing()
{
    if (m_drawing)
    {
        return;
    }

    m_drawing = true;

    // Park a still-visible previous trail instead of wiping it, so pressing the button
    // again in quick succession does not chop off the trail already on screen.
    if (!m_stroke.Empty())
    {
        m_stroke.Finish();
        m_pastStrokes.push_back(m_stroke);

        // Parked trails expire on their own within decayTimeMs, so this only bounds a
        // pathological burst of very fast presses.
        constexpr size_t MAX_PAST_STROKES = 8;
        while (m_pastStrokes.size() > MAX_PAST_STROKES)
        {
            m_pastStrokes.erase(m_pastStrokes.begin());
        }
    }
    m_stroke.Clear();
    m_pendingPoints.clear();
    m_samplesPerTick = REFERENCE_SAMPLES_PER_TICK;

    // The hook has already recorded the position that started this stroke, so the cursor
    // is only a fallback for the case where nothing has been seen yet. Seeding from it
    // unconditionally put one stray point wherever the mouse happened to sit: a hovering
    // pen does not move the system cursor, so that point could be anywhere on screen.
    POINT cursorPosition{};
    if (!m_hasLatestPosition && GetCursorPos(&cursorPosition))
    {
        m_latestPosition = cursorPosition;
        m_hasLatestPosition = true;
    }

    ShowOverlay();
    StartRenderLoop();
}

void LaserPointerOverlay::EndDrawing()
{
    if (!m_drawing)
    {
        return;
    }

    m_drawing = false;
    // Let what has already been drawn decay away instead of snapping off.
    m_stroke.Finish();
}

// The hook is what costs something, so it is installed only while some input source
// can actually start a stroke: an armed mouse, an armed pen, or a configured always-on
// button. Setting the always-on button back to None tears it down again.
bool LaserPointerOverlay::HookNeeded() const noexcept
{
    return m_mouseArmed || m_penArmed || m_settings.alwaysOnButton != LaserPointerButton::None;
}

void LaserPointerOverlay::UpdateHook()
{
    const bool needed = HookNeeded();

    if (needed && m_mouseHook == nullptr)
    {
        if (!EnsureOverlayGeometry())
        {
            Logger::error("Laser Pointer could not create its overlay; not installing the hook.");
            return;
        }

        m_mouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, m_hinstance, 0);
        if (m_mouseHook == nullptr)
        {
            Logger::error("Laser Pointer failed to install the mouse hook.");
            return;
        }

        Logger::info("Laser Pointer hook installed: mouseArmed={} penArmed={} alwaysOn={}.",
                     m_mouseArmed,
                     m_penArmed,
                     static_cast<int>(m_settings.alwaysOnButton));
    }
    else if (!needed && m_mouseHook != nullptr)
    {
        UnhookWindowsHookEx(m_mouseHook);
        m_mouseHook = nullptr;
        Logger::info("Laser Pointer hook removed.");
    }
}

// The mouse shortcut arms only the mouse. The pen has its own shortcut, so arming one
// never implies the other - that is what keeps the two pointers independent.
void LaserPointerOverlay::ArmMouse()
{
    m_mouseArmed = true;
    m_activationButtonHeld = false;

    // Traced once per activation rather than once per stroke: a stroke starts on every
    // button press.
    Trace::StartLaserPointerSession();

    UpdateHook();
    if (m_mouseHook == nullptr)
    {
        m_mouseArmed = false;
        return;
    }

    // Arming only starts listening. Nothing is drawn until the configured button is
    // actually held down.
    Logger::info("Laser Pointer mouse armed, waiting for button {}.", static_cast<int>(m_settings.activationButton));
}

// The window under the pointer wins over the focused one. Focus is invisible state and
// is usually still on whatever was clicked last - Teams, or the settings window - while
// the window being pointed at is exactly the one the user means.
static HWND PickPresenterTarget() noexcept
{
    POINT cursor{};
    if (GetCursorPos(&cursor))
    {
        // The overlay answers HTTRANSPARENT, so WindowFromPoint looks straight through
        // it; GA_ROOT turns a child control into the top-level window that owns it.
        if (const HWND under = WindowFromPoint(cursor))
        {
            const HWND root = GetAncestor(under, GA_ROOT);
            if (PresenterWindow::IsPresentableWindow(root))
            {
                return root;
            }
        }
    }

    // Pointing at the desktop or the taskbar falls back to whatever has focus.
    const HWND foreground = GetForegroundWindow();
    return PresenterWindow::IsPresentableWindow(foreground) ? foreground : nullptr;
}

// Toggles the mirror. It is deliberately independent of the laser: a presenter turns it
// on once, shares it in Teams, and leaves it up for the whole session while the laser
// comes and goes.
void LaserPointerOverlay::TogglePresenter()
{
    if (m_presenter.Active())
    {
        m_presenter.Stop();
        if (!m_drawing)
        {
            StopRenderLoop();
            HideOverlay();
        }
        return;
    }

    const HWND target = PickPresenterTarget();
    if (target == nullptr)
    {
        // The taskbar, the desktop and our own windows are not worth mirroring; the
        // laser still works on screen as usual.
        Logger::info("Laser Pointer presenter not started: nothing presentable under the pointer.");
        return;
    }

    if (!m_presenter.Start(m_hinstance, target, m_d3dDevice.get(), m_d2dDevice.get()))
    {
        Logger::warn("Laser Pointer presenter could not start.");
        return;
    }

    // The mirror lives off screen, so without this the user has no way of telling which
    // window was picked until they open the sharing picker. Outline it and name it.
    m_presenterNoticeRect = m_presenter.TargetRect();
    m_presenterNoticeText = m_presenter.TargetTitle();
    m_presenterNoticeUntilMs = GetTickCount64() + PRESENTER_NOTICE_MS;

    // The mirror has to keep refreshing even with no trail on screen, because the window
    // it mirrors carries on changing.
    ShowOverlay();
    StartRenderLoop();
}

void LaserPointerOverlay::DisarmMouse()
{
    if (!m_mouseArmed)
    {
        return;
    }

    Logger::info("Laser Pointer mouse disarmed.");
    m_mouseArmed = false;
    m_activationButtonHeld = false;

    UpdateHook();
    ReleaseIfIdle();
}

void LaserPointerOverlay::ArmPen()
{
    m_penArmed = true;
    m_penContact = false;
    m_lastPenInputMs = 0;
    m_lastPenMessage = 0;
    m_penMessagesLogged = 0;
    m_penAwaitingFirstSample = false;
    m_penStrokeOpen = false;

    UpdateHook();
    RegisterPenRawInput();

    Logger::info("Laser Pointer pen armed.");
}

void LaserPointerOverlay::DisarmPen()
{
    if (!m_penArmed)
    {
        return;
    }

    Logger::info("Laser Pointer pen disarmed.");
    m_penArmed = false;
    m_penContact = false;
    UpdatePenCapture(GetTickCount64());

    UnregisterPenRawInput();
    UpdateHook();
    ReleaseIfIdle();
    if (!m_mouseArmed && !m_drawing && !m_presenter.Active())
    {
        HideOverlay();
    }
}

// Drops anything still on screen once no input source can start a stroke. The always-on
// button is deliberately excluded from the armed checks: it is not something a shortcut
// arms, so it keeps the overlay alive on its own.
void LaserPointerOverlay::ReleaseIfIdle()
{
    if (m_mouseArmed || m_penArmed || m_alwaysOnButtonHeld)
    {
        return;
    }

    m_drawing = false;
    m_pendingPoints.clear();
    m_stroke.Clear();
    m_pastStrokes.clear();

    // The presenter border outlives the laser, so the surface has to stay up for it.
    if (m_presenter.Active())
    {
        return;
    }

    StopRenderLoop();
    HideOverlay();
}

// Full teardown, used when the module itself is going away. Unlike DisarmMouse/DisarmPen
// this removes the hook unconditionally: an always-on button must not keep a hook alive
// in a module that has been switched off.
void LaserPointerOverlay::Disarm()
{
    m_mouseArmed = false;
    m_penArmed = false;
    UpdatePenCapture(GetTickCount64());
    m_drawing = false;
    m_activationButtonHeld = false;
    m_alwaysOnButtonHeld = false;
    m_penContact = false;
    m_pendingPoints.clear();
    m_stroke.Clear();
    m_pastStrokes.clear();

    m_presenter.Stop();
    UnregisterPenRawInput();

    if (m_mouseHook != nullptr)
    {
        UnhookWindowsHookEx(m_mouseHook);
        m_mouseHook = nullptr;
        Logger::info("Laser Pointer hook removed (module shutting down).");
    }

    StopRenderLoop();
    HideOverlay();
}

void LaserPointerOverlay::SwitchActivationMode()
{
    AcquireSRWLockShared(&m_hwndLock);
    const HWND window = m_hwnd;
    ReleaseSRWLockShared(&m_hwndLock);

    if (window != nullptr)
    {
        PostMessage(window, WM_SWITCH_ACTIVATION_MODE, 0, 0);
    }
}

void LaserPointerOverlay::SwitchPresenterMode()
{
    AcquireSRWLockShared(&m_hwndLock);
    const HWND window = m_hwnd;
    ReleaseSRWLockShared(&m_hwndLock);

    if (window != nullptr)
    {
        PostMessage(window, WM_SWITCH_PRESENTER_MODE, 0, 0);
    }
}

void LaserPointerOverlay::SwitchPenActivationMode()
{
    AcquireSRWLockShared(&m_hwndLock);
    const HWND window = m_hwnd;
    ReleaseSRWLockShared(&m_hwndLock);

    if (window != nullptr)
    {
        PostMessage(window, WM_SWITCH_PEN_ACTIVATION_MODE, 0, 0);
    }
}

#pragma endregion Overlay lifetime

#pragma region Input

LRESULT LaserPointerOverlay::HandleMouseInput(WPARAM message, const MSLLHOOKSTRUCT* data) noexcept
{
    const bool fromPen = IsFromPen(data->dwExtraInfo);

    m_latestPosition = data->pt;
    m_hasLatestPosition = true;

    // The mouse keeps every position the hook reports, so its trail follows the real
    // path rather than a ~62 Hz resampling of it. The pen deliberately does not: a
    // digitizer that drops in and out of range replays the gap as a straight line of
    // ghost points, and a laser pointer wants to look live far more than it wants an
    // accurate record of the stroke. One sample per render tick is plenty.
    if (m_drawing && !fromPen)
    {
        if (m_pendingPoints.size() >= MAX_PENDING_POINTS)
        {
            m_pendingPoints.erase(m_pendingPoints.begin());
        }
        m_pendingPoints.push_back({ data->pt, GetTickCount64() });
    }

    if (m_drawing && !m_renderLoopRunning)
    {
        // The loop parked itself because the trail had faded out while the pointer sat
        // still. Movement means there is something to draw again.
        PostMessage(m_hwnd, WM_WAKE_RENDER_LOOP, WAKE_RESUME, 0);
    }

    const bool wasDrawing = m_drawing;
    bool suppress = false;

    // Diagnostic: reports each distinct message seen from the pen while armed, up to a
    // small cap, so the promoted message sequence is visible without flooding the log.
    if (m_penArmed && fromPen && m_penMessagesLogged < 8 && message != m_lastPenMessage)
    {
        m_lastPenMessage = static_cast<UINT>(message);
        ++m_penMessagesLogged;
        Logger::info("Laser Pointer pen message: msg=0x{:X} extraInfo=0x{:X} at ({},{})",
                     static_cast<unsigned int>(message),
                     static_cast<unsigned long long>(data->dwExtraInfo),
                     data->pt.x,
                     data->pt.y);
    }

    if (fromPen)
    {
        if (m_penArmed)
        {
            if (m_settings.penRenderWhenClose)
            {
                // Proximity mode: any pen activity counts as presence, so the trail
                // follows the pen without it touching. Tip-down is not reliably promoted
                // to WM_LBUTTONDOWN anyway, because an app that pans consumes the
                // pointer input first.
                m_penContact = (message != WM_LBUTTONUP);
            }
            else if (message == WM_LBUTTONDOWN)
            {
                // Contact mode: explicit tip down and up, nothing while hovering.
                m_penContact = true;
            }
            else if (message == WM_LBUTTONUP)
            {
                m_penContact = false;
            }

            if (m_penContact)
            {
                if (!wasDrawing && !m_penStrokeOpen)
                {
                    // Opening sample: the cursor is still travelling from wherever the
                    // mouse was, so this position is not trustworthy. Drop it and start
                    // the trail from the next one.
                    m_penStrokeOpen = true;
                    m_penAwaitingFirstSample = true;
                }
                else
                {
                    m_penAwaitingFirstSample = false;
                }
            }
            else
            {
                m_penStrokeOpen = false;
                m_penAwaitingFirstSample = false;
            }

            m_lastPenInputMs = GetTickCount64();

            // While armed the pen is a laser, not an input device. Letting pen input
            // reach the app was tried and removed: over scrollable content the app takes
            // ownership of the pointer stream, Windows stops promoting it to mouse
            // messages, and the trail dies mid-stroke.
            suppress = true;
        }
    }
    else
    {
        LaserPointerButton button = LaserPointerButton::None;
        bool isDown = false;
        if (ClassifyButton(message, data, button, isDown))
        {
            // The two modes are independent, and the same physical button may serve
            // both: the always-on binding still works while the shortcut has armed the
            // mouse, so each is tracked separately.
            if (m_mouseArmed && button == m_settings.activationButton)
            {
                m_activationButtonHeld = isDown;
                suppress = m_settings.suppressActivationButton && CanSuppress(button);
            }

            if (button == m_settings.alwaysOnButton)
            {
                m_alwaysOnButtonHeld = isDown;
                suppress = suppress || (m_settings.suppressActivationButton && CanSuppress(button));
            }
        }
    }

    const bool shouldDraw = m_activationButtonHeld || m_alwaysOnButtonHeld || m_penContact;
    if (shouldDraw && !wasDrawing)
    {
        // Defer the actual state change to the message loop: hooks must return fast.
        PostMessage(m_hwnd, WM_WAKE_RENDER_LOOP, WAKE_BEGIN, 0);
    }
    else if (!shouldDraw && wasDrawing)
    {
        PostMessage(m_hwnd, WM_WAKE_RENDER_LOOP, WAKE_END, 0);
    }

    return suppress ? 1 : 0;
}

void LaserPointerOverlay::RegisterPenRawInput()
{
    if (m_rawInputRegistered || m_hwnd == nullptr)
    {
        return;
    }

    // RIDEV_INPUTSINK delivers input even while another app is in the foreground, which
    // is the whole point: the pen is normally used over somebody else's window.
    RAWINPUTDEVICE devices[1]{};
    devices[0].usUsagePage = HID_USAGE_PAGE_DIGITIZER;
    devices[0].usUsage = HID_USAGE_DIGITIZER_PEN;
    devices[0].dwFlags = RIDEV_INPUTSINK;
    devices[0].hwndTarget = m_hwnd;

    if (!RegisterRawInputDevices(devices, ARRAYSIZE(devices), sizeof(RAWINPUTDEVICE)))
    {
        Logger::error("Laser Pointer could not register raw pen input: {}", GetLastError());
        return;
    }

    m_rawInputRegistered = true;
    m_rawInputLogCount = 0;
    Logger::info("Laser Pointer registered raw pen input.");
}

void LaserPointerOverlay::UnregisterPenRawInput()
{
    if (!m_rawInputRegistered)
    {
        return;
    }

    RAWINPUTDEVICE devices[1]{};
    devices[0].usUsagePage = HID_USAGE_PAGE_DIGITIZER;
    devices[0].usUsage = HID_USAGE_DIGITIZER_PEN;
    devices[0].dwFlags = RIDEV_REMOVE;

    RegisterRawInputDevices(devices, ARRAYSIZE(devices), sizeof(RAWINPUTDEVICE));
    m_rawInputRegistered = false;
    m_penDevices.clear();
    Logger::info("Laser Pointer unregistered raw pen input.");
}

// Pulls X, Y and the tip switch out of one HID report. Returns false when the device
// does not describe itself in a way this can use.
bool LaserPointerOverlay::ReadPenReport(RAWINPUT* input, POINT& screenPoint, bool& tipDown) noexcept
{
    auto it = m_penDevices.find(input->header.hDevice);
    if (it == m_penDevices.end())
    {
        PenDevice device;

        UINT size = 0;
        if (GetRawInputDeviceInfo(input->header.hDevice, RIDI_PREPARSEDDATA, nullptr, &size) != 0 || size == 0)
        {
            m_penDevices.emplace(input->header.hDevice, device);
            return false;
        }

        device.preparsed.resize(size);
        if (GetRawInputDeviceInfo(input->header.hDevice, RIDI_PREPARSEDDATA, device.preparsed.data(), &size) == static_cast<UINT>(-1))
        {
            m_penDevices.emplace(input->header.hDevice, device);
            return false;
        }

        auto preparsed = reinterpret_cast<PHIDP_PREPARSED_DATA>(device.preparsed.data());
        HIDP_CAPS caps{};
        if (HidP_GetCaps(preparsed, &caps) != HIDP_STATUS_SUCCESS)
        {
            m_penDevices.emplace(input->header.hDevice, device);
            return false;
        }

        if (caps.NumberInputValueCaps != 0)
        {
            std::vector<HIDP_VALUE_CAPS> valueCaps(caps.NumberInputValueCaps);
            USHORT valueCapsLength = caps.NumberInputValueCaps;
            if (HidP_GetValueCaps(HidP_Input, valueCaps.data(), &valueCapsLength, preparsed) == HIDP_STATUS_SUCCESS)
            {
                for (USHORT i = 0; i < valueCapsLength; ++i)
                {
                    const HIDP_VALUE_CAPS& vc = valueCaps[i];
                    if (vc.UsagePage != HID_USAGE_PAGE_GENERIC || vc.IsRange)
                    {
                        continue;
                    }
                    if (vc.NotRange.Usage == HID_USAGE_GENERIC_X && vc.LogicalMax > vc.LogicalMin)
                    {
                        device.minX = vc.LogicalMin;
                        device.maxX = vc.LogicalMax;
                        device.haveX = true;
                    }
                    else if (vc.NotRange.Usage == HID_USAGE_GENERIC_Y && vc.LogicalMax > vc.LogicalMin)
                    {
                        device.minY = vc.LogicalMin;
                        device.maxY = vc.LogicalMax;
                        device.haveY = true;
                    }
                }
            }
        }

        device.usable = device.haveX && device.haveY;
        Logger::info("Laser Pointer pen device: usable={} x=[{},{}] y=[{},{}]",
                     device.usable,
                     device.minX,
                     device.maxX,
                     device.minY,
                     device.maxY);

        it = m_penDevices.emplace(input->header.hDevice, std::move(device)).first;
    }

    PenDevice& device = it->second;
    if (!device.usable)
    {
        return false;
    }

    auto preparsed = reinterpret_cast<PHIDP_PREPARSED_DATA>(device.preparsed.data());
    PCHAR report = reinterpret_cast<PCHAR>(input->data.hid.bRawData);
    const ULONG reportLength = input->data.hid.dwSizeHid;

    ULONG x = 0;
    ULONG y = 0;
    if (HidP_GetUsageValue(HidP_Input, HID_USAGE_PAGE_GENERIC, 0, HID_USAGE_GENERIC_X, &x, preparsed, report, reportLength) != HIDP_STATUS_SUCCESS ||
        HidP_GetUsageValue(HidP_Input, HID_USAGE_PAGE_GENERIC, 0, HID_USAGE_GENERIC_Y, &y, preparsed, report, reportLength) != HIDP_STATUS_SUCCESS)
    {
        return false;
    }

    // The tip switch separates contact from hover. Its absence is not fatal, since
    // proximity mode does not care, so failure here simply reports "not touching".
    tipDown = false;
    ULONG usageCount = 16;
    USAGE usages[16]{};
    if (HidP_GetUsages(HidP_Input, HID_USAGE_PAGE_DIGITIZER, 0, usages, &usageCount, preparsed, report, reportLength) == HIDP_STATUS_SUCCESS)
    {
        for (ULONG i = 0; i < usageCount; ++i)
        {
            if (usages[i] == HID_USAGE_DIGITIZER_TIP_SWITCH)
            {
                tipDown = true;
                break;
            }
        }
    }

    // Digitizer coordinates are logical units spanning the whole tablet surface; map
    // them onto the virtual desktop.
    const int virtualLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
    const int virtualTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
    const int virtualWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
    const int virtualHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);

    const double spanX = static_cast<double>(device.maxX) - static_cast<double>(device.minX);
    const double spanY = static_cast<double>(device.maxY) - static_cast<double>(device.minY);
    if (spanX <= 0.0 || spanY <= 0.0)
    {
        return false;
    }

    const double offsetX = static_cast<double>(x) - static_cast<double>(device.minX);
    const double offsetY = static_cast<double>(y) - static_cast<double>(device.minY);
    screenPoint.x = virtualLeft + static_cast<LONG>((offsetX / spanX) * virtualWidth);
    screenPoint.y = virtualTop + static_cast<LONG>((offsetY / spanY) * virtualHeight);
    return true;
}

void LaserPointerOverlay::HandleRawInput(HRAWINPUT handle) noexcept
{
    UINT size = 0;
    if (GetRawInputData(handle, RID_INPUT, nullptr, &size, sizeof(RAWINPUTHEADER)) != 0 || size == 0)
    {
        return;
    }

    if (m_rawInputBuffer.size() < size)
    {
        m_rawInputBuffer.resize(size);
    }

    if (GetRawInputData(handle, RID_INPUT, m_rawInputBuffer.data(), &size, sizeof(RAWINPUTHEADER)) != size)
    {
        return;
    }

    RAWINPUT* input = reinterpret_cast<RAWINPUT*>(m_rawInputBuffer.data());

    if (m_rawInputLogCount < 3)
    {
        ++m_rawInputLogCount;
        Logger::info("Laser Pointer raw input: type={} sizeHid={} count={}",
                     input->header.dwType,
                     input->header.dwType == RIM_TYPEHID ? input->data.hid.dwSizeHid : 0u,
                     input->header.dwType == RIM_TYPEHID ? input->data.hid.dwCount : 0u);
    }

    if (input->header.dwType != RIM_TYPEHID || !m_penArmed)
    {
        return;
    }

    POINT screenPoint{};
    bool tipDown = false;
    if (!ReadPenReport(input, screenPoint, tipDown))
    {
        return;
    }

    const bool wantDraw = m_settings.penRenderWhenClose || tipDown;
    const bool wasDrawing = m_drawing;
    const uint64_t nowMs = GetTickCount64();

    // Measured before the timestamp is updated: a gap this long means the pen left range
    // and came back somewhere else, and the samples either side must not be joined.
    const bool resumedAfterGap = m_lastPenInputMs != 0 && (nowMs - m_lastPenInputMs) > PEN_RANGE_TIMEOUT_MS;

    m_latestPosition = screenPoint;
    m_hasLatestPosition = true;
    m_lastPenPoint = screenPoint;
    m_lastPenInputMs = nowMs;

    // Hover reports come in before the pen touches, and this is what turns that into a
    // window ready to catch the contact. Waiting for the render tick would be too late:
    // with nothing drawn the loop is parked, so nothing would be capturing yet.
    UpdatePenCapture(nowMs);
    if (!m_renderLoopRunning)
    {
        ShowOverlay();
        StartRenderLoop();
    }

    if (wantDraw)
    {
        m_penContact = true;
        m_penAwaitingFirstSample = false;
        if (!wasDrawing)
        {
            BeginDrawing();
        }
        else if (!m_renderLoopRunning)
        {
            ShowOverlay();
            StartRenderLoop();
        }
    }
    else if (m_penContact)
    {
        m_penContact = false;
        if (wasDrawing)
        {
            EndDrawing();
        }
    }

    // The digitizer reports far faster than the 16 ms render tick, so every report is
    // kept rather than only the one that happens to be current when a frame is drawn.
    // Sampling once per frame is what made pen lines look angular next to the mouse's,
    // which feeds the hook every position it sees. BeginDrawing clears the buffer, so
    // this has to come after it.
    if (m_drawing && wantDraw)
    {
        if (resumedAfterGap)
        {
            // Don't bridge a hover gap: the straight run of points across it is exactly
            // the ghosting that buffering pen input caused the first time round.
            m_pendingPoints.clear();
        }

        if (m_pendingPoints.size() >= MAX_PENDING_POINTS)
        {
            m_pendingPoints.erase(m_pendingPoints.begin());
        }
        m_pendingPoints.push_back({ screenPoint, nowMs });
    }
}

LRESULT CALLBACK LaserPointerOverlay::MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam) noexcept
{
    if (nCode == HC_ACTION && instance != nullptr)
    {
        const auto* data = reinterpret_cast<const MSLLHOOKSTRUCT*>(lParam);
        if (instance->HandleMouseInput(wParam, data) != 0)
        {
            return 1;
        }
    }

    return CallNextHookEx(nullptr, nCode, wParam, lParam);
}

#pragma endregion Input

#pragma region Render loop

float LaserPointerOverlay::DpiScaleForPoint(POINT screenPoint) const
{
    HMONITOR monitor = MonitorFromPoint(screenPoint, MONITOR_DEFAULTTONEAREST);
    if (monitor == nullptr)
    {
        return 1.0f;
    }

    UINT dpiX = 96;
    UINT dpiY = 96;
    if (FAILED(GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, &dpiX, &dpiY)))
    {
        return 1.0f;
    }

    return static_cast<float>(dpiX) / 96.0f;
}

void LaserPointerOverlay::OnRenderTick()
{
    const uint64_t now = GetTickCount64();

    // The pen has no dependable tip-up message, so a gap in pen input is what ends the
    // stroke. Without this the trail would never stop growing once the pen went away.
    // Only proximity mode needs this: contact mode gets an explicit tip-up, and a pen
    // held still against the screen must not have its stroke cut short.
    if (m_penContact && m_settings.penRenderWhenClose && (now - m_lastPenInputMs) > PEN_IDLE_TIMEOUT_MS)
    {
        m_penContact = false;
        if (m_drawing && !m_activationButtonHeld && !m_alwaysOnButtonHeld)
        {
            EndDrawing();
        }
    }

    if (m_drawing && m_penAwaitingFirstSample)
    {
        // Wait for a position reported after the stroke began.
        return;
    }

    if (m_drawing && m_hasLatestPosition)
    {
        const float scale = DpiScaleForPoint(m_latestPosition);

        // The stroke model tapers over a number of *samples*, so feeding it more samples
        // per second would shorten the visible trail. Scaling the setting by the
        // measured rate keeps the trail the same length at any polling rate - the
        // property the old one-sample-per-tick cadence got for free. Smoothed because
        // the per-tick count jitters, and floored at the reference rate so a slow mouse
        // never tapers sooner than it used to.
        const size_t sampleCount = m_pendingPoints.empty() ? 1 : m_pendingPoints.size();
        m_samplesPerTick = m_samplesPerTick * 0.9f + static_cast<float>(sampleCount) * 0.1f;
        const float rateScale = (std::max)(m_samplesPerTick, REFERENCE_SAMPLES_PER_TICK);

        LaserPointerCore::StrokeOptions options;
        options.size = static_cast<float>(m_settings.size) * scale;
        options.streamline = static_cast<float>(m_settings.streamlinePercent) / 100.0f;
        options.decayTimeMs = static_cast<uint32_t>(m_settings.decayTimeMs);
        options.decayLength = static_cast<uint32_t>(static_cast<float>(m_settings.decayLength) * rateScale);
        options.minDistance = 0.75f * scale;
        m_stroke.SetOptions(options);

        if (m_pendingPoints.empty())
        {
            // Stationary pointer: the head still needs a sample so the dot stays lit.
            m_stroke.AddPoint(static_cast<float>(m_latestPosition.x), static_cast<float>(m_latestPosition.y), now);
        }
        else
        {
            for (const auto& pending : m_pendingPoints)
            {
                m_stroke.AddPoint(static_cast<float>(pending.pt.x), static_cast<float>(pending.pt.y), pending.t);
            }
            m_pendingPoints.clear();
        }
    }

    if (m_presenter.Active())
    {
        if (!m_presenter.Present([this, now](ID2D1DeviceContext* context) { DrawTrails(context, now); }))
        {
            Logger::info("Laser Pointer presenter ended (target window gone or device lost).");
            m_presenter.Stop();
        }
    }

    // The border is drawn for the whole session, so the mirror being up is itself a
    // reason to keep rendering.
    const bool presenterActive = m_presenter.Active();
    const bool noticeActive = now < m_presenterNoticeUntilMs;

    // A pen in range keeps the overlay up even with nothing drawn: a hidden window is
    // never hit-tested, and capturing the pen is the whole point of being up.
    const bool penPresent = PenPresent(now);
    UpdatePenCapture(now);

    bool hasContent = m_stroke.Prune(now) || noticeActive || presenterActive || penPresent;

    for (auto it = m_pastStrokes.begin(); it != m_pastStrokes.end();)
    {
        if (it->Prune(now))
        {
            hasContent = true;
            ++it;
        }
        else
        {
            it = m_pastStrokes.erase(it);
        }
    }

    if (!hasContent)
    {
        // Nothing left to draw. Blank the overlay once, then go idle so we are not
        // running a timer and presenting frames while the laser has nothing to show -
        // this covers a stationary pointer in toggle mode as well as a released button.
        // Movement wakes the loop back up from the mouse hook.
        Render(now);
        StopRenderLoop();
        HideOverlay();
        m_stroke.Clear();
        return;
    }

    // A trail is showing whenever there is stroke content or the label is still up; the
    // border on its own does not need the full surface.
    // Capturing the pen needs the whole surface hit-testable and in front of the app, so
    // it rules out the border-only frame region and the drop out of the topmost band.
    const bool trailVisible = !m_stroke.Empty() || !m_pastStrokes.empty() || noticeActive || m_penCapturing;
    UpdateOverlayRegion(trailVisible);
    UpdateOverlayZOrder(trailVisible, now);

    if (!m_overlayVisible)
    {
        ShowOverlay();
    }

    if (!m_d2dContext || !m_swapChain)
    {
        // Device was lost during the previous frame; rebuild before drawing again.
        if (!EnsureOverlayGeometry())
        {
            return;
        }
    }

    Render(now);
}

#pragma endregion Render loop

#pragma region Settings

void LaserPointerOverlay::ApplySettings(LaserPointerSettings settings)
{
    m_settings = settings;
    m_settings.size = std::clamp(m_settings.size, 1, 200);
    m_settings.decayTimeMs = std::clamp(m_settings.decayTimeMs, 100, 10000);
    m_settings.decayLength = std::clamp(m_settings.decayLength, 2, 500);
    m_settings.streamlinePercent = std::clamp(m_settings.streamlinePercent, 0, 95);
}

void LaserPointerOverlay::QueueSettings(LaserPointerSettings settings)
{
    AcquireSRWLockExclusive(&m_settingsLock);
    m_pendingSettings = settings;
    m_settingsPending = true;
    ReleaseSRWLockExclusive(&m_settingsLock);

    AcquireSRWLockShared(&m_hwndLock);
    const HWND window = m_hwnd;
    ReleaseSRWLockShared(&m_hwndLock);

    if (window != nullptr)
    {
        PostMessage(window, WM_APPLY_SETTINGS, 0, 0);
    }
}

void LaserPointerOverlay::ProcessPendingSettings()
{
    LaserPointerSettings settings{};

    AcquireSRWLockExclusive(&m_settingsLock);
    const bool pending = m_settingsPending;
    if (pending)
    {
        settings = m_pendingSettings;
        m_settingsPending = false;
    }
    ReleaseSRWLockExclusive(&m_settingsLock);

    if (!pending)
    {
        return;
    }

    const LaserPointerButton previousAlwaysOn = m_settings.alwaysOnButton;
    ApplySettings(settings);

    if (m_settings.alwaysOnButton != previousAlwaysOn)
    {
        // Re-binding mid-press would strand the flag: the button-up that clears it no
        // longer matches the setting.
        m_alwaysOnButtonHeld = false;
    }

    // The always-on button decides whether the hook runs while nothing is armed, so a
    // change to it has to be acted on here rather than at the next arm.
    UpdateHook();

    // Logged on every applied change: the first thing to check when the configured
    // button does not appear to take effect is whether the change arrived here at all.
    Logger::info("Laser Pointer settings applied (mouseArmed={} penArmed={}): button={} alwaysOn={} suppress={} size={}",
                 m_mouseArmed,
                 m_penArmed,
                 static_cast<int>(m_settings.activationButton),
                 static_cast<int>(m_settings.alwaysOnButton),
                 m_settings.suppressActivationButton,
                 m_settings.size);
}

#pragma endregion Settings

#pragma region Window

LRESULT CALLBACK LaserPointerOverlay::WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept
{
    switch (message)
    {
    case WM_NCCREATE:
        AcquireSRWLockExclusive(&instance->m_hwndLock);
        instance->m_hwnd = hWnd;
        ReleaseSRWLockExclusive(&instance->m_hwndLock);
        return DefWindowProc(hWnd, message, wParam, lParam);

    case WM_CREATE:
        if (!instance->CreateGraphics())
        {
            return -1;
        }
        instance->ProcessPendingSettings();
        // An always-on button read from settings at startup needs its hook now; nothing
        // else will arm to install it.
        instance->UpdateHook();
        return 0;

    case WM_INPUT:
        instance->HandleRawInput(reinterpret_cast<HRAWINPUT>(lParam));
        return DefWindowProc(hWnd, message, wParam, lParam);

    case WM_NCHITTEST:
    {
        // Click-through except for a small patch around an armed pen that is in range,
        // which is where its next contact will land. See UpdatePenCapture.
        if (!instance->m_penCapturing)
        {
            return HTTRANSPARENT;
        }

        const POINT hit{ static_cast<LONG>(static_cast<short>(LOWORD(lParam))),
                         static_cast<LONG>(static_cast<short>(HIWORD(lParam))) };
        return instance->NearLastPenPoint(hit) ? HTCLIENT : HTTRANSPARENT;
    }

    // Whatever the pen does while captured is swallowed here. Not calling DefWindowProc
    // also stops these being promoted to mouse messages, so the hook does not see the
    // same contact a second time.
    case WM_POINTERDOWN:
    case WM_POINTERUPDATE:
    case WM_POINTERUP:
    case WM_POINTERENTER:
    case WM_POINTERLEAVE:
    case WM_POINTERCAPTURECHANGED:
        return 0;

    case WM_SWITCH_ACTIVATION_MODE:
        if (instance->m_mouseArmed)
        {
            instance->DisarmMouse();
        }
        else
        {
            instance->ArmMouse();
        }
        return 0;

    case WM_SWITCH_PRESENTER_MODE:
        instance->TogglePresenter();
        return 0;

    case WM_SWITCH_PEN_ACTIVATION_MODE:
        if (instance->m_penArmed)
        {
            instance->DisarmPen();
        }
        else
        {
            instance->ArmPen();
        }
        return 0;

    case WM_WAKE_RENDER_LOOP:
        switch (wParam)
        {
        case WAKE_BEGIN:
            instance->BeginDrawing();
            break;
        case WAKE_END:
            instance->EndDrawing();
            break;
        case WAKE_RESUME:
            if (instance->m_mouseArmed || instance->m_penArmed || instance->m_alwaysOnButtonHeld)
            {
                instance->ShowOverlay();
                instance->StartRenderLoop();
            }
            break;
        default:
            break;
        }
        return 0;

    case WM_APPLY_SETTINGS:
        instance->ProcessPendingSettings();
        return 0;

    case WM_TERMINATE_OVERLAY:
        // Destroying the owner destroys this overlay with it, which lands in WM_DESTROY
        // below and ends the message loop.
        instance->Disarm();
        if (instance->m_hwndOwner != nullptr)
        {
            DestroyWindow(instance->m_hwndOwner);
        }
        else
        {
            DestroyWindow(hWnd);
        }
        return 0;

    case WM_TIMER:
        if (wParam == RENDER_TIMER_ID)
        {
            instance->OnRenderTick();
        }
        return 0;

    case WM_DISPLAYCHANGE:
    case WM_DPICHANGED:
        instance->EnsureOverlayGeometry();
        return 0;

    case WM_DESTROY:
        instance->Disarm();
        instance->ReleaseGraphics();
        PostQuitMessage(0);
        return 0;

    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
}

bool LaserPointerOverlay::MyRegisterClass(HINSTANCE hInstance)
{
    WNDCLASS wc{};

    m_hinstance = hInstance;

    SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    if (!GetClassInfoW(hInstance, m_className, &wc))
    {
        wc.lpfnWndProc = WndProc;
        wc.hInstance = hInstance;
        wc.hIcon = LoadIcon(hInstance, IDI_APPLICATION);
        wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
        wc.hbrBackground = static_cast<HBRUSH>(GetStockObject(NULL_BRUSH));
        wc.lpszClassName = m_className;

        if (!RegisterClassW(&wc))
        {
            return false;
        }
    }

    // Owner window: destroying it tears the overlay down too, which is how the runner's
    // disable() unwinds this thread. Guarded because Terminate() reads it from the
    // runner's thread.
    const HWND owner = CreateWindow(L"static", nullptr, WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, hInstance, nullptr);
    AcquireSRWLockExclusive(&m_hwndLock);
    m_hwndOwner = owner;
    ReleaseSRWLockExclusive(&m_hwndLock);

    const DWORD exStyle = WS_EX_TRANSPARENT | WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
    return CreateWindowExW(exStyle, m_className, m_windowTitle, WS_POPUP, CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, m_hwndOwner, nullptr, hInstance, nullptr) != nullptr;
}

void LaserPointerOverlay::Terminate()
{
    AcquireSRWLockShared(&m_hwndLock);
    const HWND window = m_hwnd;
    const HWND owner = m_hwndOwner;
    ReleaseSRWLockShared(&m_hwndLock);

    // Routed through our own window proc rather than posting WM_CLOSE to the owner: the
    // owner is a "static" class window whose procedure is not ours, so relying on it to
    // turn WM_CLOSE into a DestroyWindow would make shutdown depend on undocumented
    // behaviour. DestroyWindow must also run on the thread that created the window,
    // which is the thread pumping this message.
    if (window != nullptr && PostMessage(window, WM_TERMINATE_OVERLAY, 0, 0))
    {
        return;
    }

    Logger::warn("Laser Pointer overlay window unavailable while terminating; closing the owner directly.");
    if (owner != nullptr)
    {
        PostMessage(owner, WM_CLOSE, 0, 0);
    }
}

#pragma endregion Window

#pragma region LaserPointer_API

void LaserPointerApplySettings(LaserPointerSettings settings)
{
    if (LaserPointerOverlay::instance != nullptr)
    {
        LaserPointerOverlay::instance->QueueSettings(settings);
    }
}

void LaserPointerSwitch()
{
    if (LaserPointerOverlay::instance != nullptr)
    {
        Logger::info("Switching Laser Pointer mouse activation mode.");
        LaserPointerOverlay::instance->SwitchActivationMode();
    }
}

void LaserPointerSwitchPen()
{
    if (LaserPointerOverlay::instance != nullptr)
    {
        Logger::info("Switching Laser Pointer pen activation mode.");
        LaserPointerOverlay::instance->SwitchPenActivationMode();
    }
}

void LaserPointerSwitchPresenter()
{
    if (LaserPointerOverlay::instance != nullptr)
    {
        Logger::info("Switching Laser Pointer presenter window.");
        LaserPointerOverlay::instance->SwitchPresenterMode();
    }
}

void LaserPointerDisable()
{
    if (LaserPointerOverlay::instance != nullptr)
    {
        Logger::info("Terminating the Laser Pointer instance.");
        LaserPointerOverlay::instance->Terminate();
    }
}

bool LaserPointerIsEnabled()
{
    return LaserPointerOverlay::instance != nullptr;
}

int LaserPointerMain(HINSTANCE hInstance, LaserPointerSettings settings)
{
    Logger::info("Starting a Laser Pointer instance.");
    if (LaserPointerOverlay::instance != nullptr)
    {
        Logger::error("A Laser Pointer instance was still working when trying to start a new one.");
        return 0;
    }

    LaserPointerOverlay overlay;
    LaserPointerOverlay::instance = &overlay;
    overlay.ApplySettings(settings);

    if (!overlay.MyRegisterClass(hInstance))
    {
        Logger::error("Couldn't initialize a Laser Pointer instance.");
        LaserPointerOverlay::instance = nullptr;
        return FALSE;
    }

    Logger::info("Initialized the Laser Pointer instance.");

    // The activation shortcut arms and disarms the tool in both modes; only the
    // explicit "activate on startup" setting arms it without one. In hold mode arming
    // draws nothing on its own - it just starts listening for the configured button -
    // so the laser stays invisible until that button is actually held.
    if (settings.autoActivate)
    {
        overlay.SwitchActivationMode();
    }

    MSG msg;
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    Logger::info("Laser Pointer message loop ended.");
    LaserPointerOverlay::instance = nullptr;

    return static_cast<int>(msg.wParam);
}

#pragma endregion LaserPointer_API
