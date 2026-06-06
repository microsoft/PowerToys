// AltWindowCycle.cpp
// Adapts the POC overlay (C:\Users\crutkas\source\AltWindowCycle\cpp\AltWindowCycle.cpp)
// for the PowerToys in-proc module. All overlay windows and the Switcher state machine
// live on a dedicated UI thread; the runner's on_hotkey callback only posts a message.
// Thumbnail previews default to DWM compositor thumbnails for visual fidelity.

#include "pch.h"

#include "AltWindowCycle.h"
#include "AltWindowCycleLogic.h"

#include <uxtheme.h>
#include <shellscalingapi.h>
#include <objidl.h>
#include <gdiplus.h>
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

    inline COLORREF AccentFallbackRef() { return RGB(0, 120, 215); }
    constexpr COLORREF HeaderTextRef(bool selected)
    {
        return selected ? RGB(255, 255, 255) : RGB(207, 207, 207);
    }

    inline Gdiplus::Color Transparent() { return Gdiplus::Color(0, 0, 0, 0); }
    inline Gdiplus::Color Card(bool selected)
    {
        return selected ? Gdiplus::Color(255, 58, 58, 62)
                        : Gdiplus::Color(255, 43, 43, 46);
    }
    constexpr COLORREF CardRef(bool selected)
    {
        return selected ? RGB(58, 58, 62) : RGB(43, 43, 46);
    }
    inline Gdiplus::Color Accent(COLORREF accent)
    {
        return Gdiplus::Color(255, GetRValue(accent), GetGValue(accent), GetBValue(accent));
    }
    inline Gdiplus::Color SurfaceStrokeDefault() { return Gdiplus::Color(64, 255, 255, 255); }
    inline Gdiplus::Color PreviewStroke(bool selected)
    {
        return selected ? Gdiplus::Color(90, 255, 255, 255)
                        : Gdiplus::Color(45, 255, 255, 255);
    }
    inline Gdiplus::Color FocusShadow() { return Gdiplus::Color(150, 0, 0, 0); }
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

// True for paths whose filename is ApplicationFrameHost.exe (the shared host that
// owns every UWP/packaged app's top-level ApplicationFrameWindow).
static bool IsApplicationFrameHost(const std::wstring& path)
{
    size_t slash = path.find_last_of(L"\\/");
    const wchar_t* name = path.c_str() + (slash == std::wstring::npos ? 0 : slash + 1);
    return _wcsicmp(name, L"ApplicationFrameHost.exe") == 0;
}

struct CoreWindowFind
{
    DWORD hostPid = 0;
    HWND found = nullptr;
};

static BOOL CALLBACK FindCoreWindowProc(HWND child, LPARAM lp)
{
    CoreWindowFind* cf = reinterpret_cast<CoreWindowFind*>(lp);
    wchar_t cls[64];
    if (GetClassNameW(child, cls, ARRAYSIZE(cls)) &&
        wcscmp(cls, L"Windows.UI.Core.CoreWindow") == 0)
    {
        DWORD pid = 0;
        GetWindowThreadProcessId(child, &pid);
        if (pid && pid != cf->hostPid)
        {
            cf->found = child;
            return FALSE; // stop enumerating
        }
    }
    return TRUE;
}

// Image path that identifies the *real* owning app. For UWP/packaged windows the
// top-level window belongs to ApplicationFrameHost.exe, so all packaged apps would
// otherwise group together. Resolve to the hosted CoreWindow's actual process so
// each packaged app is grouped on its own.
static std::wstring RealProcessImagePath(HWND hwnd)
{
    std::wstring path = ProcessImagePath(hwnd);
    if (!IsApplicationFrameHost(path))
        return path;

    DWORD hostPid = 0;
    GetWindowThreadProcessId(hwnd, &hostPid);

    CoreWindowFind cf;
    cf.hostPid = hostPid;
    EnumChildWindows(hwnd, FindCoreWindowProc, reinterpret_cast<LPARAM>(&cf));
    if (cf.found)
    {
        std::wstring real = ProcessImagePath(cf.found);
        if (!real.empty())
            return real;
    }
    return path;
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
    std::wstring exe = RealProcessImagePath(hwnd);
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
    ctx.exe = RealProcessImagePath(foreground);
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
    void OnHotkey(bool forward, unsigned int holdModifiers);

private:
    enum class St { Idle, Visible };

    static const UINT_PTR TIMER_ID = 1;
    static const UINT TIMER_MS = 25;

    static LRESULT CALLBACK WndProc(HWND, UINT, WPARAM, LPARAM);

    void OnTick();
    void Commit();
    void Cancel();
    void ShowOverlayWindow();
    void HideOverlayWindow();
    void SetSelection(int index);
    void RenderLayered();
    void ComputeLayout(const RECT& work, int& x, int& y, int& panelW, int& panelH);
    void RegisterThumbnails();
    void UnregisterThumbnails();
    void EnsureFont();

    RECT TileRect(int index) const;
    RECT PreviewRect(const RECT& tile) const;
    RECT HeaderRect(const RECT& tile) const;

    int Scaled(int v) const { return AltWindowCycleLogic::ScaledValue(scale, v); }

    HINSTANCE hinst = nullptr;
    HWND overlay = nullptr;
    HWND thumbHost = nullptr;
    int ovX = 0, ovY = 0, ovW = 0, ovH = 0;

    St state = St::Idle;
    std::vector<HWND> windows;
    std::vector<HTHUMBNAIL> thumbs;
    std::vector<HICON> icons;
    std::vector<std::wstring> titles;
    HWND anchorWindow = nullptr;
    int selected = 0;
    unsigned int activeHoldModifiers = AltWindowCycleLogic::ModifierAlt;

    double scale = 1.0;
    AltWindowCycleLogic::OverlayLayout overlayLayout;
    int cols = 1, rows = 1;
    int pad = 0, gap = 0, tileW = 0, tileH = 0, previewH = 0, inner = 0, radius = 0;
    int cardTrimBottom = 0;
    int headerH = 0, iconSize = 0;

    HFONT font = nullptr;
    double fontScale = 0.0;
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
    hc.hbrBackground = nullptr;
    hc.lpszClassName = L"AltWindowCycleThumbHost";
    RegisterClassW(&hc);

    thumbHost = CreateWindowExW(
        WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
        hc.lpszClassName, L"", WS_POPUP | WS_DISABLED,
        0, 0, 0, 0, nullptr, nullptr, hinst, nullptr);
    if (!thumbHost)
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
    if (thumbHost)
    {
        DestroyWindow(thumbHost);
        thumbHost = nullptr;
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
    UnregisterClassW(L"AltWindowCycleThumbHost", hinst);
    g_switcherActive.store(false);
    state = St::Idle;
}

// ---- state machine -----------------------------------------------------------

#pragma warning(suppress : 26497)
static unsigned int CurrentModifiersDown()
{
    unsigned int modifiers = 0;
    const auto isDown = [](int vk) {
        return (GetAsyncKeyState(vk) & 0x8000) != 0;
    };

    if (isDown(VK_MENU))
    {
        modifiers |= AltWindowCycleLogic::ModifierAlt;
    }
    if (isDown(VK_CONTROL))
    {
        modifiers |= AltWindowCycleLogic::ModifierCtrl;
    }
    if (isDown(VK_SHIFT))
    {
        modifiers |= AltWindowCycleLogic::ModifierShift;
    }
    if (isDown(VK_LWIN) || isDown(VK_RWIN))
    {
        modifiers |= AltWindowCycleLogic::ModifierWin;
    }

    return modifiers;
}

void Switcher::OnHotkey(bool forward, unsigned int holdModifiers)
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
        activeHoldModifiers = AltWindowCycleLogic::StableHoldModifiers(holdModifiers);
        const auto firstHotkey = AltWindowCycleLogic::BeginCycle(idx, static_cast<int>(windows.size()), forward);
        if (firstHotkey.action != AltWindowCycleLogic::FirstHotkeyAction::ShowOverlay)
            return;

        selected = firstHotkey.selected;
        ShowOverlayWindow();
        state = St::Visible;
        SetTimer(overlay, TIMER_ID, TIMER_MS, nullptr);
        g_switcherActive.store(true);
    }
    else
    {
        selected = AltWindowCycleLogic::WrapIndex(forward ? selected + 1 : selected - 1, static_cast<int>(windows.size()));
        if (state == St::Visible)
            SetSelection(selected);
    }
}

void Switcher::OnTick()
{
    bool escDown = (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;

    if (escDown)
    {
        Cancel();
        return;
    }
    if (!AltWindowCycleLogic::AreRequiredModifiersDown(activeHoldModifiers, CurrentModifiersDown()))
    {
        Commit();
        return;
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

static std::wstring GetTitle(HWND hwnd);

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
    titles.clear();
    for (HWND w : windows)
    {
        icons.push_back(GetWindowIcon(w));
        titles.push_back(GetTitle(w));
    }

    ovX = x; ovY = y; ovW = panelW; ovH = panelH;

    SetWindowPos(thumbHost, HWND_TOPMOST, x, y, panelW, panelH,
                 SWP_NOACTIVATE | SWP_NOOWNERZORDER);
    EnableAcrylic(thumbHost, AltTabStyle::AcrylicThinGradientABGR);
    DWORD cornerPref = DWMWCP_ROUND;
    DwmSetWindowAttribute(thumbHost, DWMWA_WINDOW_CORNER_PREFERENCE,
                          &cornerPref, sizeof(cornerPref));
    HRGN rgn = CreateRoundRectRgn(0, 0, panelW + 1, panelH + 1,
                                  2 * Scaled(8), 2 * Scaled(8));
    SetWindowRgn(thumbHost, rgn, FALSE);
    RedrawWindow(thumbHost, nullptr, nullptr,
                 RDW_INVALIDATE | RDW_ERASE | RDW_UPDATENOW | RDW_ALLCHILDREN);
    RegisterThumbnails();

    // Update the hidden layered bitmap before showing it. Otherwise, monitor/DPI
    // switches can flash the previous-size overlay for one frame.
    RenderLayered();
    SetWindowPos(thumbHost, HWND_TOPMOST, x, y, panelW, panelH,
                 SWP_NOACTIVATE | SWP_SHOWWINDOW);
    SetWindowPos(overlay, HWND_TOPMOST, x, y, panelW, panelH,
                 SWP_NOACTIVATE | SWP_SHOWWINDOW);
    SetWindowPos(thumbHost, overlay, 0, 0, 0, 0,
                 SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
}

void Switcher::HideOverlayWindow()
{
    ShowWindow(overlay, SW_HIDE);
    ShowWindow(thumbHost, SW_HIDE);
    UnregisterThumbnails();
    icons.clear();
    titles.clear();
}

void Switcher::SetSelection(int index)
{
    selected = index;
    RenderLayered();
}

void Switcher::ComputeLayout(const RECT& work, int& x, int& y, int& panelW, int& panelH)
{
    overlayLayout = AltWindowCycleLogic::ComputeOverlayLayout(work, static_cast<int>(windows.size()), scale);
    pad = overlayLayout.pad;
    gap = overlayLayout.gap;
    tileW = overlayLayout.tileW;
    headerH = overlayLayout.headerH;
    previewH = overlayLayout.previewH;
    inner = overlayLayout.inner;
    radius = overlayLayout.radius;
    cardTrimBottom = overlayLayout.cardTrimBottom;
    iconSize = overlayLayout.iconSize;
    tileH = overlayLayout.tileH;
    cols = overlayLayout.cols;
    rows = overlayLayout.rows;

    x = overlayLayout.panelX;
    y = overlayLayout.panelY;
    panelW = overlayLayout.panelW;
    panelH = overlayLayout.panelH;
}

RECT Switcher::TileRect(int index) const
{
    return AltWindowCycleLogic::TileRect(overlayLayout, index);
}

RECT Switcher::PreviewRect(const RECT& tile) const
{
    return AltWindowCycleLogic::PreviewRect(overlayLayout, tile);
}

RECT Switcher::HeaderRect(const RECT& tile) const
{
    return AltWindowCycleLogic::HeaderRect(overlayLayout, tile);
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

void Switcher::RegisterThumbnails()
{
    UnregisterThumbnails();

    for (size_t i = 0; i < windows.size(); ++i)
    {
        RECT dest = PreviewRect(TileRect(static_cast<int>(i)));
        HTHUMBNAIL th = nullptr;
        if (FAILED(DwmRegisterThumbnail(thumbHost, windows[i], &th)) || !th)
        {
            thumbs.push_back(nullptr);
            continue;
        }
        thumbs.push_back(th);

        SIZE clientSize = IsIconic(windows[i]) ? SIZE{ 0, 0 } : ClientSourceSize(windows[i]);
        BOOL clientOnly = TRUE;
        if (clientSize.cx <= 0 || clientSize.cy <= 0)
        {
            clientSize = QueryThumbSize(th);
            clientOnly = FALSE;
        }
        RECT avail = { 0, 0, clientSize.cx, clientSize.cy };
        if (clientOnly)
        {
            int ix = (std::min)(2, (int)(avail.right - avail.left) / 4);
            int iy = (std::min)(2, (int)(avail.bottom - avail.top) / 4);
            avail.left += ix; avail.right -= ix;
            avail.top += iy; avail.bottom -= iy;
        }
        RECT rcSrc = AltWindowCycleLogic::CoverSource(dest, avail);
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
    font = CreateFontW(height, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
                       DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                       CLEARTYPE_NATURAL_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
    fontScale = scale;
}

// ---- rendering helpers -------------------------------------------------------

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

    // Legacy (1-bit mask) icons carry no per-pixel alpha, so DrawIconEx leaves the
    // alpha channel at 0 and a pure-black opaque pixel is indistinguishable from a
    // transparent one by color alone. Render the icon a second time over a white
    // background: pixels identical on both backgrounds are opaque (preserving black
    // detail), pixels that differ by ~full white are transparent.
    void* whiteBits = nullptr;
    HBITMAP whiteDib = nullptr;
    if (!hasAlpha)
    {
        whiteDib = CreateDIBSection(screen, &bi, DIB_RGB_COLORS, &whiteBits, nullptr, 0);
        if (whiteDib)
        {
            SelectObject(iconDC, whiteDib);
            memset(whiteBits, 0xFF, static_cast<size_t>(size) * size * 4);
            DrawIconEx(iconDC, 0, 0, icon, size, size, 0, nullptr, DI_NORMAL);
        }
    }
    BYTE* white = static_cast<BYTE*>(whiteBits);

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

            size_t srcOff = (static_cast<size_t>(sy) * size + sx) * 4;
            BYTE* s = src + srcOff;
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
            else
            {
                bool opaque;
                if (white)
                {
                    BYTE* wpx = white + srcOff;
                    int diff = (wpx[0] - s[0]) + (wpx[1] - s[1]) + (wpx[2] - s[2]);
                    opaque = diff < 384; // < half of 3*255 → covered on both backgrounds
                }
                else
                {
                    opaque = (s[0] || s[1] || s[2]);
                }
                if (opaque)
                {
                    d[0] = s[0]; d[1] = s[1]; d[2] = s[2]; d[3] = 255;
                }
            }
        }
    }

    SelectObject(iconDC, oldBmp);
    DeleteObject(dib);
    if (whiteDib)
        DeleteObject(whiteDib);
    DeleteDC(iconDC);
    ReleaseDC(nullptr, screen);
}

static void DrawHeaderText(BYTE* destBits, int destW, int destH, HFONT fontHandle,
                           const RECT& rc, const std::wstring& text,
                           COLORREF textColor, COLORREF backgroundColor)
{
    if (!destBits || destW <= 0 || destH <= 0 || text.empty() || !fontHandle)
        return;

    int w = rc.right - rc.left;
    int h = rc.bottom - rc.top;
    if (w <= 0 || h <= 0)
        return;

    HDC screen = GetDC(nullptr);
    HDC textDC = CreateCompatibleDC(screen);

    BITMAPINFO bi = {};
    bi.bmiHeader.biSize = sizeof(bi.bmiHeader);
    bi.bmiHeader.biWidth = w;
    bi.bmiHeader.biHeight = -h;
    bi.bmiHeader.biPlanes = 1;
    bi.bmiHeader.biBitCount = 32;
    bi.bmiHeader.biCompression = BI_RGB;

    void* scratchBits = nullptr;
    HBITMAP scratch = CreateDIBSection(screen, &bi, DIB_RGB_COLORS, &scratchBits, nullptr, 0);
    if (!scratch)
    {
        DeleteDC(textDC);
        ReleaseDC(nullptr, screen);
        return;
    }

    HGDIOBJ oldBmp = SelectObject(textDC, scratch);
    HGDIOBJ oldFont = SelectObject(textDC, fontHandle);
    RECT fill = { 0, 0, w, h };
    HBRUSH bg = CreateSolidBrush(backgroundColor);
    FillRect(textDC, &fill, bg);
    DeleteObject(bg);

    int oldBk = SetBkMode(textDC, TRANSPARENT);
    COLORREF oldColor = SetTextColor(textDC, textColor);

    RECT textRc = fill;
    DrawTextW(textDC, text.c_str(), static_cast<int>(text.size()), &textRc,
              DT_SINGLELINE | DT_VCENTER | DT_END_ELLIPSIS | DT_NOPREFIX);

    SetTextColor(textDC, oldColor);
    SetBkMode(textDC, oldBk);
    SelectObject(textDC, oldFont);

    const BYTE* src = static_cast<const BYTE*>(scratchBits);
    for (int y = 0; y < h; ++y)
    {
        int dy = rc.top + y;
        if (dy < 0 || dy >= destH)
            continue;
        for (int x = 0; x < w; ++x)
        {
            int dx = rc.left + x;
            if (dx < 0 || dx >= destW)
                continue;

            const BYTE* s = src + (static_cast<size_t>(y) * w + x) * 4;
            BYTE* d = destBits + (static_cast<size_t>(dy) * destW + dx) * 4;
            d[0] = s[0];
            d[1] = s[1];
            d[2] = s[2];
            d[3] = 255;
        }
    }

    SelectObject(textDC, oldBmp);
    DeleteObject(scratch);
    DeleteDC(textDC);
    ReleaseDC(nullptr, screen);
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

            // Full rounded card chrome. The live DWM preview is a rectangular
            // viewport inside it, which avoids fighting public DWM's square thumbnail.
            Gdiplus::GraphicsPath cardPath;
            BuildRoundRect(cardPath, InflateF(tile, 0), static_cast<Gdiplus::REAL>(radius));
            Gdiplus::SolidBrush cardBrush(AltTabStyle::Card(sel));
            g.FillPath(&cardBrush, &cardPath);

            int pw = pv.right - pv.left;
            int ph = pv.bottom - pv.top;
            if (pw > 0 && ph > 0)
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
                Gdiplus::Pen previewPen(AltTabStyle::PreviewStroke(sel),
                                         static_cast<Gdiplus::REAL>((std::max)(1, Scaled(1))));
                g.SetSmoothingMode(Gdiplus::SmoothingModeNone);
                g.DrawRectangle(&previewPen, pv.left, pv.top, pw - 1, ph - 1);
                g.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
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

            const std::wstring* text = (i < static_cast<int>(titles.size())) ? &titles[i] : nullptr;
            if (text)
            {
                g.Flush();
                RECT textRc = { textLeft, tile.top, hdr.right, tile.top + headerH };
                DrawHeaderText(static_cast<BYTE*>(bits), w, h, font, textRc, *text,
                               AltTabStyle::HeaderTextRef(sel), AltTabStyle::CardRef(sel));
            }

            // Two-ring accent focus ring around the selected tile.
            if (sel)
            {
                int gPad = Scaled(10);
                int gOut = gPad + Scaled(2);
                Gdiplus::GraphicsPath innerRing, out;
                BuildRoundRect(innerRing, InflateF(tile, gPad),
                               static_cast<Gdiplus::REAL>(radius + gPad));
                BuildRoundRect(out, InflateF(tile, gOut),
                               static_cast<Gdiplus::REAL>(radius + gOut));
                Gdiplus::Pen darkPen(AltTabStyle::FocusShadow(),
                                     static_cast<Gdiplus::REAL>((std::max)(1, Scaled(1))));
                Gdiplus::Pen accentPen(accentClr,
                                       static_cast<Gdiplus::REAL>((std::max)(2, Scaled(3))));
                g.DrawPath(&darkPen, &innerRing);
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

// Custom thread message: wParam = forward flag (0/1), lParam = held modifier mask.
static const UINT WM_AWC_HOTKEY = WM_APP + 1;

static Switcher g_switcher;
static HANDLE g_uiThread = nullptr;
static DWORD g_uiThreadId = 0;
static std::atomic<bool> g_initOk{ false };
static std::atomic<bool> g_shutdownRequested{ false };
static HINSTANCE g_threadHinst = nullptr;
static HANDLE g_threadReadyEvent = nullptr;

static void ClearExitedUIThread()
{
    if (g_uiThread && WaitForSingleObject(g_uiThread, 0) == WAIT_OBJECT_0)
    {
        CloseHandle(g_uiThread);
        g_uiThread = nullptr;
        g_uiThreadId = 0;
        g_initOk.store(false);
        g_switcherActive.store(false);
    }
}

static bool PostHotkeyToUIThread(bool forward, unsigned int holdModifiers)
{
    return g_uiThreadId &&
           PostThreadMessageW(g_uiThreadId, WM_AWC_HOTKEY, forward ? 1u : 0u, static_cast<LPARAM>(holdModifiers)) != FALSE;
}

static DWORD WINAPI UIThreadProc(LPVOID param)
{
    UNREFERENCED_PARAMETER(param);

    HINSTANCE hinst = g_threadHinst;
    HANDLE readyEvent = g_threadReadyEvent;
    g_threadReadyEvent = nullptr;

    // Set per-monitor v2 DPI awareness on this thread so overlay layout and DWM
    // thumbnail rects line up on any monitor. Does not affect the host process.
    SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    Gdiplus::GdiplusStartupInput gdiplusStartupInput = {};
    ULONG_PTR token = 0;
    Gdiplus::GdiplusStartup(&token, &gdiplusStartupInput, nullptr);

    const bool initOk = g_switcher.Init(hinst);
    g_initOk.store(initOk);

    // Signal the caller that initialization is complete (success or failure).
    if (readyEvent)
    {
        SetEvent(readyEvent);
        CloseHandle(readyEvent);
    }

    if (!initOk || g_shutdownRequested.load())
    {
        g_switcher.Shutdown();
        if (token)
            Gdiplus::GdiplusShutdown(token);
        return initOk ? 0 : 1;
    }

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        if (msg.hwnd == nullptr && msg.message == WM_AWC_HOTKEY)
        {
            g_switcher.OnHotkey(msg.wParam != 0, static_cast<unsigned int>(msg.lParam));
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
        return g_initOk.load(); // already running or still starting

    g_initOk.store(false);
    g_shutdownRequested.store(false);

    HANDLE readyEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!readyEvent)
        return false;

    HANDLE readyEventForThread = nullptr;
    if (!DuplicateHandle(GetCurrentProcess(), readyEvent, GetCurrentProcess(), &readyEventForThread, 0, FALSE, DUPLICATE_SAME_ACCESS))
    {
        CloseHandle(readyEvent);
        return false;
    }

    g_threadHinst = hinst;
    g_threadReadyEvent = readyEventForThread;

    DWORD tid = 0;
    g_uiThread = CreateThread(nullptr, 0, UIThreadProc, nullptr, 0, &tid);
    if (!g_uiThread)
    {
        g_threadReadyEvent = nullptr;
        CloseHandle(readyEventForThread);
        CloseHandle(readyEvent);
        return false;
    }
    g_uiThreadId = tid;

    // Wait for Init() to complete (up to 5 s to handle slow machines).
    const DWORD waitResult = WaitForSingleObject(readyEvent, 5000);
    CloseHandle(readyEvent);
    if (waitResult != WAIT_OBJECT_0)
    {
        Logger::error("Timed out waiting for AltWindowCycle UI thread initialization");
        return false;
    }

    if (!g_initOk.load())
    {
        if (WaitForSingleObject(g_uiThread, 2000) == WAIT_OBJECT_0)
        {
            CloseHandle(g_uiThread);
            g_uiThread = nullptr;
            g_uiThreadId = 0;
            g_initOk.store(false);
        }
        else
        {
            Logger::error("Timed out waiting for failed AltWindowCycle UI thread initialization to exit");
        }
        return false;
    }

    return true;
}

void ShutdownAltWindowCycle()
{
    if (!g_uiThread)
        return; // already shut down

    g_shutdownRequested.store(true);

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

bool HandleAltWindowCycleHotkey(bool forward, unsigned int holdModifiers)
{
    ClearExitedUIThread();
    if (!g_uiThread)
        return false;

    // If the overlay is already active, swallow the key and advance the selection.
    if (g_switcherActive.load())
    {
        return PostHotkeyToUIThread(forward, holdModifiers);
    }

    // Quick check: need >= 2 same-app windows to do anything useful.
    HWND fg;
    std::vector<HWND> wins;
    if (!GetAppWindows(fg, wins) || wins.size() < 2)
        return false;

    return PostHotkeyToUIThread(forward, holdModifiers);
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
