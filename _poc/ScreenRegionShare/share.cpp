// share.cpp — Screen Region Share PoC (marker overlay + shareable mirror).
//
// This combines the two earlier PoCs into the real end-to-end experience:
//
//   * overlay.cpp  -> the RED HOLLOW FRAME "marker" the user sees and resizes.
//   * main.cpp     -> the WGC capture pipeline that mirrors a screen region
//                     into a normal top-level window Teams/Zoom/Meet can share.
//
// Flow:
//   Ctrl+Shift+R  -> drag a region. A persistent red frame stays in place, and
//                    a separate "mirror" window (real pixels) starts showing
//                    exactly that region. Share the mirror in your call app.
//   Ctrl+Shift+R  -> (again) dismiss the frame + stop the mirror.
//   Ctrl+Shift+Q  -> quit.
//
// Why two windows?  A see-through overlay shares as blank in Teams (there are
// no pixels to capture). So the frame is the *control surface* the user drives,
// while the mirror is the *shareable surface* with real content. The marker is
// flagged WDA_EXCLUDEFROMCAPTURE so it never shows up in the mirror (clean view,
// no red border, no feedback loop). The mirror is parked on another monitor
// (or off to the side) so it doesn't overlap the captured region.

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <windowsx.h>
#include <dwmapi.h>
#include <d3d11.h>
#include <dxgi1_6.h>
#include <inspectable.h>
#include <cstdio>
#include <algorithm>

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Metadata.h>
#include <winrt/Windows.Graphics.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>

#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "dwmapi.lib")
#pragma comment(lib, "windowsapp.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "gdi32.lib")

namespace wgc  = winrt::Windows::Graphics::Capture;
namespace wgdx = winrt::Windows::Graphics::DirectX;
using winrt::Windows::Foundation::Metadata::ApiInformation;
using winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice;
using winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface;

// ---- tunables -------------------------------------------------------------
static const int      BORDER      = 4;                    // frame thickness (px)
static const int      GRAB        = 18;                   // corner/edge grab zone
static const int      MINW        = 80;
static const int      MINH        = 60;
static const COLORREF FRAME_COLOR = RGB(232, 17, 35);     // PowerToys-ish red
static const wchar_t* kMirrorTitle = L"PowerToys \u2014 Shared Region (PoC)";
static const DWORD    MIRROR_STYLE = WS_OVERLAPPEDWINDOW;

static const UINT     WM_APP_FRAME   = WM_APP + 1;
static const UINT_PTR TIMER_KEEPTOP  = 1;
static const UINT_PTR TIMER_AUTOEXIT = 0xBEEF;

#ifndef WDA_EXCLUDEFROMCAPTURE
#define WDA_EXCLUDEFROMCAPTURE 0x00000011
#endif

// ---- globals --------------------------------------------------------------
static HINSTANCE g_inst   = nullptr;
static HWND      g_marker = nullptr;

// Persistent graphics device (created once, reused across share sessions).
static winrt::com_ptr<ID3D11Device>        g_d3d;
static winrt::com_ptr<ID3D11DeviceContext> g_ctx;
static IDirect3DDevice                     g_winrtDevice{ nullptr };

// Per-share state.
static bool                              g_active  = false;
static bool                              g_closing = false;
static HWND                              g_mirror  = nullptr;
static RECT                              g_monRect{};        // captured monitor (screen px)
static LONG                              g_cropX = 0, g_cropY = 0, g_cropW = 0, g_cropH = 0;
static winrt::com_ptr<IDXGISwapChain1>   g_swap;
static wgc::GraphicsCaptureItem          g_item{ nullptr };
static wgc::Direct3D11CaptureFramePool   g_pool{ nullptr };
static wgc::GraphicsCaptureSession       g_session{ nullptr };

static void StopShare();

static void logf(const wchar_t* fmt, ...)
{
    wchar_t buf[1024];
    va_list a; va_start(a, fmt);
    _vsnwprintf_s(buf, _countof(buf), _TRUNCATE, fmt, a);
    va_end(a);
    wprintf(L"%s\n", buf); fflush(stdout);
}

template <typename T> static T clampv(T v, T lo, T hi) { return v < lo ? lo : (v > hi ? hi : v); }

static void EnablePerMonitorDpi()
{
    HMODULE u = GetModuleHandleW(L"user32.dll");
    if (!u) return;
    typedef BOOL (WINAPI *SetCtxFn)(DPI_AWARENESS_CONTEXT);
    auto fn = reinterpret_cast<SetCtxFn>(GetProcAddress(u, "SetProcessDpiAwarenessContext"));
    if (fn) fn(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
}

// ===========================================================================
//  Selection overlay (drag a rectangle across the whole virtual desktop)
// ===========================================================================
struct SelState
{
    bool  dragging = false, done = false, cancel = false;
    POINT start{}, cur{};
    int   vx = 0, vy = 0;
};

static LRESULT CALLBACK SelProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    auto* st = reinterpret_cast<SelState*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    switch (msg)
    {
    case WM_SETCURSOR: SetCursor(LoadCursorW(nullptr, IDC_CROSS)); return TRUE;
    case WM_LBUTTONDOWN:
        st->dragging = true;
        st->start = { GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
        st->cur = st->start; SetCapture(hwnd); return 0;
    case WM_MOUSEMOVE:
        if (st->dragging) { st->cur = { GET_X_LPARAM(lp), GET_Y_LPARAM(lp) }; InvalidateRect(hwnd, nullptr, TRUE); }
        return 0;
    case WM_LBUTTONUP:
        if (st->dragging) { st->dragging = false; st->cur = { GET_X_LPARAM(lp), GET_Y_LPARAM(lp) }; ReleaseCapture(); st->done = true; }
        return 0;
    case WM_KEYDOWN:
        if (wp == VK_ESCAPE) st->cancel = true;
        return 0;
    case WM_ERASEBKGND: return 1;
    case WM_PAINT:
    {
        PAINTSTRUCT ps; HDC hdc = BeginPaint(hwnd, &ps);
        RECT rc; GetClientRect(hwnd, &rc);
        HBRUSH bg = CreateSolidBrush(RGB(20, 20, 20));
        FillRect(hdc, &rc, bg); DeleteObject(bg);
        if (st->dragging)
        {
            RECT s{ std::min(st->start.x, st->cur.x), std::min(st->start.y, st->cur.y),
                    std::max(st->start.x, st->cur.x), std::max(st->start.y, st->cur.y) };
            HBRUSH red = CreateSolidBrush(FRAME_COLOR); RECT t;
            t = { s.left, s.top, s.right, s.top + 3 };           FillRect(hdc, &t, red);
            t = { s.left, s.bottom - 3, s.right, s.bottom };     FillRect(hdc, &t, red);
            t = { s.left, s.top, s.left + 3, s.bottom };         FillRect(hdc, &t, red);
            t = { s.right - 3, s.top, s.right, s.bottom };       FillRect(hdc, &t, red);
            DeleteObject(red);
        }
        EndPaint(hwnd, &ps);
        return 0;
    }
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

static bool SelectRegion(RECT& out)
{
    static bool registered = false;
    if (!registered)
    {
        WNDCLASSW wc{};
        wc.lpfnWndProc = SelProc; wc.hInstance = g_inst;
        wc.lpszClassName = L"SRS_SelOverlay"; wc.hCursor = LoadCursorW(nullptr, IDC_CROSS);
        RegisterClassW(&wc); registered = true;
    }

    SelState st;
    st.vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
    st.vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
    int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
    int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);

    HWND hwnd = CreateWindowExW(
        WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
        L"SRS_SelOverlay", L"", WS_POPUP,
        st.vx, st.vy, vw, vh, nullptr, nullptr, g_inst, nullptr);
    if (!hwnd) return false;

    SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(&st));
    SetLayeredWindowAttributes(hwnd, 0, 90, LWA_ALPHA);
    ShowWindow(hwnd, SW_SHOW); SetForegroundWindow(hwnd);
    logf(L"[select] Drag a region (Esc to cancel)...");

    MSG m;
    while (!st.done && !st.cancel && GetMessageW(&m, nullptr, 0, 0))
    {
        if (m.message == WM_HOTKEY) continue;
        TranslateMessage(&m); DispatchMessage(&m);
    }
    DestroyWindow(hwnd);

    if (st.cancel) { logf(L"[select] cancelled."); return false; }
    RECT r{ std::min(st.start.x, st.cur.x) + st.vx, std::min(st.start.y, st.cur.y) + st.vy,
            std::max(st.start.x, st.cur.x) + st.vx, std::max(st.start.y, st.cur.y) + st.vy };
    if ((r.right - r.left) < 8 || (r.bottom - r.top) < 8) { logf(L"[select] region too small; ignored."); return false; }
    out = r;
    return true;
}

// ===========================================================================
//  D3D / WGC helpers
// ===========================================================================
static winrt::com_ptr<ID3D11Device> CreateD3DDevice(winrt::com_ptr<ID3D11DeviceContext>& ctx)
{
    winrt::com_ptr<ID3D11Device> d3d;
    UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
    HRESULT hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags,
                                   nullptr, 0, D3D11_SDK_VERSION, d3d.put(), nullptr, ctx.put());
    if (FAILED(hr))
    {
        logf(L"[warn] Hardware D3D device failed (0x%08X); using WARP.", hr);
        d3d = nullptr; ctx = nullptr;
        winrt::check_hresult(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, flags,
                                               nullptr, 0, D3D11_SDK_VERSION, d3d.put(), nullptr, ctx.put()));
    }
    return d3d;
}

static IDirect3DDevice CreateWinrtDevice(winrt::com_ptr<ID3D11Device> const& d3d)
{
    auto dxgi = d3d.as<IDXGIDevice>();
    winrt::com_ptr<IInspectable> inspectable;
    winrt::check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgi.get(), inspectable.put()));
    return inspectable.as<IDirect3DDevice>();
}

static winrt::com_ptr<ID3D11Texture2D> TextureFromSurface(IDirect3DSurface const& surface)
{
    auto access = surface.as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
    winrt::com_ptr<ID3D11Texture2D> tex;
    winrt::check_hresult(access->GetInterface(winrt::guid_of<ID3D11Texture2D>(), tex.put_void()));
    return tex;
}

// Crop the live monitor frame down to the marker rect and present it.
static void RenderFrame()
{
    if (g_closing || !g_pool || !g_swap) return;
    auto frame = g_pool.TryGetNextFrame();
    if (!frame) return;

    auto srcTex = TextureFromSurface(frame.Surface());
    D3D11_TEXTURE2D_DESC sd{}; srcTex->GetDesc(&sd);

    winrt::com_ptr<ID3D11Texture2D> back;
    winrt::check_hresult(g_swap->GetBuffer(0, winrt::guid_of<ID3D11Texture2D>(), back.put_void()));
    D3D11_TEXTURE2D_DESC bd{}; back->GetDesc(&bd);

    LONG availW = static_cast<LONG>(sd.Width)  - g_cropX;
    LONG availH = static_cast<LONG>(sd.Height) - g_cropY;
    LONG w = clampv<LONG>(std::min<LONG>(g_cropW, availW), 0, static_cast<LONG>(bd.Width));
    LONG h = clampv<LONG>(std::min<LONG>(g_cropH, availH), 0, static_cast<LONG>(bd.Height));

    if (w > 0 && h > 0)
    {
        D3D11_BOX box{};
        box.left  = clampv<UINT>(static_cast<UINT>(g_cropX), 0, sd.Width);
        box.top   = clampv<UINT>(static_cast<UINT>(g_cropY), 0, sd.Height);
        box.right = box.left + static_cast<UINT>(w);
        box.bottom = box.top + static_cast<UINT>(h);
        box.front = 0; box.back = 1;
        g_ctx->CopySubresourceRegion(back.get(), 0, 0, 0, 0, srcTex.get(), 0, &box);
    }
    back = nullptr;             // release before Present (FLIP requirement)
    g_swap->Present(1, 0);
    frame.Close();
}

// ===========================================================================
//  Mirror window (the shareable surface)
// ===========================================================================
static LRESULT CALLBACK MirrorProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    switch (msg)
    {
    case WM_APP_FRAME:
        RenderFrame();
        return 0;
    case WM_SIZE:
        if (g_swap && wp != SIZE_MINIMIZED)
        {
            UINT cw = LOWORD(lp), ch = HIWORD(lp);
            if (cw && ch) g_swap->ResizeBuffers(0, cw, ch, DXGI_FORMAT_UNKNOWN, 0);
        }
        return 0;
    case WM_CLOSE:
        // User closed the mirror -> treat as "stop sharing" (keeps app alive).
        StopShare();
        return 0;
    case WM_DESTROY:
        g_closing = true;
        return 0;
    default:
        return DefWindowProcW(hwnd, msg, wp, lp);
    }
}

// Park the mirror completely OFF every monitor so the user never sees it, while
// DWM still composes it -> any "Share a window" app can still enumerate and
// capture it. Placing the whole rect left of the virtual screen guarantees it
// can never overlap the captured region (no feedback loop) on any layout.
static POINT OffscreenOrigin(int outerW)
{
    int vsLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
    int vsTop  = GetSystemMetrics(SM_YVIRTUALSCREEN);
    // Right edge sits 200px left of the leftmost monitor -> fully off-screen
    // regardless of the window's height.
    return POINT{ vsLeft - outerW - 200, vsTop + 100 };
}

// ===========================================================================
//  Marker window (persistent red hollow frame; click-through interior)
// ===========================================================================
static void UpdateFrameRegion(HWND hwnd)
{
    RECT rc; GetClientRect(hwnd, &rc);
    int w = rc.right, h = rc.bottom;
    if (w <= 0 || h <= 0) return;
    HRGN outer = CreateRectRgn(0, 0, w, h);
    int ix = std::min(BORDER, w / 2), iy = std::min(BORDER, h / 2);
    HRGN inner = CreateRectRgn(ix, iy, w - ix, h - iy);
    CombineRgn(outer, outer, inner, RGN_DIFF);
    DeleteObject(inner);
    SetWindowRgn(hwnd, outer, TRUE);
}

// Marker moved/resized -> recompute crop and resize the mirror to match.
static void SyncCropFromMarker()
{
    if (!g_active || !g_marker || !g_mirror) return;
    RECT mr; GetWindowRect(g_marker, &mr);
    g_cropX = mr.left - g_monRect.left;
    g_cropY = mr.top  - g_monRect.top;
    g_cropW = mr.right - mr.left;
    g_cropH = mr.bottom - mr.top;

    RECT wr{ 0, 0, g_cropW, g_cropH };
    AdjustWindowRect(&wr, MIRROR_STYLE, FALSE);
    int outerW = wr.right - wr.left, outerH = wr.bottom - wr.top;
    POINT slot = OffscreenOrigin(outerW);
    // Keep it parked off-screen even as it grows (recompute X from new width).
    SetWindowPos(g_mirror, nullptr, slot.x, slot.y, outerW, outerH,
                 SWP_NOZORDER | SWP_NOACTIVATE);
}

static LRESULT CALLBACK MarkerProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    switch (msg)
    {
    case WM_NCHITTEST:
    {
        POINT pt{ GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
        ScreenToClient(hwnd, &pt);
        RECT rc; GetClientRect(hwnd, &rc);
        bool left = pt.x < GRAB, right = pt.x >= rc.right - GRAB;
        bool top = pt.y < GRAB, bottom = pt.y >= rc.bottom - GRAB;
        if (top && left)     return HTTOPLEFT;
        if (top && right)    return HTTOPRIGHT;
        if (bottom && left)  return HTBOTTOMLEFT;
        if (bottom && right) return HTBOTTOMRIGHT;
        if (left)   return HTLEFT;
        if (right)  return HTRIGHT;
        if (top)    return HTTOP;
        if (bottom) return HTBOTTOM;
        return HTNOWHERE;
    }
    case WM_GETMINMAXINFO:
    {
        auto* mmi = reinterpret_cast<MINMAXINFO*>(lp);
        mmi->ptMinTrackSize.x = MINW; mmi->ptMinTrackSize.y = MINH;
        return 0;
    }
    case WM_SIZE:
        UpdateFrameRegion(hwnd);
        SyncCropFromMarker();
        InvalidateRect(hwnd, nullptr, TRUE);
        return 0;
    case WM_MOVE:
        SyncCropFromMarker();
        return 0;
    case WM_TIMER:
        if (wp == TIMER_KEEPTOP)
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        return 0;
    case WM_ERASEBKGND: return 1;
    case WM_PAINT:
    {
        PAINTSTRUCT ps; HDC hdc = BeginPaint(hwnd, &ps);
        RECT rc; GetClientRect(hwnd, &rc);
        HBRUSH red = CreateSolidBrush(FRAME_COLOR);
        FillRect(hdc, &rc, red); DeleteObject(red);
        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_DESTROY:
        KillTimer(hwnd, TIMER_KEEPTOP);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

static void CreateMarker(const RECT& r)
{
    static bool registered = false;
    if (!registered)
    {
        WNDCLASSW wc{};
        wc.lpfnWndProc = MarkerProc; wc.hInstance = g_inst;
        wc.lpszClassName = L"SRS_Marker"; wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        RegisterClassW(&wc); registered = true;
    }

    int w = r.right - r.left, h = r.bottom - r.top;
    g_marker = CreateWindowExW(
        WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
        L"SRS_Marker", L"Screen Region Share marker", WS_POPUP,
        r.left, r.top, w, h, nullptr, nullptr, g_inst, nullptr);
    if (!g_marker) { logf(L"[marker] CreateWindow failed."); return; }

    // Keep the red frame out of the mirror's capture (clean shared view, no
    // feedback loop). Requires Windows 10 2004+.
    if (!SetWindowDisplayAffinity(g_marker, WDA_EXCLUDEFROMCAPTURE))
        logf(L"[marker] WDA_EXCLUDEFROMCAPTURE not available (0x%08X) - red border may show in the share.", GetLastError());

    UpdateFrameRegion(g_marker);
    ShowWindow(g_marker, SW_SHOWNOACTIVATE);
    SetWindowPos(g_marker, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
    SetTimer(g_marker, TIMER_KEEPTOP, 400, nullptr);
    logf(L"[marker] live at %d,%d %dx%d (drag any border/corner to resize).", r.left, r.top, w, h);
}

// ===========================================================================
//  Share session lifecycle
// ===========================================================================
static void StartShare(const RECT& sel)
{
    if (g_active) return;

    CreateMarker(sel);
    if (!g_marker) return;

    // Capture the monitor under the marker's center; crop to the marker rect.
    POINT center{ (sel.left + sel.right) / 2, (sel.top + sel.bottom) / 2 };
    HMONITOR hmon = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
    MONITORINFO mi{ sizeof(mi) }; GetMonitorInfoW(hmon, &mi);
    g_monRect = mi.rcMonitor;
    g_cropX = sel.left - g_monRect.left;
    g_cropY = sel.top  - g_monRect.top;
    g_cropW = sel.right - sel.left;
    g_cropH = sel.bottom - sel.top;

    try
    {
        auto interop = winrt::get_activation_factory<wgc::GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
        winrt::check_hresult(interop->CreateForMonitor(
            hmon, winrt::guid_of<wgc::GraphicsCaptureItem>(), winrt::put_abi(g_item)));

        // Mirror window sized to the region, parked off the captured area.
        static bool mirrorClass = false;
        if (!mirrorClass)
        {
            WNDCLASSEXW wc{ sizeof(wc) };
            wc.lpfnWndProc = MirrorProc; wc.hInstance = g_inst;
            wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
            wc.hbrBackground = reinterpret_cast<HBRUSH>(GetStockObject(BLACK_BRUSH));
            wc.lpszClassName = L"SRS_Mirror";
            RegisterClassExW(&wc); mirrorClass = true;
        }
        POINT slot = OffscreenOrigin(0); // temp; recompute below with outer size
        RECT wr{ 0, 0, g_cropW, g_cropH };
        AdjustWindowRect(&wr, MIRROR_STYLE, FALSE);
        int outerW = wr.right - wr.left, outerH = wr.bottom - wr.top;
        slot = OffscreenOrigin(outerW);
        // WS_EX_APPWINDOW keeps it in the "Share a window" picker even though
        // it lives off-screen and is never shown to the user.
        g_mirror = CreateWindowExW(
            WS_EX_APPWINDOW, L"SRS_Mirror", kMirrorTitle, MIRROR_STYLE,
            slot.x, slot.y, outerW, outerH,
            nullptr, nullptr, g_inst, nullptr);
        if (!g_mirror) { logf(L"[mirror] CreateWindow failed."); StopShare(); return; }

        // Swapchain for the mirror.
        auto dxgiDevice = g_d3d.as<IDXGIDevice>();
        winrt::com_ptr<IDXGIAdapter> adapter;
        winrt::check_hresult(dxgiDevice->GetAdapter(adapter.put()));
        winrt::com_ptr<IDXGIFactory2> factory;
        winrt::check_hresult(adapter->GetParent(winrt::guid_of<IDXGIFactory2>(), factory.put_void()));

        DXGI_SWAP_CHAIN_DESC1 scd{};
        scd.Width  = static_cast<UINT>(std::max<LONG>(g_cropW, 1));
        scd.Height = static_cast<UINT>(std::max<LONG>(g_cropH, 1));
        scd.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        scd.SampleDesc.Count = 1;
        scd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        scd.BufferCount = 2;
        scd.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
        g_swap = nullptr;
        winrt::check_hresult(factory->CreateSwapChainForHwnd(
            g_d3d.get(), g_mirror, &scd, nullptr, nullptr, g_swap.put()));

        ShowWindow(g_mirror, SW_SHOWNOACTIVATE);
        SetWindowPos(g_mirror, nullptr, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE | SWP_NOZORDER);

        // Capture session; frames post to the UI thread to render.
        g_closing = false;
        g_pool = wgc::Direct3D11CaptureFramePool::CreateFreeThreaded(
            g_winrtDevice, wgdx::DirectXPixelFormat::B8G8R8A8UIntNormalized, 2, g_item.Size());
        g_session = g_pool.CreateCaptureSession(g_item);
        if (ApiInformation::IsPropertyPresent(winrt::name_of<wgc::GraphicsCaptureSession>(), L"IsBorderRequired"))
            g_session.IsBorderRequired(false);
        if (ApiInformation::IsPropertyPresent(winrt::name_of<wgc::GraphicsCaptureSession>(), L"IsCursorCaptureEnabled"))
            g_session.IsCursorCaptureEnabled(true);

        HWND mir = g_mirror;
        g_pool.FrameArrived([mir](auto&&, auto&&) { PostMessageW(mir, WM_APP_FRAME, 0, 0); });
        g_session.StartCapture();

        g_active = true;
        logf(L"[share] live. In your call app: Share -> Window -> \"%ls\".", kMirrorTitle);
    }
    catch (winrt::hresult_error const& e)
    {
        logf(L"[share] failed 0x%08X: %ls", static_cast<unsigned>(e.code()), e.message().c_str());
        StopShare();
    }
}

static void StopShare()
{
    g_active = false;
    g_closing = true;
    if (g_session) { g_session.Close(); g_session = nullptr; }
    if (g_pool)    { g_pool.Close();    g_pool = nullptr; }
    g_item = nullptr;
    g_swap = nullptr;

    HWND mirror = g_mirror; g_mirror = nullptr;
    if (mirror) DestroyWindow(mirror);
    HWND marker = g_marker; g_marker = nullptr;
    if (marker) DestroyWindow(marker);
    logf(L"[share] stopped.");
}

// ===========================================================================
//  Entry point
// ===========================================================================
int wmain(int argc, wchar_t** argv)
{
    g_inst = GetModuleHandleW(nullptr);
    EnablePerMonitorDpi();

    RECT startRect{}; bool haveRect = false; int seconds = 0;
    for (int i = 1; i < argc; ++i)
    {
        if (!_wcsicmp(argv[i], L"--rect") && i + 4 < argc)
        {
            startRect.left = _wtol(argv[i + 1]); startRect.top = _wtol(argv[i + 2]);
            startRect.right = startRect.left + _wtol(argv[i + 3]);
            startRect.bottom = startRect.top + _wtol(argv[i + 4]);
            haveRect = true; i += 4;
        }
        else if (!_wcsicmp(argv[i], L"--seconds") && i + 1 < argc) { seconds = _wtoi(argv[i + 1]); i += 1; }
    }

    winrt::init_apartment(winrt::apartment_type::single_threaded);
    if (!wgc::GraphicsCaptureSession::IsSupported())
    {
        logf(L"[fatal] Windows.Graphics.Capture is not supported here.");
        return 1;
    }

    g_d3d = CreateD3DDevice(g_ctx);
    g_winrtDevice = CreateWinrtDevice(g_d3d);

    RegisterHotKey(nullptr, 1, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, 'R');
    RegisterHotKey(nullptr, 2, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, 'Q');

    logf(L"Screen Region Share - share PoC");
    logf(L"  Ctrl+Shift+R : draw a region and start sharing (press again to stop)");
    logf(L"  Ctrl+Shift+Q : quit");
    logf(L"  Drag any border/corner of the red frame to resize.");

    if (haveRect) StartShare(startRect);
    if (seconds > 0) SetTimer(nullptr, TIMER_AUTOEXIT, seconds * 1000, nullptr);

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0))
    {
        if (msg.message == WM_HOTKEY)
        {
            if (msg.wParam == 1)
            {
                if (g_active) StopShare();
                else { RECT r; if (SelectRegion(r)) StartShare(r); }
            }
            else if (msg.wParam == 2) break;
            continue;
        }
        if (msg.message == WM_TIMER && msg.wParam == TIMER_AUTOEXIT) { logf(L"[info] auto-exit."); break; }
        TranslateMessage(&msg); DispatchMessage(&msg);
    }

    StopShare();
    UnregisterHotKey(nullptr, 1);
    UnregisterHotKey(nullptr, 2);
    logf(L"[info] exit.");
    return 0;
}
