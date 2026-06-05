// AltWindowCycle.cpp
// Adapts the POC overlay (C:\Users\crutkas\source\AltWindowCycle\cpp\AltWindowCycle.cpp)
// for the PowerToys in-proc module.  All overlay windows and the Switcher state machine
// live on a dedicated UI thread; the runner's on_hotkey callback only posts a message.

#include "pch.h"

#include "AltWindowCycle.h"

#include <uxtheme.h>
#include <shellscalingapi.h>
#include <objidl.h>
#include <gdiplus.h>
#include <cwchar>
#include <cstdlib>
#include <atomic>

// Win11 system backdrop / corner attributes (in case SDK is older).
#ifndef DWMWA_SYSTEMBACKDROP_TYPE
#define DWMWA_SYSTEMBACKDROP_TYPE 38
#endif
#ifndef DWMSBT_TRANSIENTWINDOW
#define DWMSBT_TRANSIENTWINDOW 3
#endif
#ifndef DWMWA_CLOAKED
#define DWMWA_CLOAKED 14
#endif
#ifndef DWMWA_WINDOW_CORNER_PREFERENCE
#define DWMWA_WINDOW_CORNER_PREFERENCE 33
#endif
#ifndef DWMWCP_ROUND
#define DWMWCP_ROUND 2
#endif
#ifndef DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
#define DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 ((DPI_AWARENESS_CONTEXT)-4)
#endif
#ifndef PW_RENDERFULLCONTENT
#define PW_RENDERFULLCONTENT 0x00000002
#endif

// Undocumented but stable composition API used by the shell for acrylic.
enum ACCENT_STATE
{
    ACCENT_DISABLED = 0,
    ACCENT_ENABLE_BLURBEHIND = 3,
    ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
};
struct ACCENT_POLICY
{
    DWORD AccentState;
    DWORD AccentFlags;
    DWORD GradientColor; // 0xAABBGGRR
    DWORD AnimationId;
};
struct WINDOWCOMPOSITIONATTRIBDATA
{
    DWORD Attrib;
    PVOID pvData;
    SIZE_T cbData;
};
#define WCA_ACCENT_POLICY 19
typedef BOOL(WINAPI* PFN_SetWindowCompositionAttribute)(HWND, WINDOWCOMPOSITIONATTRIBDATA*);

namespace AltTabStyle
{
    // Alt-Tab In-App Acrylic Thin: TintColor=#545454, TintOpacity=0,
    // TintLuminosityOpacity=0.64, FallbackColor=#202020, CornerRadius=8.
    constexpr DWORD AcrylicThinGradientABGR = 0x00545454;
    constexpr COLORREF AcrylicThinFallbackRef = RGB(32, 32, 32);

    inline COLORREF DebugMagentaRef() { return RGB(255, 0, 255); }
    inline COLORREF AccentFallbackRef() { return RGB(0, 120, 215); }
    constexpr COLORREF HeaderTextRef(bool selected)
    {
        return selected ? RGB(255, 255, 255) : RGB(207, 207, 207);
    }

    inline Gdiplus::Color Transparent() { return Gdiplus::Color(0, 0, 0, 0); }
    inline Gdiplus::Color TranslucentBackdrop() { return Gdiplus::Color(180, 0, 255, 0); }
    inline Gdiplus::Color DebugMagenta() { return Gdiplus::Color(255, 255, 0, 255); }
    inline Gdiplus::Color WhitePreview() { return Gdiplus::Color(255, 255, 255, 255); }
    inline Gdiplus::Color Card(bool selected)
    {
        return selected ? Gdiplus::Color(255, 58, 58, 62)
                        : Gdiplus::Color(255, 43, 43, 46);
    }
    inline Gdiplus::Color PreviewMask(bool selected, bool magentaDebug, bool translucentBackdrop)
    {
        if (translucentBackdrop)
            return Gdiplus::Color(255, 0, 255, 0);
        if (magentaDebug)
            return DebugMagenta();
        return Card(selected);
    }
    inline Gdiplus::Color Accent(COLORREF accent)
    {
        return Gdiplus::Color(255, GetRValue(accent), GetGValue(accent), GetBValue(accent));
    }
    inline Gdiplus::Color SurfaceStrokeDefault() { return Gdiplus::Color(64, 255, 255, 255); }
    inline Gdiplus::Color FocusShadow() { return Gdiplus::Color(150, 0, 0, 0); }
}

static DWORD GetAcrylicGradientABGR()
{
    wchar_t value[32] = {};
    DWORD len = GetEnvironmentVariableW(L"AWC_ACRYLIC_ABGR", value, ARRAYSIZE(value));
    if (len > 0 && len < ARRAYSIZE(value))
    {
        wchar_t* end = nullptr;
        DWORD parsed = static_cast<DWORD>(wcstoul(value, &end, 0));
        if (end && *end == L'\0')
            return parsed;
    }
    return AltTabStyle::AcrylicThinGradientABGR;
}

static void EnableAcrylic(HWND hwnd, DWORD gradientColorABGR)
{
    static PFN_SetWindowCompositionAttribute fn =
        reinterpret_cast<PFN_SetWindowCompositionAttribute>(
            GetProcAddress(GetModuleHandleW(L"user32.dll"), "SetWindowCompositionAttribute"));
    if (!fn)
        return;
    ACCENT_POLICY accent = {};
    accent.AccentState = ACCENT_ENABLE_ACRYLICBLURBEHIND;
    accent.AccentFlags = 0;
    accent.GradientColor = gradientColorABGR;
    WINDOWCOMPOSITIONATTRIBDATA data = {};
    data.Attrib = WCA_ACCENT_POLICY;
    data.pvData = &accent;
    data.cbData = sizeof(accent);
    fn(hwnd, &data);
}

// =================== Snapshot thumbnail ===================

struct SnapshotThumb
{
    HBITMAP bitmap = nullptr;
    void* bits = nullptr;
    int width = 0;
    int height = 0;
};

// =================== Window enumeration / activation ===================

static std::wstring ProcessImagePath(HWND hwnd)
{
    DWORD pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);
    if (!pid)
        return std::wstring();

    HANDLE proc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    if (!proc)
        return std::wstring();

    wchar_t buf[MAX_PATH * 2];
    DWORD len = static_cast<DWORD>(sizeof(buf) / sizeof(buf[0]));
    std::wstring result;
    if (QueryFullProcessImageNameW(proc, 0, buf, &len))
        result.assign(buf, len);
    CloseHandle(proc);
    return result;
}

static bool IsAltTabWindow(HWND hwnd)
{
    if (!IsWindowVisible(hwnd))
        return false;

    int cloaked = 0;
    if (SUCCEEDED(DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, &cloaked, sizeof(cloaked))) && cloaked)
        return false;

    HWND walk = GetAncestor(hwnd, GA_ROOTOWNER);
    HWND tryPopup = nullptr;
    for (;;)
    {
        tryPopup = GetLastActivePopup(walk);
        if (tryPopup == walk)
            break;
        if (IsWindowVisible(tryPopup))
            break;
        walk = tryPopup;
    }
    if (walk != hwnd)
        return false;

    LONG_PTR exStyle = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
    if (exStyle & WS_EX_TOOLWINDOW)
        return false;

    return true;
}

struct EnumCtx
{
    std::wstring exe;
    std::vector<HWND> windows;
};

static BOOL CALLBACK EnumProc(HWND hwnd, LPARAM lp)
{
    EnumCtx* ctx = reinterpret_cast<EnumCtx*>(lp);
    if (!IsAltTabWindow(hwnd))
        return TRUE;
    std::wstring exe = ProcessImagePath(hwnd);
    if (!exe.empty() && _wcsicmp(exe.c_str(), ctx->exe.c_str()) == 0)
        ctx->windows.push_back(hwnd);
    return TRUE;
}

// Collect Alt-Tab-eligible windows of the foreground app in Z-order (MRU).
static bool GetAppWindows(HWND& foreground, std::vector<HWND>& windows)
{
    windows.clear();
    foreground = GetForegroundWindow();
    if (!foreground)
        return false;

    EnumCtx ctx;
    ctx.exe = ProcessImagePath(foreground);
    if (ctx.exe.empty())
        return false;

    EnumWindows(EnumProc, reinterpret_cast<LPARAM>(&ctx));
    windows = std::move(ctx.windows);
    return !windows.empty();
}

static void ForceForeground(HWND hwnd)
{
    if (IsIconic(hwnd))
        ShowWindow(hwnd, SW_RESTORE);

    DWORD fgThread = GetWindowThreadProcessId(GetForegroundWindow(), nullptr);
    DWORD myThread = GetCurrentThreadId();

    if (fgThread && fgThread != myThread)
        AttachThreadInput(myThread, fgThread, TRUE);

    BringWindowToTop(hwnd);
    SetForegroundWindow(hwnd);
    SetFocus(hwnd);

    if (fgThread && fgThread != myThread)
        AttachThreadInput(myThread, fgThread, FALSE);
}

// =================== Active-state flag (written on UI thread, read on runner) ===================

static std::atomic<bool> g_switcherActive{ false };

// =================== Switcher class ===================

class Switcher
{
public:
    bool Init(HINSTANCE instance);
    void Shutdown();
    void OnHotkey(bool forward);

private:
    enum class St { Idle, Pending, Visible };

    static const UINT_PTR TIMER_ID = 1;
    static const UINT TIMER_MS = 25;
    static const DWORD SHOW_DELAY_MS = 180;
    static const int MAX_COLS = 6;

    static LRESULT CALLBACK WndProc(HWND, UINT, WPARAM, LPARAM);

    void OnTick();
    void Commit();
    void Cancel();
    void ShowOverlayWindow();
    void HideOverlayWindow();
    void SetSelection(int index);
    void RenderBackdrop();
    void RenderLayered();
    void ComputeLayout(const RECT& work, int& x, int& y, int& panelW, int& panelH);
    void CaptureSnapshots();
    void ClearSnapshots();
    void RegisterThumbnails();
    void UnregisterThumbnails();
    void EnsureFont();

    RECT TileRect(int index) const;
    RECT PreviewRect(const RECT& tile) const;
    RECT HeaderRect(const RECT& tile) const;

    static constexpr int Wrap(int i, int n) { return ((i % n) + n) % n; }
    int Scaled(int v) const { return static_cast<int>(v * scale + 0.5); }

    HINSTANCE hinst = nullptr;
    HWND backdropHost = nullptr;
    HWND overlay = nullptr;
    HWND thumbHost = nullptr;
    int ovX = 0, ovY = 0, ovW = 0, ovH = 0;

    St state = St::Idle;
    std::vector<HWND> windows;
    std::vector<HTHUMBNAIL> thumbs;
    std::vector<SnapshotThumb> snapshots;
    std::vector<HICON> icons;
    HWND anchorWindow = nullptr;
    int selected = 0;
    DWORD startTick = 0;

    double scale = 1.0;
    int cols = 1, rows = 1;
    int pad = 0, gap = 0, tileW = 0, tileH = 0, previewH = 0, inner = 0, radius = 0;
    int cardTrimBottom = 0;
    int headerH = 0, iconSize = 0;

    HFONT font = nullptr;
    double fontScale = 0.0;
    bool snapshotThumbnails = false;
    HBRUSH thumbHostBrush = nullptr;
};

// ---- lifecycle ---------------------------------------------------------------

bool Switcher::Init(HINSTANCE instance)
{
    hinst = instance;

    WNDCLASSW hc = {};
    hc.style = CS_HREDRAW | CS_VREDRAW;
    hc.lpfnWndProc = &DefWindowProcW;
    hc.hInstance = hinst;
    hc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    thumbHostBrush = GetEnvironmentVariableW(L"AWC_MAGENTA", nullptr, 0)
                         ? CreateSolidBrush(AltTabStyle::DebugMagentaRef())
                         : nullptr;
    hc.hbrBackground = thumbHostBrush;
    hc.lpszClassName = L"AltWindowCycleThumbHost";
    RegisterClassW(&hc);

    thumbHost = CreateWindowExW(
        WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
        hc.lpszClassName, L"", WS_POPUP | WS_DISABLED,
        0, 0, 0, 0, nullptr, nullptr, hinst, nullptr);
    if (!thumbHost)
        return false;

    WNDCLASSW bc = {};
    bc.lpfnWndProc = &DefWindowProcW;
    bc.hInstance = hinst;
    bc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    bc.hbrBackground = nullptr;
    bc.lpszClassName = L"AltWindowCycleBackdropHost";
    RegisterClassW(&bc);

    backdropHost = CreateWindowExW(
        WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED,
        bc.lpszClassName, L"", WS_POPUP | WS_DISABLED,
        0, 0, 0, 0, nullptr, nullptr, hinst, nullptr);
    if (!backdropHost)
        return false;

    WNDCLASSW wc = {};
    wc.style = CS_HREDRAW | CS_VREDRAW;
    wc.lpfnWndProc = &Switcher::WndProc;
    wc.hInstance = hinst;
    wc.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wc.hbrBackground = nullptr;
    wc.lpszClassName = L"AltWindowCycleOverlay";
    RegisterClassW(&wc);

    overlay = CreateWindowExW(
        WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED,
        wc.lpszClassName, L"", WS_POPUP,
        0, 0, 0, 0, nullptr, nullptr, hinst, nullptr);
    if (!overlay)
        return false;

    SetWindowLongPtrW(overlay, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(this));
    return true;
}

void Switcher::Shutdown()
{
    UnregisterThumbnails();
    ClearSnapshots();
    if (thumbHost)
    {
        DestroyWindow(thumbHost);
        thumbHost = nullptr;
    }
    if (backdropHost)
    {
        DestroyWindow(backdropHost);
        backdropHost = nullptr;
    }
    if (font)
    {
        DeleteObject(font);
        font = nullptr;
    }
    if (overlay)
    {
        KillTimer(overlay, TIMER_ID);
        DestroyWindow(overlay);
        overlay = nullptr;
    }
    UnregisterClassW(L"AltWindowCycleOverlay", hinst);
    UnregisterClassW(L"AltWindowCycleBackdropHost", hinst);
    UnregisterClassW(L"AltWindowCycleThumbHost", hinst);
    if (thumbHostBrush)
    {
        DeleteObject(thumbHostBrush);
        thumbHostBrush = nullptr;
    }
    g_switcherActive.store(false);
    state = St::Idle;
}

// ---- state machine -----------------------------------------------------------

void Switcher::OnHotkey(bool forward)
{
    if (state == St::Idle)
    {
        HWND fg;
        if (!GetAppWindows(fg, windows) || windows.size() < 2)
            return;

        int idx = -1;
        for (size_t i = 0; i < windows.size(); ++i)
            if (windows[i] == fg) { idx = static_cast<int>(i); break; }
        if (idx < 0)
            idx = 0;

        anchorWindow = fg;
        selected = Wrap(forward ? idx + 1 : idx - 1, static_cast<int>(windows.size()));
        state = St::Pending;
        startTick = GetTickCount();
        SetTimer(overlay, TIMER_ID, TIMER_MS, nullptr);
        g_switcherActive.store(true);
    }
    else
    {
        selected = Wrap(forward ? selected + 1 : selected - 1, static_cast<int>(windows.size()));
        if (state == St::Visible)
            SetSelection(selected);
    }
}

void Switcher::OnTick()
{
    bool altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
    bool escDown = (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;

    if (escDown)
    {
        Cancel();
        return;
    }
    if (!altDown)
    {
        Commit();
        return;
    }
    if (state == St::Pending && (GetTickCount() - startTick) >= SHOW_DELAY_MS)
    {
        ShowOverlayWindow();
        state = St::Visible;
    }
}

void Switcher::Commit()
{
    KillTimer(overlay, TIMER_ID);
    if (state == St::Visible)
        HideOverlayWindow();
    anchorWindow = nullptr;

    if (selected >= 0 && selected < static_cast<int>(windows.size()))
    {
        HWND target = windows[selected];
        if (IsWindow(target))
            ForceForeground(target);
    }
    state = St::Idle;
    g_switcherActive.store(false);
}

void Switcher::Cancel()
{
    KillTimer(overlay, TIMER_ID);
    if (state == St::Visible)
        HideOverlayWindow();
    anchorWindow = nullptr;
    state = St::Idle;
    g_switcherActive.store(false);
}

// ---- overlay window ----------------------------------------------------------

static HMONITOR GetAnchorMonitor(HWND anchor)
{
    return MonitorFromWindow(anchor, MONITOR_DEFAULTTONEAREST);
}

static UINT GetMonitorEffectiveDpi(HMONITOR mon)
{
    UINT dpiX = 96, dpiY = 96;
    if (mon && SUCCEEDED(GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, &dpiX, &dpiY)) && dpiX)
        return dpiX;
    return 96;
}

static RECT GetWorkArea(HMONITOR mon)
{
    MONITORINFO mi = {};
    mi.cbSize = sizeof(mi);
    if (mon && GetMonitorInfoW(mon, &mi))
        return mi.rcWork;
    RECT fallback = { 0, 0, 1920, 1080 };
    return fallback;
}

static UINT GetDpiForHostOnMonitor(HWND host, HMONITOR mon, const RECT& work)
{
    if (host && mon)
    {
        SetWindowPos(host, nullptr, work.left, work.top, 1, 1,
                     SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER | SWP_HIDEWINDOW);
        UINT dpi = GetDpiForWindow(host);
        if (dpi)
            return dpi;
    }
    return GetMonitorEffectiveDpi(mon);
}

static HICON GetWindowIcon(HWND hwnd)
{
    DWORD_PTR res = 0;
    HICON icon = nullptr;
    if (SendMessageTimeoutW(hwnd, WM_GETICON, ICON_SMALL2, 0, SMTO_ABORTIFHUNG, 100, &res) && res)
        icon = reinterpret_cast<HICON>(res);
    if (!icon && SendMessageTimeoutW(hwnd, WM_GETICON, ICON_BIG, 0, SMTO_ABORTIFHUNG, 100, &res) && res)
        icon = reinterpret_cast<HICON>(res);
    if (!icon)
        icon = reinterpret_cast<HICON>(GetClassLongPtrW(hwnd, GCLP_HICONSM));
    if (!icon)
        icon = reinterpret_cast<HICON>(GetClassLongPtrW(hwnd, GCLP_HICON));
    return icon;
}

void Switcher::ShowOverlayWindow()
{
    if (windows.empty())
        return;

    int anchorIdx = selected;
    if (anchorIdx < 0) anchorIdx = 0;
    if (anchorIdx >= static_cast<int>(windows.size())) anchorIdx = 0;
    HWND anchor = IsWindow(anchorWindow) ? anchorWindow : windows[anchorIdx];

    HMONITOR mon = GetAnchorMonitor(anchor);
    RECT work = GetWorkArea(mon);
    UINT dpi = GetDpiForHostOnMonitor(thumbHost, mon, work);
    scale = dpi / 96.0;

    int x, y, panelW, panelH;
    ComputeLayout(work, x, y, panelW, panelH);

    EnsureFont();

    icons.clear();
    for (HWND w : windows)
        icons.push_back(GetWindowIcon(w));

    ovX = x; ovY = y; ovW = panelW; ovH = panelH;

    bool magentaDebug = GetEnvironmentVariableW(L"AWC_MAGENTA", nullptr, 0) != 0;
    bool translucentBackdrop = GetEnvironmentVariableW(L"AWC_TRANSLUCENT_BACKDROP", nullptr, 0) != 0;
    bool noThumbnails = GetEnvironmentVariableW(L"AWC_NO_THUMBNAILS", nullptr, 0) != 0;
    bool whitePreview = GetEnvironmentVariableW(L"AWC_WHITE_PREVIEW", nullptr, 0) != 0;
    bool forceDwmThumbnails = GetEnvironmentVariableW(L"AWC_DWM_THUMBNAILS", nullptr, 0) != 0;
    snapshotThumbnails = !forceDwmThumbnails && !noThumbnails && !whitePreview;

    if (snapshotThumbnails)
        CaptureSnapshots();
    else
        ClearSnapshots();

    if (translucentBackdrop)
    {
        SetWindowPos(backdropHost, HWND_TOPMOST, x, y, panelW, panelH,
                     SWP_NOACTIVATE | SWP_SHOWWINDOW);
        RenderBackdrop();
    }
    else
    {
        ShowWindow(backdropHost, SW_HIDE);
    }

    bool showThumbHost = !translucentBackdrop || !snapshotThumbnails;
    if (showThumbHost)
    {
        SetWindowPos(thumbHost, HWND_TOPMOST, x, y, panelW, panelH,
                     SWP_NOACTIVATE | SWP_SHOWWINDOW);
        if (!magentaDebug && !translucentBackdrop)
            EnableAcrylic(thumbHost, GetAcrylicGradientABGR());
        DWORD cornerPref = DWMWCP_ROUND;
        DwmSetWindowAttribute(thumbHost, DWMWA_WINDOW_CORNER_PREFERENCE,
                              &cornerPref, sizeof(cornerPref));
        HRGN rgn = nullptr;
        if (translucentBackdrop)
        {
            rgn = CreateRectRgn(0, 0, 0, 0);
            const int topOver = (std::max)(3, Scaled(3));
            const int rr = radius + Scaled(4);
            for (int i = 0; i < static_cast<int>(windows.size()); ++i)
            {
                RECT pv = PreviewRect(TileRect(i));
                HRGN topRgn = CreateRectRgn(pv.left, pv.top - topOver, pv.right, pv.bottom - rr);
                HRGN botRgn = CreateRoundRectRgn(pv.left, pv.bottom - 2 * rr,
                                                 pv.right + 1, pv.bottom + 1,
                                                 2 * rr, 2 * rr);
                HRGN tileRgn = CreateRectRgn(0, 0, 0, 0);
                CombineRgn(tileRgn, topRgn, botRgn, RGN_OR);
                CombineRgn(rgn, rgn, tileRgn, RGN_OR);
                DeleteObject(topRgn);
                DeleteObject(botRgn);
                DeleteObject(tileRgn);
            }
        }
        else
        {
            rgn = CreateRoundRectRgn(0, 0, panelW + 1, panelH + 1,
                                     2 * Scaled(8), 2 * Scaled(8));
        }
        SetWindowRgn(thumbHost, rgn, FALSE);
        RedrawWindow(thumbHost, nullptr, nullptr,
                     RDW_INVALIDATE | RDW_ERASE | RDW_UPDATENOW | RDW_ALLCHILDREN);
    }
    else
    {
        ShowWindow(thumbHost, SW_HIDE);
    }
    RegisterThumbnails();

    SetWindowPos(overlay, HWND_TOPMOST, x, y, panelW, panelH,
                 SWP_NOACTIVATE | SWP_SHOWWINDOW);
    RenderLayered();
    if (showThumbHost)
    {
        SetWindowPos(thumbHost, overlay, 0, 0, 0, 0,
                     SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
    }
    if (translucentBackdrop)
    {
        SetWindowPos(backdropHost, showThumbHost ? thumbHost : overlay, 0, 0, 0, 0,
                     SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
    }
}

void Switcher::HideOverlayWindow()
{
    ShowWindow(overlay, SW_HIDE);
    ShowWindow(thumbHost, SW_HIDE);
    ShowWindow(backdropHost, SW_HIDE);
    UnregisterThumbnails();
    ClearSnapshots();
    snapshotThumbnails = false;
    icons.clear();
}

void Switcher::SetSelection(int index)
{
    selected = index;
    RenderLayered();
}

void Switcher::ComputeLayout(const RECT& work, int& x, int& y, int& panelW, int& panelH)
{
    pad = Scaled(32);
    gap = Scaled(26);
    tileW = Scaled(300);
    headerH = Scaled(44);
    previewH = Scaled(176);
    inner = 0;
    radius = Scaled(8);
    cardTrimBottom = radius;
    iconSize = Scaled(24);
    tileH = headerH + previewH;

    int workW = work.right - work.left;
    int workH = work.bottom - work.top;
    int n = static_cast<int>(windows.size());

    int maxCols = (std::max)(1, (workW - 2 * pad + gap) / (tileW + gap));
    cols = (std::min)(n, (std::min)(MAX_COLS, maxCols));
    if (cols < 1) cols = 1;
    rows = (n + cols - 1) / cols;

    panelW = 2 * pad + cols * tileW + (cols - 1) * gap;
    panelH = 2 * pad + rows * tileH + (rows - 1) * gap;

    x = work.left + (workW - panelW) / 2;
    y = work.top + (workH - panelH) / 2;
}

RECT Switcher::TileRect(int index) const
{
    int col = index % cols;
    int row = index / cols;
    int left = pad + col * (tileW + gap);
    int top = pad + row * (tileH + gap);
    RECT r = { left, top, left + tileW, top + tileH - cardTrimBottom };
    return r;
}

RECT Switcher::PreviewRect(const RECT& tile) const
{
    RECT r = { tile.left + inner, tile.top + headerH, tile.right - inner, tile.bottom - inner };
    return r;
}

RECT Switcher::HeaderRect(const RECT& tile) const
{
    int m = Scaled(12);
    RECT r = { tile.left + m, tile.top, tile.right - m, tile.top + headerH };
    return r;
}

static RECT CoverSource(const RECT& dest, const RECT& avail)
{
    int aw = avail.right - avail.left;
    int ah = avail.bottom - avail.top;
    int dw = dest.right - dest.left;
    int dh = dest.bottom - dest.top;
    if (aw <= 0 || ah <= 0 || dw <= 0 || dh <= 0)
        return avail;

    double destA = static_cast<double>(dw) / dh;
    double srcA = static_cast<double>(aw) / ah;
    if (srcA > destA)
    {
        int cw = (std::max)(1, static_cast<int>(ah * destA + 0.5));
        int x = avail.left + (aw - cw) / 2;
        return { x, avail.top, x + cw, avail.bottom };
    }
    else
    {
        int ch = (std::max)(1, static_cast<int>(aw / destA + 0.5));
        int y = avail.top + (ah - ch) / 2;
        return { avail.left, y, avail.right, y + ch };
    }
}

static SIZE QueryThumbSize(HTHUMBNAIL th)
{
    SIZE s = { 0, 0 };
    DwmQueryThumbnailSourceSize(th, &s);
    return s;
}

static SIZE ClientSourceSize(HWND hwnd)
{
    RECT cr = {};
    if (GetClientRect(hwnd, &cr))
    {
        SIZE s = { cr.right - cr.left, cr.bottom - cr.top };
        if (s.cx > 0 && s.cy > 0)
            return s;
    }
    return { 0, 0 };
}

static SnapshotThumb CaptureWindowClientSnapshot(HWND hwnd)
{
    SnapshotThumb snap;

    RECT cr = {};
    if (!GetClientRect(hwnd, &cr))
        return snap;

    int w = cr.right - cr.left;
    int h = cr.bottom - cr.top;
    if (w <= 0 || h <= 0)
        return snap;

    HDC screen = GetDC(nullptr);
    HDC mem = CreateCompatibleDC(screen);
    if (!mem)
    {
        ReleaseDC(nullptr, screen);
        return snap;
    }

    BITMAPINFO bi = {};
    bi.bmiHeader.biSize = sizeof(bi.bmiHeader);
    bi.bmiHeader.biWidth = w;
    bi.bmiHeader.biHeight = -h;
    bi.bmiHeader.biPlanes = 1;
    bi.bmiHeader.biBitCount = 32;
    bi.bmiHeader.biCompression = BI_RGB;

    void* bits = nullptr;
    HBITMAP dib = CreateDIBSection(screen, &bi, DIB_RGB_COLORS, &bits, nullptr, 0);
    if (!dib)
    {
        DeleteDC(mem);
        ReleaseDC(nullptr, screen);
        return snap;
    }

    HGDIOBJ oldBmp = SelectObject(mem, dib);
    ZeroMemory(bits, static_cast<size_t>(w) * h * 4);

    DWORD_PTR printResult = 0;
    BOOL ok = SendMessageTimeoutW(hwnd,
                                  WM_PRINTCLIENT,
                                  reinterpret_cast<WPARAM>(mem),
                                  PRF_CLIENT | PRF_CHILDREN | PRF_OWNED,
                                  SMTO_ABORTIFHUNG | SMTO_BLOCK,
                                  75,
                                  &printResult) != 0;
    if (!ok && !IsIconic(hwnd))
    {
        POINT pt = { 0, 0 };
        if (ClientToScreen(hwnd, &pt))
        {
            HDC windowDC = GetDC(nullptr);
            ok = BitBlt(mem, 0, 0, w, h, windowDC, pt.x, pt.y, SRCCOPY);
            ReleaseDC(nullptr, windowDC);
        }
    }

    if (ok)
    {
        BYTE* px = static_cast<BYTE*>(bits);
        for (size_t i = 0, count = static_cast<size_t>(w) * h; i < count; ++i)
            px[i * 4 + 3] = 255;

        snap.bitmap = dib;
        snap.bits = bits;
        snap.width = w;
        snap.height = h;
    }

    SelectObject(mem, oldBmp);
    if (!ok)
        DeleteObject(dib);
    DeleteDC(mem);
    ReleaseDC(nullptr, screen);
    return snap;
}

void Switcher::ClearSnapshots()
{
    for (SnapshotThumb& snap : snapshots)
    {
        if (snap.bitmap)
            DeleteObject(snap.bitmap);
    }
    snapshots.clear();
}

void Switcher::CaptureSnapshots()
{
    ClearSnapshots();
    snapshots.reserve(windows.size());
    for (HWND hwnd : windows)
        snapshots.push_back(CaptureWindowClientSnapshot(hwnd));
}

void Switcher::RegisterThumbnails()
{
    UnregisterThumbnails();
    if (snapshotThumbnails ||
        GetEnvironmentVariableW(L"AWC_NO_THUMBNAILS", nullptr, 0) ||
        GetEnvironmentVariableW(L"AWC_WHITE_PREVIEW", nullptr, 0))
        return;

    const int topOver = (std::max)(3, Scaled(3));
    for (size_t i = 0; i < windows.size(); ++i)
    {
        RECT dest = PreviewRect(TileRect(static_cast<int>(i)));
        dest.top -= topOver;
        HTHUMBNAIL th = nullptr;
        if (FAILED(DwmRegisterThumbnail(thumbHost, windows[i], &th)) || !th)
        {
            thumbs.push_back(nullptr);
            continue;
        }
        thumbs.push_back(th);

        SIZE csz = IsIconic(windows[i]) ? SIZE{ 0, 0 } : ClientSourceSize(windows[i]);
        BOOL clientOnly = TRUE;
        if (csz.cx <= 0 || csz.cy <= 0)
        {
            csz = QueryThumbSize(th);
            clientOnly = FALSE;
        }
        RECT avail = { 0, 0, csz.cx, csz.cy };
        if (clientOnly)
        {
            int ix = (std::min)(2, (int)(avail.right - avail.left) / 4);
            int iy = (std::min)(2, (int)(avail.bottom - avail.top) / 4);
            avail.left += ix; avail.right -= ix;
            avail.top += iy; avail.bottom -= iy;
        }
        RECT rcSrc = CoverSource(dest, avail);
        DWM_THUMBNAIL_PROPERTIES props = {};
        props.dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_RECTSOURCE |
                        DWM_TNP_VISIBLE | DWM_TNP_OPACITY | DWM_TNP_SOURCECLIENTAREAONLY;
        props.rcDestination = dest;
        props.rcSource = rcSrc;
        props.opacity = 255;
        props.fVisible = TRUE;
        props.fSourceClientAreaOnly = clientOnly;
        DwmUpdateThumbnailProperties(th, &props);
    }
}

void Switcher::UnregisterThumbnails()
{
    for (HTHUMBNAIL th : thumbs)
        if (th)
            DwmUnregisterThumbnail(th);
    thumbs.clear();
}

void Switcher::EnsureFont()
{
    if (font && fontScale == scale)
        return;
    if (font)
    {
        DeleteObject(font);
        font = nullptr;
    }
    int height = -Scaled(15);
    BYTE quality = scale > 1.01 ? ANTIALIASED_QUALITY : CLEARTYPE_NATURAL_QUALITY;
    font = CreateFontW(height, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
                       DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                       quality, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
    fontScale = scale;
}

// ---- rendering helpers -------------------------------------------------------

static void ClearPreviewBottomCorners(Gdiplus::Graphics& g, const RECT& pv, int radius)
{
    Gdiplus::REAL r = static_cast<Gdiplus::REAL>(radius);
    if (r <= 0)
        return;

    Gdiplus::REAL L = static_cast<Gdiplus::REAL>(pv.left);
    Gdiplus::REAL R = static_cast<Gdiplus::REAL>(pv.right);
    Gdiplus::REAL B = static_cast<Gdiplus::REAL>(pv.bottom);
    Gdiplus::SolidBrush clearBrush(AltTabStyle::Transparent());

    Gdiplus::GraphicsState state = g.Save();
    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    g.SetCompositingMode(Gdiplus::CompositingModeSourceCopy);

    Gdiplus::GraphicsPath bottomL;
    bottomL.AddLine(L, B - r, L, B);
    bottomL.AddLine(L, B, L + r, B);
    bottomL.AddArc(L, B - 2 * r, 2 * r, 2 * r, 90, 90);
    bottomL.CloseFigure();
    g.FillPath(&clearBrush, &bottomL);

    Gdiplus::GraphicsPath bottomR;
    bottomR.AddLine(R, B - r, R, B);
    bottomR.AddLine(R, B, R - r, B);
    bottomR.AddArc(R - 2 * r, B - 2 * r, 2 * r, 2 * r, 90, -90);
    bottomR.CloseFigure();
    g.FillPath(&clearBrush, &bottomR);

    g.Restore(state);
}

static void BuildRoundRect(Gdiplus::GraphicsPath& path, const Gdiplus::RectF& r, Gdiplus::REAL rad)
{
    path.Reset();
    Gdiplus::REAL d = rad * 2;
    if (d <= 0 || d > r.Width || d > r.Height)
    {
        path.AddRectangle(r);
        return;
    }
    path.AddArc(r.X, r.Y, d, d, 180, 90);
    path.AddArc(r.GetRight() - d, r.Y, d, d, 270, 90);
    path.AddArc(r.GetRight() - d, r.GetBottom() - d, d, d, 0, 90);
    path.AddArc(r.X, r.GetBottom() - d, d, d, 90, 90);
    path.CloseFigure();
}

static void BuildTopRoundRect(Gdiplus::GraphicsPath& path, const Gdiplus::RectF& r, Gdiplus::REAL rad)
{
    path.Reset();
    Gdiplus::REAL d = rad * 2;
    if (d <= 0 || d > r.Width || d > r.Height)
    {
        path.AddRectangle(r);
        return;
    }
    path.StartFigure();
    path.AddLine(r.X, r.GetBottom(), r.X, r.Y + rad);
    path.AddArc(r.X, r.Y, d, d, 180, 90);
    path.AddLine(r.X + rad, r.Y, r.GetRight() - rad, r.Y);
    path.AddArc(r.GetRight() - d, r.Y, d, d, 270, 90);
    path.AddLine(r.GetRight(), r.Y + rad, r.GetRight(), r.GetBottom());
    path.AddLine(r.GetRight(), r.GetBottom(), r.X, r.GetBottom());
    path.CloseFigure();
}

static Gdiplus::RectF InflateF(const RECT& r, int by)
{
    return Gdiplus::RectF(
        static_cast<Gdiplus::REAL>(r.left - by),
        static_cast<Gdiplus::REAL>(r.top - by),
        static_cast<Gdiplus::REAL>((r.right - r.left) + 2 * by),
        static_cast<Gdiplus::REAL>((r.bottom - r.top) + 2 * by));
}

static std::wstring GetTitle(HWND hwnd)
{
    wchar_t buf[256];
    int len = GetWindowTextW(hwnd, buf, static_cast<int>(sizeof(buf) / sizeof(buf[0])));
    return std::wstring(buf, len > 0 ? len : 0);
}

// Draw HICON into a premultiplied-alpha DIB without disturbing the alpha channel
// of already-opaque pixels outside the icon shape.
static void DrawIconOverPARGB(void* destBits, int destW, int destH,
                              HICON icon, int x, int y, int size)
{
    if (!destBits || !icon || destW <= 0 || destH <= 0 || size <= 0)
        return;

    HDC screen = GetDC(nullptr);
    HDC iconDC = CreateCompatibleDC(screen);
    if (!iconDC)
    {
        ReleaseDC(nullptr, screen);
        return;
    }

    BITMAPINFO bi = {};
    bi.bmiHeader.biSize = sizeof(bi.bmiHeader);
    bi.bmiHeader.biWidth = size;
    bi.bmiHeader.biHeight = -size;
    bi.bmiHeader.biPlanes = 1;
    bi.bmiHeader.biBitCount = 32;
    bi.bmiHeader.biCompression = BI_RGB;

    void* iconBits = nullptr;
    HBITMAP dib = CreateDIBSection(screen, &bi, DIB_RGB_COLORS, &iconBits, nullptr, 0);
    if (!dib)
    {
        DeleteDC(iconDC);
        ReleaseDC(nullptr, screen);
        return;
    }

    HGDIOBJ oldBmp = SelectObject(iconDC, dib);
    ZeroMemory(iconBits, static_cast<size_t>(size) * size * 4);
    DrawIconEx(iconDC, 0, 0, icon, size, size, 0, nullptr, DI_NORMAL);

    BYTE* src = static_cast<BYTE*>(iconBits);
    BYTE* dst = static_cast<BYTE*>(destBits);

    bool hasAlpha = false;
    for (int i = 0; i < size * size; ++i)
    {
        if (src[i * 4 + 3] != 0)
        {
            hasAlpha = true;
            break;
        }
    }

    for (int sy = 0; sy < size; ++sy)
    {
        int dy = y + sy;
        if (dy < 0 || dy >= destH)
            continue;
        for (int sx = 0; sx < size; ++sx)
        {
            int dx = x + sx;
            if (dx < 0 || dx >= destW)
                continue;

            BYTE* s = src + (static_cast<size_t>(sy) * size + sx) * 4;
            BYTE* d = dst + (static_cast<size_t>(dy) * destW + dx) * 4;

            if (hasAlpha)
            {
                int a = s[3];
                if (a == 0)
                    continue;

                int sb = s[0], sg = s[1], sr = s[2];
                if (sb > a || sg > a || sr > a)
                {
                    sb = (sb * a + 127) / 255;
                    sg = (sg * a + 127) / 255;
                    sr = (sr * a + 127) / 255;
                }
                int inv = 255 - a;
                d[0] = static_cast<BYTE>((std::min)(255, sb + (d[0] * inv + 127) / 255));
                d[1] = static_cast<BYTE>((std::min)(255, sg + (d[1] * inv + 127) / 255));
                d[2] = static_cast<BYTE>((std::min)(255, sr + (d[2] * inv + 127) / 255));
                d[3] = 255;
            }
            else if (s[0] || s[1] || s[2])
            {
                d[0] = s[0]; d[1] = s[1]; d[2] = s[2]; d[3] = 255;
            }
        }
    }

    SelectObject(iconDC, oldBmp);
    DeleteObject(dib);
    DeleteDC(iconDC);
    ReleaseDC(nullptr, screen);
}

static void DrawHeaderText(HDC dc, HFONT hfont, const RECT& rc,
                           const std::wstring& text, COLORREF color)
{
    if (text.empty() || !hfont)
        return;

    HGDIOBJ oldFont = SelectObject(dc, hfont);
    int oldBk = SetBkMode(dc, TRANSPARENT);
    COLORREF oldColor = SetTextColor(dc, color);

    RECT textRc = rc;
    DrawTextW(dc, text.c_str(), static_cast<int>(text.size()), &textRc,
              DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS | DT_NOPREFIX);

    SetTextColor(dc, oldColor);
    SetBkMode(dc, oldBk);
    SelectObject(dc, oldFont);
}

static COLORREF GetAccentColor()
{
    DWORD color = 0, size = sizeof(color), type = 0;
    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\DWM",
                      0, KEY_QUERY_VALUE, &key) == ERROR_SUCCESS)
    {
        LONG r = RegQueryValueExW(key, L"AccentColor", nullptr, &type,
                                  reinterpret_cast<BYTE*>(&color), &size);
        RegCloseKey(key);
        if (r == ERROR_SUCCESS && type == REG_DWORD)
            return color & 0x00FFFFFF;
    }
    DWORD argb = 0;
    BOOL opaque = FALSE;
    if (SUCCEEDED(DwmGetColorizationColor(&argb, &opaque)))
        return RGB((argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF);

    return AltTabStyle::AccentFallbackRef();
}

void Switcher::RenderBackdrop()
{
    int w = ovW, h = ovH;
    if (!backdropHost || w <= 0 || h <= 0)
        return;

    HDC screenDC = GetDC(nullptr);
    HDC memDC = CreateCompatibleDC(screenDC);

    BITMAPINFO bi = {};
    bi.bmiHeader.biSize = sizeof(bi.bmiHeader);
    bi.bmiHeader.biWidth = w;
    bi.bmiHeader.biHeight = -h;
    bi.bmiHeader.biPlanes = 1;
    bi.bmiHeader.biBitCount = 32;
    bi.bmiHeader.biCompression = BI_RGB;

    void* bits = nullptr;
    HBITMAP dib = CreateDIBSection(screenDC, &bi, DIB_RGB_COLORS, &bits, nullptr, 0);
    if (!dib)
    {
        DeleteDC(memDC);
        ReleaseDC(nullptr, screenDC);
        return;
    }

    HGDIOBJ oldBmp = SelectObject(memDC, dib);
    ZeroMemory(bits, static_cast<size_t>(w) * h * 4);

    {
        Gdiplus::Bitmap bmp(w, h, w * 4, PixelFormat32bppPARGB,
                            static_cast<BYTE*>(bits));
        Gdiplus::Graphics g(&bmp);
        g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);

        Gdiplus::GraphicsPath panel;
        RECT panelRect = { 0, 0, w, h };
        BuildRoundRect(panel, InflateF(panelRect, 0), static_cast<Gdiplus::REAL>(Scaled(8)));
        Gdiplus::SolidBrush brush(AltTabStyle::TranslucentBackdrop());
        g.SetCompositingMode(Gdiplus::CompositingModeSourceCopy);
        g.FillPath(&brush, &panel);
        g.Flush();
    }

    POINT ptDst = { ovX, ovY };
    SIZE sz = { w, h };
    POINT ptSrc = { 0, 0 };
    BLENDFUNCTION bf = { AC_SRC_OVER, 0, 255, AC_SRC_ALPHA };
    UpdateLayeredWindow(backdropHost, screenDC, &ptDst, &sz, memDC, &ptSrc, 0, &bf, ULW_ALPHA);

    SelectObject(memDC, oldBmp);
    DeleteObject(dib);
    DeleteDC(memDC);
    ReleaseDC(nullptr, screenDC);
}

void Switcher::RenderLayered()
{
    int w = ovW, h = ovH;
    if (w <= 0 || h <= 0)
        return;

    HDC screenDC = GetDC(nullptr);
    HDC memDC = CreateCompatibleDC(screenDC);

    BITMAPINFO bi = {};
    bi.bmiHeader.biSize = sizeof(bi.bmiHeader);
    bi.bmiHeader.biWidth = w;
    bi.bmiHeader.biHeight = -h;
    bi.bmiHeader.biPlanes = 1;
    bi.bmiHeader.biBitCount = 32;
    bi.bmiHeader.biCompression = BI_RGB;

    void* bits = nullptr;
    HBITMAP dib = CreateDIBSection(screenDC, &bi, DIB_RGB_COLORS, &bits, nullptr, 0);
    if (!dib)
    {
        DeleteDC(memDC);
        ReleaseDC(nullptr, screenDC);
        return;
    }
    HGDIOBJ oldBmp = SelectObject(memDC, dib);
    ZeroMemory(bits, static_cast<size_t>(w) * h * 4);

    {
        Gdiplus::Bitmap bmp(w, h, w * 4, PixelFormat32bppPARGB,
                            static_cast<BYTE*>(bits));
        Gdiplus::Graphics g(&bmp);
        g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
        g.SetTextRenderingHint(Gdiplus::TextRenderingHintAntiAliasGridFit);

        bool magentaDebug = GetEnvironmentVariableW(L"AWC_MAGENTA", nullptr, 0) != 0;
        bool translucentBackdrop = GetEnvironmentVariableW(L"AWC_TRANSLUCENT_BACKDROP", nullptr, 0) != 0;
        bool noThumbnails = GetEnvironmentVariableW(L"AWC_NO_THUMBNAILS", nullptr, 0) != 0;
        bool whitePreview = GetEnvironmentVariableW(L"AWC_WHITE_PREVIEW", nullptr, 0) != 0;

        // Leave the panel background transparent; acrylic lives on thumbHost.
        Gdiplus::REAL panelRadius = static_cast<Gdiplus::REAL>(Scaled(8));
        RECT panelRect = { 0, 0, w, h };
        Gdiplus::GraphicsPath panel;
        BuildRoundRect(panel, InflateF(panelRect, 0), panelRadius);
        Gdiplus::SolidBrush panelBrush(AltTabStyle::Transparent());
        g.SetCompositingMode(Gdiplus::CompositingModeSourceCopy);
        g.FillPath(&panelBrush, &panel);
        g.SetCompositingMode(Gdiplus::CompositingModeSourceOver);
        Gdiplus::Pen panelStroke(AltTabStyle::SurfaceStrokeDefault(),
                                 static_cast<Gdiplus::REAL>((std::max)(1, Scaled(1))));
        g.DrawPath(&panelStroke, &panel);

        COLORREF accent = GetAccentColor();
        Gdiplus::Color accentClr = AltTabStyle::Accent(accent);

        for (int i = 0; i < static_cast<int>(windows.size()); ++i)
        {
            RECT tile = TileRect(i);
            bool sel = (i == selected);
            RECT pv = PreviewRect(tile);

            // Opaque header tab only; no full-height card behind the preview.
            RECT tab = { tile.left, tile.top, tile.right, pv.top };
            Gdiplus::GraphicsPath tabPath;
            BuildTopRoundRect(tabPath, InflateF(tab, 0), static_cast<Gdiplus::REAL>(radius));
            Gdiplus::SolidBrush tabBrush(AltTabStyle::Card(sel));
            g.FillPath(&tabBrush, &tabPath);

            int pw = pv.right - pv.left;
            int ph = pv.bottom - pv.top;
            if (!noThumbnails && pw > 0 && ph > 0)
            {
                Gdiplus::REAL r = static_cast<Gdiplus::REAL>(radius + Scaled(4));
                if (whitePreview)
                {
                    g.SetSmoothingMode(Gdiplus::SmoothingModeNone);
                    g.SetCompositingMode(Gdiplus::CompositingModeSourceCopy);
                    Gdiplus::SolidBrush previewBrush(AltTabStyle::WhitePreview());
                    g.FillRectangle(&previewBrush,
                                    static_cast<Gdiplus::REAL>(pv.left),
                                    static_cast<Gdiplus::REAL>(pv.top),
                                    static_cast<Gdiplus::REAL>(pw),
                                    static_cast<Gdiplus::REAL>(ph));
                    ClearPreviewBottomCorners(g, pv, static_cast<int>(r));
                    g.SetCompositingMode(Gdiplus::CompositingModeSourceOver);
                }
                else if (snapshotThumbnails)
                {
                    if (i < static_cast<int>(snapshots.size()) &&
                        snapshots[i].bitmap && snapshots[i].bits &&
                        snapshots[i].width > 0 && snapshots[i].height > 0)
                    {
                        SnapshotThumb& snap = snapshots[i];
                        RECT avail = { 0, 0, snap.width, snap.height };
                        int ix = (std::min)(2, static_cast<int>(avail.right - avail.left) / 4);
                        int iy = (std::min)(2, static_cast<int>(avail.bottom - avail.top) / 4);
                        avail.left += ix; avail.right -= ix;
                        avail.top += iy; avail.bottom -= iy;
                        RECT src = CoverSource(pv, avail);

                        Gdiplus::Bitmap srcBmp(snap.width, snap.height, snap.width * 4,
                                               PixelFormat32bppRGB,
                                               static_cast<BYTE*>(snap.bits));
                        Gdiplus::GraphicsState saved = g.Save();
                        g.SetCompositingMode(Gdiplus::CompositingModeSourceCopy);
                        g.SetInterpolationMode(Gdiplus::InterpolationModeHighQualityBicubic);
                        g.SetPixelOffsetMode(Gdiplus::PixelOffsetModeHalf);
                        Gdiplus::Rect dest(pv.left, pv.top, pw, ph);
                        g.DrawImage(&srcBmp, dest,
                                    src.left, src.top,
                                    src.right - src.left, src.bottom - src.top,
                                    Gdiplus::UnitPixel);
                        g.Restore(saved);
                        ClearPreviewBottomCorners(g, pv, static_cast<int>(r));
                    }
                }
                else
                {
                    // DWM thumbnail path: punch transparent hole; DWM composites behind.
                    g.SetSmoothingMode(Gdiplus::SmoothingModeNone);
                    g.SetCompositingMode(Gdiplus::CompositingModeSourceCopy);
                    Gdiplus::SolidBrush previewBrush(AltTabStyle::Transparent());
                    g.FillRectangle(&previewBrush,
                                    static_cast<Gdiplus::REAL>(pv.left),
                                    static_cast<Gdiplus::REAL>(pv.top),
                                    static_cast<Gdiplus::REAL>(pw),
                                    static_cast<Gdiplus::REAL>(ph));
                    g.SetCompositingMode(Gdiplus::CompositingModeSourceOver);

                    Gdiplus::REAL cover = static_cast<Gdiplus::REAL>((std::max)(1, Scaled(1)));
                    Gdiplus::REAL L = static_cast<Gdiplus::REAL>(pv.left);
                    Gdiplus::REAL R = static_cast<Gdiplus::REAL>(pv.right);
                    Gdiplus::REAL B = static_cast<Gdiplus::REAL>(pv.bottom);
                    Gdiplus::REAL Lc = L - cover;
                    Gdiplus::REAL Rc = R + cover;
                    Gdiplus::REAL Bc = B + cover;
                    Gdiplus::SolidBrush maskBrush(
                        AltTabStyle::PreviewMask(sel, magentaDebug, translucentBackdrop));
                    g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);

                    Gdiplus::GraphicsPath bottomL;
                    bottomL.AddLine(Lc, Bc - r, Lc, Bc);
                    bottomL.AddLine(Lc, Bc, Lc + r, Bc);
                    bottomL.AddArc(Lc, Bc - 2 * r, 2 * r, 2 * r, 90, 90);
                    bottomL.CloseFigure();
                    g.FillPath(&maskBrush, &bottomL);

                    Gdiplus::GraphicsPath bottomR;
                    bottomR.AddLine(Rc, Bc - r, Rc, Bc);
                    bottomR.AddLine(Rc, Bc, Rc - r, Bc);
                    bottomR.AddArc(Rc - 2 * r, Bc - 2 * r, 2 * r, 2 * r, 90, -90);
                    bottomR.CloseFigure();
                    g.FillPath(&maskBrush, &bottomR);
                }
            }

            // Header tab: app icon + window title.
            RECT hdr = HeaderRect(tile);
            int textLeft = hdr.left;
            HICON ic = (i < static_cast<int>(icons.size())) ? icons[i] : nullptr;
            if (ic)
            {
                int iy = tile.top + (headerH - iconSize) / 2;
                g.Flush();
                DrawIconOverPARGB(bits, w, h, ic, hdr.left, iy, iconSize);
                textLeft = hdr.left + iconSize + Scaled(8);
            }

            std::wstring text = GetTitle(windows[i]);
            g.Flush();
            RECT textRc = { textLeft, tile.top, hdr.right, tile.top + headerH };
            DrawHeaderText(memDC, font, textRc, text, AltTabStyle::HeaderTextRef(sel));

            // Two-ring accent focus ring around the selected tile.
            if (sel)
            {
                int gPad = Scaled(10);
                int gOut = gPad + Scaled(2);
                Gdiplus::GraphicsPath inr, out;
                BuildRoundRect(inr, InflateF(tile, gPad),
                               static_cast<Gdiplus::REAL>(radius + gPad));
                BuildRoundRect(out, InflateF(tile, gOut),
                               static_cast<Gdiplus::REAL>(radius + gOut));
                Gdiplus::Pen darkPen(AltTabStyle::FocusShadow(),
                                     static_cast<Gdiplus::REAL>((std::max)(1, Scaled(1))));
                Gdiplus::Pen accentPen(accentClr,
                                       static_cast<Gdiplus::REAL>((std::max)(2, Scaled(3))));
                g.DrawPath(&darkPen, &inr);
                g.DrawPath(&accentPen, &out);
            }
        }

        g.Flush();
    }

    POINT ptDst = { ovX, ovY };
    SIZE sz = { w, h };
    POINT ptSrc = { 0, 0 };
    BLENDFUNCTION bf = { AC_SRC_OVER, 0, 255, AC_SRC_ALPHA };
    UpdateLayeredWindow(overlay, screenDC, &ptDst, &sz, memDC, &ptSrc, 0, &bf, ULW_ALPHA);

    SelectObject(memDC, oldBmp);
    DeleteObject(dib);
    DeleteDC(memDC);
    ReleaseDC(nullptr, screenDC);
}

LRESULT CALLBACK Switcher::WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    Switcher* self = reinterpret_cast<Switcher*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    if (self)
    {
        switch (msg)
        {
        case WM_TIMER:
            if (wParam == TIMER_ID)
            {
                self->OnTick();
                return 0;
            }
            break;
        case WM_ERASEBKGND:
            return 1;
        }
    }
    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

// =================== Dedicated UI thread =====================================

// Custom thread message: wParam = forward flag (0/1).
static const UINT WM_AWC_HOTKEY = WM_APP + 1;

static Switcher g_switcher;
static HANDLE g_uiThread = nullptr;
static DWORD g_uiThreadId = 0;
static HANDLE g_readyEvent = nullptr;
static bool g_initOk = false;

static void ClearExitedUIThread()
{
    if (g_uiThread && WaitForSingleObject(g_uiThread, 0) == WAIT_OBJECT_0)
    {
        CloseHandle(g_uiThread);
        g_uiThread = nullptr;
        g_uiThreadId = 0;
        g_switcherActive.store(false);
    }
}

static bool PostHotkeyToUIThread(bool forward)
{
    return g_uiThreadId &&
           PostThreadMessageW(g_uiThreadId, WM_AWC_HOTKEY, forward ? 1u : 0u, 0) != FALSE;
}

static DWORD WINAPI UIThreadProc(LPVOID param)
{
    // Set per-monitor v2 DPI awareness on this thread so overlay layout and DWM
    // thumbnail rects line up on any monitor. Does not affect the host process.
    SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    Gdiplus::GdiplusStartupInput gsi = {};
    ULONG_PTR token = 0;
    Gdiplus::GdiplusStartup(&token, &gsi, nullptr);

    HINSTANCE hinst = static_cast<HINSTANCE>(param);
    g_initOk = g_switcher.Init(hinst);

    // Signal the caller that initialization is complete (success or failure).
    SetEvent(g_readyEvent);

    if (!g_initOk)
    {
        g_switcher.Shutdown();
        if (token)
            Gdiplus::GdiplusShutdown(token);
        return 1;
    }

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        if (msg.hwnd == nullptr && msg.message == WM_AWC_HOTKEY)
        {
            g_switcher.OnHotkey(msg.wParam != 0);
        }
        else
        {
            TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    }

    // Destroy all overlay resources BEFORE GdiplusShutdown.
    g_switcher.Shutdown();

    if (token)
        Gdiplus::GdiplusShutdown(token);

    return 0;
}

// =================== Public module API =======================================

bool InitializeAltWindowCycle(HINSTANCE hinst)
{
    ClearExitedUIThread();
    if (g_uiThread != nullptr)
        return true; // already running

    g_initOk = false;
    g_readyEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!g_readyEvent)
        return false;

    DWORD tid = 0;
    g_uiThread = CreateThread(nullptr, 0, UIThreadProc,
                              reinterpret_cast<LPVOID>(hinst), 0, &tid);
    if (!g_uiThread)
    {
        CloseHandle(g_readyEvent);
        g_readyEvent = nullptr;
        return false;
    }
    g_uiThreadId = tid;

    // Wait for Init() to complete (up to 5 s to handle slow machines).
    WaitForSingleObject(g_readyEvent, 5000);
    CloseHandle(g_readyEvent);
    g_readyEvent = nullptr;

    if (!g_initOk)
    {
        WaitForSingleObject(g_uiThread, 2000);
        CloseHandle(g_uiThread);
        g_uiThread = nullptr;
        g_uiThreadId = 0;
        return false;
    }

    return true;
}

void ShutdownAltWindowCycle()
{
    if (!g_uiThread)
        return; // already shut down

    // Ask the UI thread's GetMessage loop to exit.
    PostThreadMessageW(g_uiThreadId, WM_QUIT, 0, 0);
    if (WaitForSingleObject(g_uiThread, 5000) != WAIT_OBJECT_0)
    {
        g_switcherActive.store(false);
        return;
    }
    CloseHandle(g_uiThread);
    g_uiThread = nullptr;
    g_uiThreadId = 0;
    g_switcherActive.store(false);
}

bool HandleAltWindowCycleHotkey(bool forward)
{
    ClearExitedUIThread();
    if (!g_uiThread)
        return false;

    // If the overlay is already active, swallow the key and advance the selection.
    if (g_switcherActive.load())
    {
        return PostHotkeyToUIThread(forward);
    }

    // Quick check: need >= 2 same-app windows to do anything useful.
    HWND fg;
    std::vector<HWND> wins;
    if (!GetAppWindows(fg, wins) || wins.size() < 2)
        return false;

    return PostHotkeyToUIThread(forward);
}

// =================== Legacy instant-cycle helper =============================

void CycleForegroundAppWindows(bool forward)
{
    HWND fg;
    std::vector<HWND> wins;
    if (!GetAppWindows(fg, wins) || wins.size() < 2)
        return;

    int idx = -1;
    int n = static_cast<int>(wins.size());
    for (int i = 0; i < n; ++i)
        if (wins[i] == fg) { idx = i; break; }

    int target = (idx < 0) ? 0
               : forward   ? (idx + 1) % n
                           : (idx + n - 1) % n;

    ForceForeground(wins[target]);
}
