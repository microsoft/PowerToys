// LaserPointer.cpp : Overlay, input handling and rendering for the Laser Pointer module.
//
// The trail model itself lives in LaserStroke.{h,cpp}; this file owns the transparent
// click-through overlay it is drawn on, the low level mouse hook that feeds it, and the
// render loop.

#include "pch.h"
#include "LaserPointer.h"
#include "LaserStroke.h"
#include "trace.h"

#include <algorithm>
#include <cmath>
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
    void ApplySettings(LaserPointerSettings settings);
    void QueueSettings(LaserPointerSettings settings);

private:
    static LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept;
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam) noexcept;

    LRESULT HandleMouseInput(WPARAM message, const MSLLHOOKSTRUCT* data) noexcept;

    void ArmMouse();
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
    void RenderStroke(const LaserPointerCore::LaserStroke& stroke, uint64_t nowMs);
    void FillOutline(const LaserPointerCore::LaserStroke& stroke, uint64_t nowMs, float widthScale, const D2D1_COLOR_F& color);
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

void LaserPointerOverlay::FillOutline(const LaserPointerCore::LaserStroke& stroke, uint64_t nowMs, float widthScale, const D2D1_COLOR_F& color)
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

    m_brush->SetColor(color);
    m_d2dContext->FillGeometry(geometry.get(), m_brush.get());
}

// Draws one trail: an optional soft halo, then the solid body. Both are filled outlines
// built from the same centerline rather than overlapping stamps, so a semi transparent
// laser colour keeps a single, even alpha instead of darkening wherever the stroke
// overlaps itself.
void LaserPointerOverlay::RenderStroke(const LaserPointerCore::LaserStroke& stroke, uint64_t nowMs)
{
    const auto& c = m_settings.laserColor;
    const float r = c.R / 255.0f;
    const float g = c.G / 255.0f;
    const float b = c.B / 255.0f;
    const float a = c.A / 255.0f;

    if (m_settings.glowEnabled)
    {
        FillOutline(stroke, nowMs, GLOW_WIDTH_SCALE, D2D1::ColorF(r, g, b, a * GLOW_ALPHA_SCALE));
    }

    FillOutline(stroke, nowMs, 1.0f, D2D1::ColorF(r, g, b, a));
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

    for (const auto& past : m_pastStrokes)
    {
        RenderStroke(past, nowMs);
    }

    RenderStroke(m_stroke, nowMs);

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
    SetWindowPos(m_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    m_overlayVisible = true;
}

void LaserPointerOverlay::HideOverlay()
{
    if (!m_overlayVisible)
    {
        return;
    }

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

    POINT cursorPosition{};
    if (GetCursorPos(&cursorPosition))
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

    UpdateHook();
    if (m_mouseHook == nullptr)
    {
        m_penArmed = false;
        return;
    }

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

    UpdateHook();
    ReleaseIfIdle();
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
    m_drawing = false;
    m_activationButtonHeld = false;
    m_alwaysOnButtonHeld = false;
    m_penContact = false;
    m_pendingPoints.clear();
    m_stroke.Clear();
    m_pastStrokes.clear();

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
    m_latestPosition = data->pt;
    m_hasLatestPosition = true;

    // Keep every position the hook reports rather than collapsing to the newest one.
    // The render tick drains this, so the trail follows the mouse's real path instead of
    // a ~62 Hz resampling of it.
    if (m_drawing)
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

    const bool fromPen = IsFromPen(data->dwExtraInfo);
    const bool wasDrawing = m_drawing;
    bool suppress = false;

    if (fromPen)
    {
        // The pen tip touching down is the natural draw gesture, whichever mouse button
        // happens to be configured. Pen events are never swallowed so the pen keeps
        // working normally underneath.
        if (m_penArmed)
        {
            if (message == WM_LBUTTONDOWN)
            {
                m_penContact = true;
            }
            else if (message == WM_LBUTTONUP)
            {
                m_penContact = false;
            }
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

    bool hasContent = m_stroke.Prune(now);

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

    case WM_NCHITTEST:
        return HTTRANSPARENT;

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
