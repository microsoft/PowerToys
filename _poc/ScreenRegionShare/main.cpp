//==============================================================================
//
// Screen Region Share - Proof of Concept
//
// Validates: WGC live capture of an arbitrary screen region, cropped and
// presented into a normal top-level window that Teams can share via
// "Share -> Window". Also exercises the recursion behavior:
//   * default      -> capture the top-level WINDOW under the selection
//                     (mirror window is not part of the captured item, so
//                      no feedback loop)
//   * --monitor    -> capture the whole MONITOR (mirror on that monitor
//                     will be captured -> intentional recursion demo)
//
// Single file, no NuGet. Build with cl.exe + generated C++/WinRT headers.
//
//==============================================================================

#include <windows.h>
#include <windowsx.h>
#include <dwmapi.h>
#include <d3d11.h>
#include <dxgi1_6.h>
#include <inspectable.h>
#include <cstdio>
#include <algorithm>
#include <string>

// C++/WinRT (headers generated into .\generated\winrt by cppwinrt.exe)
#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Metadata.h>
#include <winrt/Windows.Graphics.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>

// Interop (from the Windows SDK)
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "dwmapi.lib")
#pragma comment(lib, "windowsapp.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "gdi32.lib")

namespace wgc = winrt::Windows::Graphics::Capture;
namespace wgdx = winrt::Windows::Graphics::DirectX;
using winrt::Windows::Foundation::Metadata::ApiInformation;

// ---------------------------------------------------------------------------
// Globals / small helpers
// ---------------------------------------------------------------------------
static const wchar_t* kMirrorTitle = L"PowerToys \u2014 Shared Region (PoC)";
static const UINT WM_APP_FRAME = WM_APP + 1;

static void logf(const wchar_t* fmt, ...)
{
    wchar_t buf[1024];
    va_list args;
    va_start(args, fmt);
    _vsnwprintf_s(buf, _countof(buf), _TRUNCATE, fmt, args);
    va_end(args);
    wprintf(L"%s\n", buf);
    fflush(stdout);
}

template <typename T>
static T clampv(T v, T lo, T hi) { return v < lo ? lo : (v > hi ? hi : v); }

// ---------------------------------------------------------------------------
// Region selection overlay (drag a rectangle across the virtual desktop)
// ---------------------------------------------------------------------------
struct OverlayState
{
    HWND    hwnd = nullptr;
    bool    dragging = false;
    bool    done = false;
    bool    cancelled = false;
    POINT   origin{};   // virtual-desktop top-left (screen coords of client 0,0)
    POINT   start{};    // in client coords
    POINT   cur{};      // in client coords
};

static LRESULT CALLBACK OverlayProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    auto* s = reinterpret_cast<OverlayState*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    switch (msg)
    {
    case WM_LBUTTONDOWN:
        s->dragging = true;
        s->start = { GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
        s->cur = s->start;
        SetCapture(hwnd);
        InvalidateRect(hwnd, nullptr, FALSE);
        return 0;
    case WM_MOUSEMOVE:
        if (s->dragging)
        {
            s->cur = { GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
            InvalidateRect(hwnd, nullptr, FALSE);
        }
        return 0;
    case WM_LBUTTONUP:
        if (s->dragging)
        {
            s->dragging = false;
            s->cur = { GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
            ReleaseCapture();
            s->done = true;
            PostQuitMessage(0);
        }
        return 0;
    case WM_KEYDOWN:
        if (wp == VK_ESCAPE) { s->cancelled = true; s->done = true; PostQuitMessage(0); }
        return 0;
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd, &ps);
        RECT client; GetClientRect(hwnd, &client);
        // Dim background
        HBRUSH dim = CreateSolidBrush(RGB(20, 20, 24));
        FillRect(hdc, &client, dim);
        DeleteObject(dim);
        if (s->dragging || s->done)
        {
            RECT sel{ std::min(s->start.x, s->cur.x), std::min(s->start.y, s->cur.y),
                      std::max(s->start.x, s->cur.x), std::max(s->start.y, s->cur.y) };
            HBRUSH inside = CreateSolidBrush(RGB(60, 90, 140));
            FillRect(hdc, &sel, inside);
            DeleteObject(inside);
            HPEN pen = CreatePen(PS_SOLID, 2, RGB(120, 200, 255));
            HGDIOBJ oldPen = SelectObject(hdc, pen);
            HGDIOBJ oldBr = SelectObject(hdc, GetStockObject(NULL_BRUSH));
            Rectangle(hdc, sel.left, sel.top, sel.right, sel.bottom);
            SelectObject(hdc, oldPen); SelectObject(hdc, oldBr);
            DeleteObject(pen);
        }
        EndPaint(hwnd, &ps);
        return 0;
    }
    default:
        return DefWindowProcW(hwnd, msg, wp, lp);
    }
}

// Runs a modal overlay; returns true and fills outRect (screen coords) on success.
static bool SelectRegion(RECT& outRect)
{
    WNDCLASSEXW wc{ sizeof(wc) };
    wc.lpfnWndProc = OverlayProc;
    wc.hInstance = GetModuleHandleW(nullptr);
    wc.hCursor = LoadCursorW(nullptr, IDC_CROSS);
    wc.lpszClassName = L"ScreenRegionShare.Overlay";
    RegisterClassExW(&wc);

    int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
    int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
    int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
    int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);

    OverlayState s;
    s.origin = { vx, vy };

    HWND hwnd = CreateWindowExW(
        WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
        wc.lpszClassName, L"", WS_POPUP,
        vx, vy, vw, vh, nullptr, nullptr, wc.hInstance, nullptr);
    if (!hwnd) return false;
    s.hwnd = hwnd;
    SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(&s));
    SetLayeredWindowAttributes(hwnd, 0, 110, LWA_ALPHA);
    ShowWindow(hwnd, SW_SHOW);
    SetForegroundWindow(hwnd);

    logf(L"[overlay] Drag to select a region. Press ESC to cancel.");

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
        if (s.done) break;
    }
    DestroyWindow(hwnd);
    UnregisterClassW(wc.lpszClassName, wc.hInstance);

    if (s.cancelled) return false;

    RECT r{ std::min(s.start.x, s.cur.x) + s.origin.x,
            std::min(s.start.y, s.cur.y) + s.origin.y,
            std::max(s.start.x, s.cur.x) + s.origin.x,
            std::max(s.start.y, s.cur.y) + s.origin.y };
    if (r.right - r.left < 8 || r.bottom - r.top < 8) return false;
    outRect = r;
    return true;
}

// ---------------------------------------------------------------------------
// D3D / WGC helpers
// ---------------------------------------------------------------------------
static winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice
CreateWinrtDevice(winrt::com_ptr<ID3D11Device> const& d3d)
{
    auto dxgi = d3d.as<IDXGIDevice>();
    winrt::com_ptr<IInspectable> inspectable;
    winrt::check_hresult(CreateDirect3D11DeviceFromDXGIDevice(dxgi.get(), inspectable.put()));
    return inspectable.as<winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice>();
}

static winrt::com_ptr<ID3D11Texture2D>
TextureFromSurface(winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DSurface const& surface)
{
    auto access = surface.as<::Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess>();
    winrt::com_ptr<ID3D11Texture2D> tex;
    winrt::check_hresult(access->GetInterface(winrt::guid_of<ID3D11Texture2D>(), tex.put_void()));
    return tex;
}

// ---------------------------------------------------------------------------
// Mirror window: presents cropped frames via a DXGI swapchain
// ---------------------------------------------------------------------------
struct MirrorState
{
    winrt::com_ptr<ID3D11Device>        d3d;
    winrt::com_ptr<ID3D11DeviceContext> ctx;
    winrt::com_ptr<IDXGISwapChain1>     swap;
    wgc::Direct3D11CaptureFramePool     pool{ nullptr };
    wgc::GraphicsCaptureSession         session{ nullptr };
    // Crop origin in the captured surface's coordinate space (physical px)
    LONG cropX = 0, cropY = 0, cropW = 0, cropH = 0;
    bool closing = false;
};

static void RenderFrame(MirrorState* m)
{
    if (m->closing || !m->pool) return;
    auto frame = m->pool.TryGetNextFrame();
    if (!frame) return;

    auto srcTex = TextureFromSurface(frame.Surface());
    D3D11_TEXTURE2D_DESC sd{};
    srcTex->GetDesc(&sd);

    winrt::com_ptr<ID3D11Texture2D> back;
    winrt::check_hresult(m->swap->GetBuffer(0, winrt::guid_of<ID3D11Texture2D>(), back.put_void()));
    D3D11_TEXTURE2D_DESC bd{};
    back->GetDesc(&bd);

    // Effective copy size, clamped to the source surface and the back buffer.
    LONG availW = static_cast<LONG>(sd.Width) - m->cropX;
    LONG availH = static_cast<LONG>(sd.Height) - m->cropY;
    LONG w = clampv<LONG>(std::min<LONG>(m->cropW, availW), 0, static_cast<LONG>(bd.Width));
    LONG h = clampv<LONG>(std::min<LONG>(m->cropH, availH), 0, static_cast<LONG>(bd.Height));

    if (w > 0 && h > 0)
    {
        D3D11_BOX box{};
        box.left = clampv<UINT>(static_cast<UINT>(m->cropX), 0, sd.Width);
        box.top = clampv<UINT>(static_cast<UINT>(m->cropY), 0, sd.Height);
        box.right = box.left + static_cast<UINT>(w);
        box.bottom = box.top + static_cast<UINT>(h);
        box.front = 0; box.back = 1;
        m->ctx->CopySubresourceRegion(back.get(), 0, 0, 0, 0, srcTex.get(), 0, &box);
    }
    back = nullptr; // release before Present (FLIP requirement)
    m->swap->Present(1, 0);
    frame.Close();
}

static LRESULT CALLBACK MirrorProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    auto* m = reinterpret_cast<MirrorState*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    switch (msg)
    {
    case WM_APP_FRAME:
        if (m) RenderFrame(m);
        return 0;
    case WM_TIMER:
        DestroyWindow(hwnd);
        return 0;
    case WM_SIZE:
        if (m && m->swap && wp != SIZE_MINIMIZED)
        {
            UINT cw = LOWORD(lp), ch = HIWORD(lp);
            if (cw && ch)
                m->swap->ResizeBuffers(0, cw, ch, DXGI_FORMAT_UNKNOWN, 0);
        }
        return 0;
    case WM_DESTROY:
        if (m) m->closing = true;
        PostQuitMessage(0);
        return 0;
    default:
        return DefWindowProcW(hwnd, msg, wp, lp);
    }
}

static winrt::com_ptr<ID3D11Device> CreateD3DDevice(winrt::com_ptr<ID3D11DeviceContext>& ctx)
{
    winrt::com_ptr<ID3D11Device> d3d;
    UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
    HRESULT hr = D3D11CreateDevice(
        nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags,
        nullptr, 0, D3D11_SDK_VERSION, d3d.put(), nullptr, ctx.put());
    if (FAILED(hr))
    {
        logf(L"[warn] Hardware D3D device failed (0x%08X); falling back to WARP.", hr);
        d3d = nullptr; ctx = nullptr;
        winrt::check_hresult(D3D11CreateDevice(
            nullptr, D3D_DRIVER_TYPE_WARP, nullptr, flags,
            nullptr, 0, D3D11_SDK_VERSION, d3d.put(), nullptr, ctx.put()));
    }
    return d3d;
}

// Non-intrusive validation of the full capture pipeline: device -> monitor
// capture item -> frame pool/session -> one cropped frame. No visible UI.
static int RunSelfTest()
{
    logf(L"[selftest] GraphicsCaptureSession supported: %d", wgc::GraphicsCaptureSession::IsSupported());
    winrt::com_ptr<ID3D11DeviceContext> ctx;
    auto d3d = CreateD3DDevice(ctx);
    auto winrtDevice = CreateWinrtDevice(d3d);
    logf(L"[selftest] D3D + WinRT device created.");

    POINT p{ 0, 0 };
    HMONITOR hmon = MonitorFromPoint(p, MONITOR_DEFAULTTOPRIMARY);
    auto interop = winrt::get_activation_factory<wgc::GraphicsCaptureItem, IGraphicsCaptureItemInterop>();
    wgc::GraphicsCaptureItem item{ nullptr };
    winrt::check_hresult(interop->CreateForMonitor(
        hmon, winrt::guid_of<wgc::GraphicsCaptureItem>(), winrt::put_abi(item)));
    auto sz = item.Size();
    logf(L"[selftest] Monitor capture item created: %dx%d", sz.Width, sz.Height);

    auto pool = wgc::Direct3D11CaptureFramePool::CreateFreeThreaded(
        winrtDevice, wgdx::DirectXPixelFormat::B8G8R8A8UIntNormalized, 2, sz);
    auto session = pool.CreateCaptureSession(item);
    if (ApiInformation::IsPropertyPresent(
            winrt::name_of<wgc::GraphicsCaptureSession>(), L"IsBorderRequired"))
        session.IsBorderRequired(false);

    winrt::handle frameEvent{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
    HANDLE ev = frameEvent.get();
    pool.FrameArrived([ev](auto&&, auto&&) { SetEvent(ev); });
    session.StartCapture();

    int rc = 1;
    if (WaitForSingleObject(ev, 3000) == WAIT_OBJECT_0)
    {
        if (auto frame = pool.TryGetNextFrame())
        {
            auto tex = TextureFromSurface(frame.Surface());
            D3D11_TEXTURE2D_DESC desc{};
            tex->GetDesc(&desc);
            logf(L"[selftest] Frame captured: %ux%u. Crop path OK.", desc.Width, desc.Height);
            frame.Close();
            rc = 0;
        }
    }
    logf(L"[selftest] %s", rc == 0 ? L"PASS" : L"FAIL (no frame within 3s)");
    session.Close();
    pool.Close();
    return rc;
}

// ---------------------------------------------------------------------------
// main
// ---------------------------------------------------------------------------
int wmain(int argc, wchar_t** argv)
{
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    bool forceMonitor = false;
    bool selfTest = false;
    bool haveRect = false;
    RECT argRect{};
    int autoCloseSeconds = 0;
    for (int i = 1; i < argc; ++i)
    {
        if (_wcsicmp(argv[i], L"--monitor") == 0) forceMonitor = true;
        else if (_wcsicmp(argv[i], L"--selftest") == 0) selfTest = true;
        else if (_wcsicmp(argv[i], L"--rect") == 0 && i + 4 < argc)
        {
            LONG x = _wtol(argv[i + 1]), y = _wtol(argv[i + 2]);
            LONG w = _wtol(argv[i + 3]), h = _wtol(argv[i + 4]);
            argRect = { x, y, x + w, y + h };
            haveRect = true;
            i += 4;
        }
        else if (_wcsicmp(argv[i], L"--seconds") == 0 && i + 1 < argc)
        {
            autoCloseSeconds = _wtoi(argv[i + 1]);
            i += 1;
        }
    }

    winrt::init_apartment(winrt::apartment_type::single_threaded);

    if (!wgc::GraphicsCaptureSession::IsSupported())
    {
        logf(L"[fatal] Windows.Graphics.Capture is not supported on this system.");
        return 1;
    }

    if (selfTest)
        return RunSelfTest();

    // 1) Select region (drag overlay) unless a rect was passed on the command line
    RECT sel{};
    if (haveRect)
    {
        sel = argRect;
        logf(L"[info] Using --rect region.");
    }
    else if (!SelectRegion(sel))
    {
        logf(L"[info] Selection cancelled.");
        return 0;
    }
    logf(L"[info] Selection (screen px): %d,%d  %dx%d",
        sel.left, sel.top, sel.right - sel.left, sel.bottom - sel.top);

    POINT center{ (sel.left + sel.right) / 2, (sel.top + sel.bottom) / 2 };

    try {

    // 2) Decide capture source (window vs monitor)
    HWND targetWindow = nullptr;
    if (!forceMonitor)
    {
        HWND under = WindowFromPoint(center);
        if (under)
        {
            HWND root = GetAncestor(under, GA_ROOT);
            wchar_t cls[64]{};
            GetClassNameW(root, cls, _countof(cls));
            // Skip the shell desktop so we fall back to monitor capture there.
            if (root && wcscmp(cls, L"Progman") != 0 && wcscmp(cls, L"WorkerW") != 0)
                targetWindow = root;
        }
    }

    // 3) D3D device
    winrt::com_ptr<ID3D11DeviceContext> ctx;
    winrt::com_ptr<ID3D11Device> d3d = CreateD3DDevice(ctx);
    auto winrtDevice = CreateWinrtDevice(d3d);

    // 4) Capture item + crop origin in the item's surface space
    wgc::GraphicsCaptureItem item{ nullptr };
    auto interop = winrt::get_activation_factory<wgc::GraphicsCaptureItem, IGraphicsCaptureItemInterop>();

    LONG cropX = 0, cropY = 0;
    if (targetWindow)
    {
        RECT efb{};
        DwmGetWindowAttribute(targetWindow, DWMWA_EXTENDED_FRAME_BOUNDS, &efb, sizeof(efb));
        cropX = sel.left - efb.left;
        cropY = sel.top - efb.top;
        wchar_t title[256]{}; GetWindowTextW(targetWindow, title, _countof(title));
        logf(L"[mode] WINDOW capture (no recursion). Target: \"%s\"  frame @ %d,%d", title, efb.left, efb.top);
        winrt::check_hresult(interop->CreateForWindow(
            targetWindow, winrt::guid_of<wgc::GraphicsCaptureItem>(), winrt::put_abi(item)));
    }
    else
    {
        HMONITOR hmon = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
        MONITORINFO mi{ sizeof(mi) };
        GetMonitorInfoW(hmon, &mi);
        cropX = sel.left - mi.rcMonitor.left;
        cropY = sel.top - mi.rcMonitor.top;
        logf(L"[mode] MONITOR capture (mirror on this monitor WILL recurse). Monitor @ %d,%d",
            mi.rcMonitor.left, mi.rcMonitor.top);
        winrt::check_hresult(interop->CreateForMonitor(
            hmon, winrt::guid_of<wgc::GraphicsCaptureItem>(), winrt::put_abi(item)));
    }

    LONG cropW = sel.right - sel.left;
    LONG cropH = sel.bottom - sel.top;

    // 5) Mirror window sized to the crop
    WNDCLASSEXW wc{ sizeof(wc) };
    wc.lpfnWndProc = MirrorProc;
    wc.hInstance = GetModuleHandleW(nullptr);
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.hbrBackground = reinterpret_cast<HBRUSH>(GetStockObject(BLACK_BRUSH));
    wc.lpszClassName = L"ScreenRegionShare.Mirror";
    RegisterClassExW(&wc);

    RECT wr{ 0, 0, cropW, cropH };
    DWORD style = WS_OVERLAPPEDWINDOW;
    AdjustWindowRect(&wr, style, FALSE);
    HWND mirror = CreateWindowExW(
        WS_EX_TOPMOST, wc.lpszClassName, kMirrorTitle, style,
        CW_USEDEFAULT, CW_USEDEFAULT, wr.right - wr.left, wr.bottom - wr.top,
        nullptr, nullptr, wc.hInstance, nullptr);

    MirrorState m;
    m.d3d = d3d; m.ctx = ctx;
    m.cropX = cropX; m.cropY = cropY; m.cropW = cropW; m.cropH = cropH;
    SetWindowLongPtrW(mirror, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(&m));

    // 6) Swapchain for the mirror window
    auto dxgiDevice = d3d.as<IDXGIDevice>();
    winrt::com_ptr<IDXGIAdapter> adapter;
    winrt::check_hresult(dxgiDevice->GetAdapter(adapter.put()));
    winrt::com_ptr<IDXGIFactory2> factory;
    winrt::check_hresult(adapter->GetParent(winrt::guid_of<IDXGIFactory2>(), factory.put_void()));

    DXGI_SWAP_CHAIN_DESC1 scd{};
    scd.Width = static_cast<UINT>(std::max<LONG>(cropW, 1));
    scd.Height = static_cast<UINT>(std::max<LONG>(cropH, 1));
    scd.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    scd.SampleDesc.Count = 1;
    scd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    scd.BufferCount = 2;
    scd.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
    winrt::check_hresult(factory->CreateSwapChainForHwnd(
        d3d.get(), mirror, &scd, nullptr, nullptr, m.swap.put()));

    ShowWindow(mirror, SW_SHOW);
    // Force visibility + foreground regardless of the launching STARTUPINFO
    // (the first ShowWindow call otherwise honors the process's wShowWindow).
    SetWindowPos(mirror, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
    SetForegroundWindow(mirror);

    // 7) Capture session; frames post a message to the UI thread to render
    m.pool = wgc::Direct3D11CaptureFramePool::CreateFreeThreaded(
        winrtDevice, wgdx::DirectXPixelFormat::B8G8R8A8UIntNormalized, 2, item.Size());
    m.session = m.pool.CreateCaptureSession(item);
    if (ApiInformation::IsPropertyPresent(
            winrt::name_of<wgc::GraphicsCaptureSession>(), L"IsBorderRequired"))
        m.session.IsBorderRequired(false);
    if (ApiInformation::IsPropertyPresent(
            winrt::name_of<wgc::GraphicsCaptureSession>(), L"IsCursorCaptureEnabled"))
        m.session.IsCursorCaptureEnabled(true);

    m.pool.FrameArrived([mirror](auto&&, auto&&) {
        PostMessageW(mirror, WM_APP_FRAME, 0, 0);
    });
    m.session.StartCapture();

    if (autoCloseSeconds > 0)
    {
        SetTimer(mirror, 1, static_cast<UINT>(autoCloseSeconds) * 1000, nullptr);
        logf(L"[info] Auto-closing in %d seconds.", autoCloseSeconds);
    }

    logf(L"[info] Mirror window '%ls' is live. In Teams: Share -> Window -> pick it.", kMirrorTitle);
    logf(L"[info] Close the mirror window to exit.");

    // 8) Pump messages
    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    m.closing = true;
    if (m.session) m.session.Close();
    if (m.pool) m.pool.Close();
    }
    catch (winrt::hresult_error const& e)
    {
        logf(L"[fatal] HRESULT 0x%08X: %ls", static_cast<unsigned>(e.code()), e.message().c_str());
        return 2;
    }
    return 0;
}
