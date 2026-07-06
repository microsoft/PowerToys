// overlay.cpp — PoC of the "region marker" UX for Screen Region Share.
//
// Goal: nail the *feel* before wiring up any Teams/capture plumbing.
//   * Global hotkey (Ctrl+Shift+R) starts a drag-select.
//   * You drag a rectangle; on release a persistent RED HOLLOW FRAME stays in place.
//   * The interior is a real hole -> fully click-through (interact with apps beneath).
//   * Resize from the corners; move by dragging the edges.
//   * Press the hotkey again to dismiss it (toggle).
//   * Ctrl+Shift+Q quits the demo.
//
// This is a throwaway PoC (pure Win32, no WinRT / D3D) so it builds in ~1s.

#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <windowsx.h>
#include <cstdio>
#include <algorithm>

// ---- tunables -------------------------------------------------------------
static const int   BORDER = 4;    // visible frame thickness (px)
static const int   GRAB   = 18;   // corner grab-zone size (px)
static const int   MINW   = 80;   // min marker width
static const int   MINH   = 60;   // min marker height
static const COLORREF FRAME_COLOR = RGB(232, 17, 35); // PowerToys-ish red

static const UINT_PTR TIMER_KEEPTOP  = 1;      // marker: re-assert always-on-top
static const UINT_PTR TIMER_AUTOEXIT = 0xBEEF; // optional --seconds auto-close

// ---- globals --------------------------------------------------------------
static HINSTANCE g_inst   = nullptr;
static HWND      g_marker = nullptr;

static void Log(const char* s) { printf("%s\n", s); fflush(stdout); }

// DPI: opt into per-monitor v2 so every coordinate we touch is physical px.
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
    bool  dragging = false;
    bool  done     = false;
    bool  cancel   = false;
    POINT start    = {};
    POINT cur      = {};
    int   vx = 0, vy = 0; // virtual-screen origin (client (0,0) maps here)
};

static LRESULT CALLBACK SelProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp)
{
    auto* st = reinterpret_cast<SelState*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    switch (msg)
    {
    case WM_SETCURSOR:
        SetCursor(LoadCursorW(nullptr, IDC_CROSS));
        return TRUE;

    case WM_LBUTTONDOWN:
        st->dragging = true;
        st->start = { GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
        st->cur   = st->start;
        SetCapture(hwnd);
        return 0;

    case WM_MOUSEMOVE:
        if (st->dragging)
        {
            st->cur = { GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
            InvalidateRect(hwnd, nullptr, TRUE);
        }
        return 0;

    case WM_LBUTTONUP:
        if (st->dragging)
        {
            st->dragging = false;
            st->cur = { GET_X_LPARAM(lp), GET_Y_LPARAM(lp) };
            ReleaseCapture();
            st->done = true;
        }
        return 0;

    case WM_KEYDOWN:
        if (wp == VK_ESCAPE) { st->cancel = true; }
        return 0;

    case WM_ERASEBKGND:
        return 1; // painted in WM_PAINT

    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd, &ps);
        RECT rc; GetClientRect(hwnd, &rc);

        // Dim veil (the whole window is layered at ~alpha 90).
        HBRUSH bg = CreateSolidBrush(RGB(20, 20, 20));
        FillRect(hdc, &rc, bg);
        DeleteObject(bg);

        if (st->dragging)
        {
            RECT s{
                std::min(st->start.x, st->cur.x),
                std::min(st->start.y, st->cur.y),
                std::max(st->start.x, st->cur.x),
                std::max(st->start.y, st->cur.y)
            };
            HBRUSH red = CreateSolidBrush(FRAME_COLOR);
            RECT t;
            t = { s.left, s.top, s.right, s.top + 3 };                 FillRect(hdc, &t, red);
            t = { s.left, s.bottom - 3, s.right, s.bottom };           FillRect(hdc, &t, red);
            t = { s.left, s.top, s.left + 3, s.bottom };               FillRect(hdc, &t, red);
            t = { s.right - 3, s.top, s.right, s.bottom };             FillRect(hdc, &t, red);
            DeleteObject(red);
        }
        EndPaint(hwnd, &ps);
        return 0;
    }
    }
    return DefWindowProcW(hwnd, msg, wp, lp);
}

// Returns true and fills `out` (screen px) if the user picked a region.
static bool SelectRegion(RECT& out)
{
    static bool registered = false;
    if (!registered)
    {
        WNDCLASSW wc{};
        wc.lpfnWndProc   = SelProc;
        wc.hInstance     = g_inst;
        wc.lpszClassName = L"SRS_SelOverlay";
        wc.hCursor       = LoadCursorW(nullptr, IDC_CROSS);
        wc.hbrBackground = nullptr;
        RegisterClassW(&wc);
        registered = true;
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
    ShowWindow(hwnd, SW_SHOW);
    SetForegroundWindow(hwnd);
    Log("[select] Drag a region (Esc to cancel)...");

    MSG m;
    while (!st.done && !st.cancel && GetMessageW(&m, nullptr, 0, 0))
    {
        if (m.message == WM_HOTKEY) continue; // ignore hotkeys mid-select
        TranslateMessage(&m);
        DispatchMessage(&m);
    }
    DestroyWindow(hwnd);

    if (st.cancel) { Log("[select] cancelled."); return false; }

    RECT r{
        std::min(st.start.x, st.cur.x) + st.vx,
        std::min(st.start.y, st.cur.y) + st.vy,
        std::max(st.start.x, st.cur.x) + st.vx,
        std::max(st.start.y, st.cur.y) + st.vy
    };
    if ((r.right - r.left) < 8 || (r.bottom - r.top) < 8)
    {
        Log("[select] region too small; ignored.");
        return false;
    }
    out = r;
    return true;
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
    int b = BORDER;
    int ix = std::min(b, w / 2);
    int iy = std::min(b, h / 2);
    HRGN inner = CreateRectRgn(ix, iy, w - ix, h - iy);
    CombineRgn(outer, outer, inner, RGN_DIFF); // ring = outer - inner
    DeleteObject(inner);
    SetWindowRgn(hwnd, outer, TRUE); // window takes ownership of `outer`
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
        bool left   = pt.x < GRAB;
        bool right  = pt.x >= rc.right  - GRAB;
        bool top    = pt.y < GRAB;
        bool bottom = pt.y >= rc.bottom - GRAB;
        // Resize-only: every border/corner resizes; the frame does not move.
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
        mmi->ptMinTrackSize.x = MINW;
        mmi->ptMinTrackSize.y = MINH;
        return 0;
    }

    case WM_SIZE:
        UpdateFrameRegion(hwnd);
        InvalidateRect(hwnd, nullptr, TRUE);
        return 0;

    case WM_TIMER:
        // Keep the marker reliably on top; it must never auto-dismiss on an
        // outside click. If something stole always-on-top, re-assert it.
        if (wp == TIMER_KEEPTOP)
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                         SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        return 0;

    case WM_ERASEBKGND:
        return 1;

    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd, &ps);
        RECT rc; GetClientRect(hwnd, &rc);
        HBRUSH red = CreateSolidBrush(FRAME_COLOR);
        FillRect(hdc, &rc, red); // region clips this to the ring only
        DeleteObject(red);
        EndPaint(hwnd, &ps);
        return 0;
    }

    case WM_DESTROY:
        KillTimer(hwnd, TIMER_KEEPTOP);
        Log("[marker] destroyed (WM_DESTROY).");
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
        wc.lpfnWndProc   = MarkerProc;
        wc.hInstance     = g_inst;
        wc.lpszClassName = L"SRS_Marker";
        wc.hCursor       = LoadCursorW(nullptr, IDC_ARROW);
        wc.hbrBackground = nullptr;
        RegisterClassW(&wc);
        registered = true;
    }

    int w = r.right - r.left;
    int h = r.bottom - r.top;
    g_marker = CreateWindowExW(
        WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
        L"SRS_Marker", L"PowerToys - Shared Region (PoC)",
        WS_POPUP,
        r.left, r.top, w, h, nullptr, nullptr, g_inst, nullptr);
    if (!g_marker) { Log("[marker] CreateWindow failed."); return; }

    UpdateFrameRegion(g_marker);
    ShowWindow(g_marker, SW_SHOWNOACTIVATE);
    SetWindowPos(g_marker, HWND_TOPMOST, 0, 0, 0, 0,
                 SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
    SetTimer(g_marker, TIMER_KEEPTOP, 400, nullptr); // keep-on-top heartbeat
    char buf[128];
    sprintf_s(buf, "[marker] live at %d,%d %dx%d (drag any border/corner to resize)",
              r.left, r.top, w, h);
    Log(buf);
}

// ===========================================================================
//  Entry point
// ===========================================================================
int wmain(int argc, wchar_t** argv)
{
    g_inst = GetModuleHandleW(nullptr);
    EnablePerMonitorDpi();

    // Optional: spawn a marker immediately (for screenshot verification /
    // when you can't drag interactively). --rect x y w h
    RECT startRect{};
    bool haveRect = false;
    int  seconds  = 0;
    for (int i = 1; i < argc; ++i)
    {
        if (!wcscmp(argv[i], L"--rect") && i + 4 < argc)
        {
            startRect.left   = _wtoi(argv[i + 1]);
            startRect.top    = _wtoi(argv[i + 2]);
            startRect.right  = startRect.left + _wtoi(argv[i + 3]);
            startRect.bottom = startRect.top  + _wtoi(argv[i + 4]);
            haveRect = true; i += 4;
        }
        else if (!wcscmp(argv[i], L"--seconds") && i + 1 < argc)
        {
            seconds = _wtoi(argv[i + 1]); i += 1;
        }
    }

    RegisterHotKey(nullptr, 1, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, 'R');
    RegisterHotKey(nullptr, 2, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, 'Q');

    Log("Screen Region Share - overlay PoC");
    Log("  Ctrl+Shift+R : draw a region  (press again to dismiss)");
    Log("  Ctrl+Shift+Q : quit");
    Log("  Drag any border/corner to resize");

    if (haveRect) CreateMarker(startRect);
    if (seconds > 0) SetTimer(nullptr, TIMER_AUTOEXIT, seconds * 1000, nullptr);

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0))
    {
        if (msg.message == WM_HOTKEY)
        {
            if (msg.wParam == 1) // toggle marker
            {
                if (g_marker)
                {
                    DestroyWindow(g_marker);
                    g_marker = nullptr;
                    Log("[marker] dismissed.");
                }
                else
                {
                    RECT r;
                    if (SelectRegion(r)) CreateMarker(r);
                }
            }
            else if (msg.wParam == 2) // quit
            {
                break;
            }
            continue;
        }
        if (msg.message == WM_TIMER && msg.wParam == TIMER_AUTOEXIT)
        {
            Log("[info] auto-exit timer elapsed.");
            break;
        }
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    if (g_marker) DestroyWindow(g_marker);
    UnregisterHotKey(nullptr, 1);
    UnregisterHotKey(nullptr, 2);
    Log("[info] exit.");
    return 0;
}
