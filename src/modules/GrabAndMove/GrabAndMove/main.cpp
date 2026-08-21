#include "pch.h"

#include <common/SettingsAPI/settings_objects.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/Telemetry/ProjectTelemetry.h>
#include <common/utils/process_path.h>
#include <common/utils/ProcessWaiter.h>
#include <common/utils/excluded_apps.h>
#include <common/utils/game_mode.h>
#include <common/interop/shared_constants.h>

#include "resource.h"

#include <dwmapi.h>
#include <optional>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

// ---------------------------------------------------------------------------
// Globals
// ---------------------------------------------------------------------------
static HINSTANCE g_hInstance = nullptr;
static ULONG_PTR g_gdiplusToken = 0; // GDI+ token for overlay border rendering
static HHOOK g_hhkKeyboard = nullptr;
static HHOOK g_hhkMouse = nullptr;
static HWND g_hMsgWnd = nullptr;

// 0 = Alt (default), 1 = Win
enum class GrabAndMoveModifier
{
    Alt = 0,
    Win = 1,
};

enum class InteractionAction
{
    None,
    Move,
    Resize,
};

enum class InteractionPhase
{
    Idle,
    Pending,
    Active,
};

enum class MouseButton : unsigned int
{
    None = 0,
    Left = 1,
    Right = 2,
};

enum class HookDisposition
{
    Chain,
    PassWithoutChaining,
    Swallow,
};

enum class ModifierHoldDisposition
{
    Undecided,
    Passthrough,
    Activated,
};

// Resize handle identifiers
enum class ResizeHandle
{
    None,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
};

struct SettingsSnapshot
{
    GrabAndMoveModifier modifierKey = GrabAndMoveModifier::Alt;
    bool shouldAbsorbAlt = true;
    bool showGeometry = false;
    bool doNotActivateOnGameMode = true;
    bool useAltResize = true;
    std::shared_ptr<const std::vector<std::wstring>> excludedApps =
        std::make_shared<const std::vector<std::wstring>>();
};

struct CapturedKey
{
    DWORD vk = 0;
    DWORD scanCode = 0;
    DWORD flags = 0;
};

struct ModifierSession
{
    GrabAndMoveModifier modifier = GrabAndMoveModifier::Alt;
    ModifierHoldDisposition disposition = ModifierHoldDisposition::Undecided;
    CapturedKey key{};
    bool pressed = false;
    bool absorbed = false;
    bool replayedDown = false;
    bool consumed = false;
};

struct InteractionState
{
    InteractionPhase phase = InteractionPhase::Idle;
    InteractionAction action = InteractionAction::None;
    MouseButton button = MouseButton::None;
    HWND target = nullptr;
    POINT startPoint{};
    POINT lastPoint{};
    RECT windowRect{};
    ResizeHandle resizeHandle = ResizeHandle::None;
    bool firstUpdate = false;
};

static SettingsSnapshot g_settings;
static ModifierSession g_modifierSession;
static InteractionState g_interaction;
static unsigned int g_swallowButtonUpMask = 0;
static DWORD g_swallowNextModifierUpVk = 0;
static HWND g_hOverlay = nullptr; // semi-transparent overlay during drag

// Current target window rect for overlay info display
static int g_overlayInfoX = 0, g_overlayInfoY = 0;
static int g_overlayInfoW = 0, g_overlayInfoH = 0;

// Visible frame overlay metrics. Computed once per drag/resize (cold path) and
// reused while rendering - never recomputed in the mouse-move hot path.
// Margins are the difference between GetWindowRect and the DWM extended frame
// bounds (the invisible resize border), so the fill and border hug the visible
// window. The border is drawn just inside the visible edge; Always On Top draws
// its own border just outside that edge, so the two stack into a clean double
// layer without Grab and Move having to widen its stroke.
static int g_overlayMarginL = 0, g_overlayMarginT = 0, g_overlayMarginR = 0, g_overlayMarginB = 0;
static int g_overlayCornerRadius = 0; // physical px; 0 = square corners
static int g_overlayBorderThickness = 4; // physical px

// Fluent "warning" gold - copy of WinUI SystemFillColorCaution
// (used as a ThemeResource for warnings across the Settings UI). A Win32 layered
// window can't resolve a ThemeResource, so the literal is required here.
static constexpr COLORREF OVERLAY_BORDER_COLOR = RGB(255, 185, 0); // #FFB900

// Border thickness in DIPs (scaled by the target window DPI).
static constexpr int OVERLAY_BORDER_DIP = 4;

// Translucent white wash painted over the visible window during a drag/resize,
// matching the prior overlay. ~40% opacity (premultiplied white = 0x66666666).
static constexpr BYTE OVERLAY_FILL_ALPHA = 0x66;

// Count of non-modifier keys currently held. Used to suppress GrabAndMove when the
// modifier key is pressed while another key is already down (e.g. Q held, then modifier pressed).
static int g_heldNonAltKeyCount = 0;

// Per-vkCode tracking for held keys. Only the initial press increments
// g_heldNonAltKeyCount; auto-repeat keydowns are ignored. This avoids relying
// on KF_REPEAT which is not available in KBDLLHOOKSTRUCT::flags.
static bool g_keyHeld[256] = {};

static const int MIN_WINDOW_WIDTH = 150;
static const int MIN_WINDOW_HEIGHT = 50;

// Minimum interval (ms) between move/resize updates.  Lower = snappier but
// more CPU/GPU work.  0 = unlimited (every mouse event triggers an update).
// 4 ms ≈ 240 Hz, 8 ms ≈ 120 Hz, 16 ms ≈ 60 Hz.
static constexpr ULONGLONG THROTTLE_INTERVAL_MS = 16;

// QPC helpers for throttle
static ULONGLONG QpcMs()
{
    static LARGE_INTEGER freq = {};
    if (freq.QuadPart == 0)
        QueryPerformanceFrequency(&freq);
    LARGE_INTEGER now;
    QueryPerformanceCounter(&now);
    return static_cast<ULONGLONG>(now.QuadPart * 1000 / freq.QuadPart);
}
static ULONGLONG g_lastMoveTick = 0;

// Cached system cursors (loaded once at startup) – fix #6
static HCURSOR g_curSizeAll = nullptr;
static HCURSOR g_curSizeNWSE = nullptr;
static HCURSOR g_curSizeNESW = nullptr;
static HCURSOR g_curSizeNS = nullptr;
static HCURSOR g_curSizeWE = nullptr;

// IsExcluded result cache keyed by HWND (cleared on foreground change / settings reload) – fix #4
static std::unordered_map<HWND, bool> g_excludedCache;

static const wchar_t* const CLASS_NAME = L"GrabAndMove_MsgWnd";
static const wchar_t* const OVERLAY_CLASS_NAME = L"GrabAndMove_Overlay";
static const wchar_t* const APP_TITLE = L"GrabAndMove";

static HANDLE g_hReloadSettingsEvent = nullptr;
static HANDLE g_hExitEvent = nullptr;
static HANDLE g_hStopSettingsEvent = nullptr;
static std::thread g_settingsThread;
static HWINEVENTHOOK g_hWinEventHook = nullptr;
static HANDLE g_hInstanceMutex = nullptr;
static bool g_traceRegistered = false;

static std::mutex g_pendingSettingsMutex;
static std::optional<SettingsSnapshot> g_pendingSettings;

// Custom messages are handled only by the main message window.
static constexpr UINT WM_APPLY_SETTINGS = WM_APP + 1;

static constexpr unsigned int ButtonBit(MouseButton button)
{
    return static_cast<unsigned int>(button);
}

static void ValidateInputState() noexcept
{
#ifdef _DEBUG
    const bool idleStateIsValid =
        g_interaction.phase != InteractionPhase::Idle ||
        (g_interaction.action == InteractionAction::None &&
         g_interaction.button == MouseButton::None &&
         g_interaction.target == nullptr);
    const bool interactionStateIsValid =
        g_interaction.phase == InteractionPhase::Idle ||
        (g_interaction.action != InteractionAction::None &&
         g_interaction.button != MouseButton::None &&
         g_interaction.target != nullptr);
    const bool replayStateIsValid =
        !g_modifierSession.replayedDown || g_modifierSession.key.vk != 0;
    const bool buttonMaskIsValid =
        (g_swallowButtonUpMask & ~(ButtonBit(MouseButton::Left) | ButtonBit(MouseButton::Right))) == 0;

    if (!idleStateIsValid ||
        !interactionStateIsValid ||
        !replayStateIsValid ||
        !buttonMaskIsValid)
    {
        OutputDebugStringW(L"GrabAndMove input state invariant failed.\n");
    }
#endif
}

enum class ModifierReplay
{
    DownOnly,
    UpOnly,
    DownAndUp,
};

static void StopInteraction();
static void FlushPendingClickOnModifierRelease();
static void ReplayCapturedModifier(ModifierReplay replay);
static void MarkButtonUpForSwallow(MouseButton button);

static bool HasModifierSession()
{
    return g_modifierSession.pressed ||
           g_modifierSession.absorbed ||
           g_modifierSession.replayedDown ||
           g_modifierSession.consumed;
}

static bool IsModifierPhysicallyHeld(GrabAndMoveModifier modifier)
{
    if (modifier == GrabAndMoveModifier::Win)
    {
        return ((GetAsyncKeyState(VK_LWIN) | GetAsyncKeyState(VK_RWIN)) & 0x8000) != 0;
    }

    return ((GetAsyncKeyState(VK_LMENU) | GetAsyncKeyState(VK_RMENU)) & 0x8000) != 0;
}

// ---------------------------------------------------------------------------
// WinEvent hook – detects foreground switch to elevated processes where
// low-level keyboard hooks stop delivering key-up events.
// ---------------------------------------------------------------------------
static void CALLBACK WinEventProc(HWINEVENTHOOK, DWORD, HWND hwnd, LONG, LONG, DWORD, DWORD)
{
    // Ignore focus changes to our own windows – these are benign and fire constantly
    // (overlay creation/destruction, repositioned drag targets, etc.).
    if (hwnd == g_hOverlay || hwnd == g_hMsgWnd)
        return;

    // Any foreground switch to a non-own window can eat key-up events (e.g. Win+L eats
    // the 'L' keyup before the session locks). Always reset the held-key counter so that
    // the next Alt/Win press is never blocked by a stale non-zero count.
    g_heldNonAltKeyCount = 0;
    memset(g_keyHeld, 0, sizeof(g_keyHeld));

    // Invalidate the IsExcluded cache on foreground change (fix #4)
    g_excludedCache.clear();

    // Only validate modifier state when there is actually something to reset.
    // Skipping here when all flags are clear prevents spurious resets that would
    // break continuous Alt-dragging between multiple drags.
    if (!HasModifierSession() && g_interaction.phase == InteractionPhase::Idle)
        return;

    const GrabAndMoveModifier modifier = HasModifierSession() ? g_modifierSession.modifier : g_settings.modifierKey;
    if (!IsModifierPhysicallyHeld(modifier))
    {
        if (g_interaction.phase == InteractionPhase::Pending)
        {
            FlushPendingClickOnModifierRelease();
        }
        else if (g_interaction.phase == InteractionPhase::Active)
        {
            MarkButtonUpForSwallow(g_interaction.button);
        }

        if (g_modifierSession.replayedDown)
        {
            ReplayCapturedModifier(ModifierReplay::UpOnly);
            g_swallowNextModifierUpVk = g_modifierSession.key.vk;
        }

        StopInteraction();
        g_modifierSession = {};
    }
}

static bool IsSuppressedByGameMode()
{
    // Remote sessions can report fullscreen notification states that are not actual games.
    if (GetSystemMetrics(SM_REMOTESESSION))
    {
        return false;
    }

    return g_settings.doNotActivateOnGameMode && detect_game_mode();
}

static bool IsActivationModifierPressed()
{
    return g_modifierSession.pressed &&
           g_modifierSession.disposition != ModifierHoldDisposition::Passthrough;
}

enum class GrabAndMoveShortcutAction
{
    Move,
    Resize,
};

static void TraceShortcutUse(bool successful, GrabAndMoveShortcutAction action, const wchar_t* reason) noexcept
{
    const wchar_t* actionName = action == GrabAndMoveShortcutAction::Move ? L"move" : L"resize";

    TraceLoggingWrite(
        g_hProvider,
        "GrabAndMove_ShortcutUse",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(successful, "Successful"),
        TraceLoggingWideString(actionName, "Action"),
        TraceLoggingWideString(reason, "Reason"));
}

// ---------------------------------------------------------------------------
// Settings file helpers
// ---------------------------------------------------------------------------
static bool TryLoadSettingsFromFile(const SettingsSnapshot& current, SettingsSnapshot& updated)
{
    try
    {
        updated = current;
        PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::load_from_settings_file(L"GrabAndMove");

        if (auto v = values.get_bool_value(L"shouldAbsorbAlt"))
        {
            updated.shouldAbsorbAlt = *v;
        }

        if (auto v = values.get_bool_value(L"showGeometry"))
        {
            updated.showGeometry = *v;
        }

        if (auto v = values.get_bool_value(L"doNotActivateOnGameMode"))
        {
            updated.doNotActivateOnGameMode = *v;
        }

        if (auto v = values.get_bool_value(L"useAltResize"))
        {
            updated.useAltResize = *v;
        }

        if (auto v = values.get_int_value(L"modifierKey"))
        {
            updated.modifierKey = (*v == 1) ? GrabAndMoveModifier::Win : GrabAndMoveModifier::Alt;
        }

        if (auto v = values.get_string_value(L"excluded_apps"))
        {
            std::vector<std::wstring> apps;
            std::wstring upper = *v;
            CharUpperBuffW(upper.data(), static_cast<DWORD>(upper.length()));
            std::wstring_view view(upper);

            while (!view.empty())
            {
                // skip leading whitespace / newlines
                auto start = view.find_first_not_of(L" \t\r\n");
                if (start == std::wstring_view::npos)
                    break;
                view.remove_prefix(start);

                auto pos = view.find_first_of(L"\r\n");
                if (pos == std::wstring_view::npos)
                    pos = view.length();

                apps.emplace_back(view.substr(0, pos));
                view.remove_prefix(pos);
            }

            updated.excludedApps =
                std::make_shared<const std::vector<std::wstring>>(std::move(apps));
        }

        return true;
    }
    catch (...)
    {
        return false;
    }
}

static void PublishPendingSettings(SettingsSnapshot settings)
{
    {
        std::scoped_lock lock(g_pendingSettingsMutex);
        g_pendingSettings = std::move(settings);
    }

    PostMessage(g_hMsgWnd, WM_APPLY_SETTINGS, 0, 0);
}

static void SettingsWatcherThread(SettingsSnapshot current)
{
    HANDLE events[] = { g_hReloadSettingsEvent, g_hExitEvent, g_hStopSettingsEvent };

    for (;;)
    {
        const DWORD wait = WaitForMultipleObjects(ARRAYSIZE(events), events, FALSE, INFINITE);
        if (wait == WAIT_OBJECT_0)
        {
            SettingsSnapshot updated;
            if (TryLoadSettingsFromFile(current, updated))
            {
                current = updated;
                PublishPendingSettings(std::move(updated));
            }
        }
        else if (wait == WAIT_OBJECT_0 + 1)
        {
            PostMessage(g_hMsgWnd, WM_CLOSE, 0, 0);
            break;
        }
        else
        {
            break;
        }
    }
}

// ---------------------------------------------------------------------------
// Overlay window helpers – persistent window, shown/hidden per interaction
// ---------------------------------------------------------------------------
// Tracks the last rendered overlay dimensions so we can skip re-rendering
// when only the position changed (move-only path).
static int g_overlayRenderedW = 0;
static int g_overlayRenderedH = 0;

// Maps the DWM window corner preference to a base radius in DIPs, matching
// Always On Top (WindowCornerUtils::CornersRadius).
static int CornerRadiusForWindow(HWND hwnd)
{
    // Remote sessions draw square windows even on Win11, yet still report DWMWCP_DEFAULT. Match the
    // window: a remote session gets square (radius 0) so the overlay border doesn't round off the corner.
    if (GetSystemMetrics(SM_REMOTESESSION))
    {
        return 0;
    }

    int pref = 0; // DWMWCP_DEFAULT
    if (DwmGetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, &pref, sizeof(pref)) != S_OK)
    {
        return 0; // pre-Win11 / unsupported -> square corners
    }

    switch (pref)
    {
    case DWMWCP_ROUND:
        return 8;
    case DWMWCP_ROUNDSMALL:
        return 4;
    case DWMWCP_DEFAULT:
        return 8;
    default:
        return 0; // DWMWCP_DONOTROUND
    }
}

// Computes the overlay metrics (margins to the visible frame, corner radius, border
// thickness) for the target window. Cold path only: called at the start of a
// drag/resize and after un-maximize, never from the mouse-move hot path.
static void PrepareOverlayMetrics(HWND target)
{
    g_overlayMarginL = g_overlayMarginT = g_overlayMarginR = g_overlayMarginB = 0;
    g_overlayCornerRadius = 0;
    g_overlayBorderThickness = OVERLAY_BORDER_DIP;

    if (!target)
    {
        return;
    }

    const UINT dpi = GetDpiForWindow(target);
    const float scale = (dpi != 0) ? dpi / 96.0f : 1.0f;

    RECT windowRect{};
    RECT frameRect{};
    if (GetWindowRect(target, &windowRect) &&
        SUCCEEDED(DwmGetWindowAttribute(target, DWMWA_EXTENDED_FRAME_BOUNDS, &frameRect, sizeof(frameRect))))
    {
        g_overlayMarginL = max(0, static_cast<int>(frameRect.left - windowRect.left));
        g_overlayMarginT = max(0, static_cast<int>(frameRect.top - windowRect.top));
        g_overlayMarginR = max(0, static_cast<int>(windowRect.right - frameRect.right));
        g_overlayMarginB = max(0, static_cast<int>(windowRect.bottom - frameRect.bottom));
    }

    g_overlayCornerRadius = static_cast<int>(CornerRadiusForWindow(target) * scale);
    g_overlayBorderThickness = static_cast<int>(OVERLAY_BORDER_DIP * scale);
}

// Draws an antialiased (optionally rounded) border stroke fully inside `rect` using
// GDI+. The stroke hugs the inner edge of `rect` (the visible window frame).
static void DrawOverlayBorder(Gdiplus::Graphics& graphics, const RECT& rect, int thickness, int radius)
{
    const int w = rect.right - rect.left;
    const int h = rect.bottom - rect.top;
    if (w <= 0 || h <= 0 || thickness <= 0)
    {
        return;
    }

    // Keep the whole stroke inside the visible frame on every side.
    thickness = min(thickness, min(w, h) / 2);
    if (thickness <= 0)
    {
        return;
    }

    const float half = thickness / 2.0f;
    const Gdiplus::RectF path(
        rect.left + half,
        rect.top + half,
        static_cast<Gdiplus::REAL>(w) - thickness,
        static_cast<Gdiplus::REAL>(h) - thickness);

    graphics.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);
    Gdiplus::Pen pen(
        Gdiplus::Color(255, GetRValue(OVERLAY_BORDER_COLOR), GetGValue(OVERLAY_BORDER_COLOR), GetBValue(OVERLAY_BORDER_COLOR)),
        static_cast<Gdiplus::REAL>(thickness));

    if (radius <= 0)
    {
        graphics.DrawRectangle(&pen, path);
        return;
    }

    // The stroke is centred, so the path corner radius is the window radius minus
    // half the thickness; that keeps the outer edge aligned with the window corner.
    const float pathRadius = max(0.0f, radius - half);
    const float diameter = min(pathRadius * 2.0f, min(path.Width, path.Height));
    if (diameter <= 0.0f)
    {
        graphics.DrawRectangle(&pen, path);
        return;
    }

    Gdiplus::GraphicsPath border;
    border.AddArc(path.X, path.Y, diameter, diameter, 180.0f, 90.0f);
    border.AddArc(path.GetRight() - diameter, path.Y, diameter, diameter, 270.0f, 90.0f);
    border.AddArc(path.GetRight() - diameter, path.GetBottom() - diameter, diameter, diameter, 0.0f, 90.0f);
    border.AddArc(path.X, path.GetBottom() - diameter, diameter, diameter, 90.0f, 90.0f);
    border.CloseFigure();
    graphics.DrawPath(&pen, &border);
}

// Renders the overlay surface using per-pixel alpha via UpdateLayeredWindow.
// A translucent white wash covers the visible window (matching the prior overlay)
// with a tight warning-gold border on top, both hugging the visible window frame;
// the optional geometry label box is painted fully opaque so it remains legible
// regardless of what is beneath.
static void RenderOverlayContent(HWND hwnd, int cw, int ch)
{
    if (!hwnd || cw <= 0 || ch <= 0)
        return;

    BITMAPINFO bmi = {};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = cw;
    bmi.bmiHeader.biHeight = -ch; // top-down so (0,0) is top-left
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    HDC screenDC = GetDC(nullptr);
    DWORD* pBits = nullptr;
    HBITMAP hDib = CreateDIBSection(screenDC, &bmi, DIB_RGB_COLORS, reinterpret_cast<void**>(&pBits), nullptr, 0);
    if (!hDib)
    {
        ReleaseDC(nullptr, screenDC);
        return;
    }

    HDC memDC = CreateCompatibleDC(screenDC);
    HBITMAP hOldBmp = static_cast<HBITMAP>(SelectObject(memDC, hDib));

    // Start fully transparent.
    memset(pBits, 0, static_cast<size_t>(cw) * ch * sizeof(DWORD));

    // We apply a translucent white rect with a gold border.
    // The overlay window spans GetWindowRect, so inset by
    // the invisible-border margins so both hug the visible edge; Always On Top draws
    // its own border just outside that edge, giving a clean double layer.
    {
        const RECT visible = {
            g_overlayMarginL,
            g_overlayMarginT,
            cw - g_overlayMarginR,
            ch - g_overlayMarginB
        };
        const int vw = visible.right - visible.left;
        const int vh = visible.bottom - visible.top;

        Gdiplus::Bitmap bitmap(cw, ch, cw * 4, PixelFormat32bppPARGB, reinterpret_cast<BYTE*>(pBits));
        Gdiplus::Graphics graphics(&bitmap);
        graphics.SetSmoothingMode(Gdiplus::SmoothingModeAntiAlias);

        if (vw > 0 && vh > 0)
        {
            Gdiplus::SolidBrush fillBrush(Gdiplus::Color(OVERLAY_FILL_ALPHA, 255, 255, 255));
            if (g_overlayCornerRadius > 0)
            {
                // Round the wash to match the window corners (and the border).
                const float d = min(static_cast<float>(g_overlayCornerRadius) * 2.0f,
                                    static_cast<float>(min(vw, vh)));
                Gdiplus::GraphicsPath fillPath;
                fillPath.AddArc(static_cast<float>(visible.left), static_cast<float>(visible.top), d, d, 180.0f, 90.0f);
                fillPath.AddArc(static_cast<float>(visible.right) - d, static_cast<float>(visible.top), d, d, 270.0f, 90.0f);
                fillPath.AddArc(static_cast<float>(visible.right) - d, static_cast<float>(visible.bottom) - d, d, d, 0.0f, 90.0f);
                fillPath.AddArc(static_cast<float>(visible.left), static_cast<float>(visible.bottom) - d, d, d, 90.0f, 90.0f);
                fillPath.CloseFigure();
                graphics.FillPath(&fillBrush, &fillPath);
            }
            else
            {
                graphics.FillRectangle(&fillBrush, visible.left, visible.top, vw, vh);
            }
        }

        DrawOverlayBorder(graphics, visible, g_overlayBorderThickness, g_overlayCornerRadius);
        graphics.Flush();
    }

    if (g_settings.showGeometry)
    {
        wchar_t text[128];
        swprintf_s(text, L"X: %d  Y: %d\nW: %d  H: %d", g_overlayInfoX, g_overlayInfoY, g_overlayInfoW, g_overlayInfoH);

        HFONT hFont = static_cast<HFONT>(GetStockObject(DEFAULT_GUI_FONT));
        HFONT hOldFont = static_cast<HFONT>(SelectObject(memDC, hFont));

        RECT textRect = {};
        DrawTextW(memDC, text, -1, &textRect, DT_CALCRECT | DT_CENTER | DT_NOPREFIX);

        const int pad = 10;
        const int boxW = (textRect.right - textRect.left) + pad * 2;
        const int boxH = (textRect.bottom - textRect.top) + pad * 2;
        RECT boxRect = { cw / 2 - boxW / 2, ch / 2 - boxH / 2, cw / 2 + boxW / 2, ch / 2 + boxH / 2 };

        HBRUSH hBlack = CreateSolidBrush(RGB(0, 0, 0));
        FillRect(memDC, &boxRect, hBlack);
        DeleteObject(hBlack);

        RECT textDrawRect = { boxRect.left + pad, boxRect.top + pad, boxRect.right - pad, boxRect.bottom - pad };
        SetTextColor(memDC, RGB(255, 255, 255));
        SetBkMode(memDC, TRANSPARENT);
        DrawTextW(memDC, text, -1, &textDrawRect, DT_CENTER | DT_NOPREFIX);
        SelectObject(memDC, hOldFont);

        // GDI zeroes the alpha byte for every pixel it touches in a 32bpp DIB.
        // Walk the box region and force A=255 so those pixels are fully opaque.
        // With A=255 premultiplied alpha equals straight RGB, so the colours GDI
        // wrote (black fill, white text, anti-aliased edges) are correct as-is.
        const int x0 = max(0, boxRect.left);
        const int y0 = max(0, boxRect.top);
        const int x1 = min(cw, boxRect.right);
        const int y1 = min(ch, boxRect.bottom);
        for (int y = y0; y < y1; ++y)
            for (int x = x0; x < x1; ++x)
                pBits[y * cw + x] |= 0xFF000000u;
    }

    SIZE sz = { cw, ch };
    POINT ptSrc = { 0, 0 };
    BLENDFUNCTION blend = { AC_SRC_OVER, 0, 255, AC_SRC_ALPHA };
    UpdateLayeredWindow(hwnd, screenDC, nullptr, &sz, memDC, &ptSrc, 0, &blend, ULW_ALPHA);

    SelectObject(memDC, hOldBmp);
    DeleteObject(hDib);
    DeleteDC(memDC);
    ReleaseDC(nullptr, screenDC);

    g_overlayRenderedW = cw;
    g_overlayRenderedH = ch;
}

// Ensures the persistent overlay window exists (created once, reused).
static void EnsureOverlayWindow()
{
    if (g_hOverlay)
        return;

    g_hOverlay = CreateWindowExW(
        WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
        OVERLAY_CLASS_NAME,
        nullptr,
        WS_POPUP, // initially hidden (no WS_VISIBLE)
        0,
        0,
        1,
        1,
        nullptr,
        nullptr,
        g_hInstance,
        nullptr);
}

static void ShowOverlay(const RECT& rc, HCURSOR hCursor)
{
    EnsureOverlayWindow();
    if (!g_hOverlay)
        return;

    int w = rc.right - rc.left;
    int h = rc.bottom - rc.top;

    SetClassLongPtrW(g_hOverlay, GCLP_HCURSOR, reinterpret_cast<LONG_PTR>(hCursor));

    g_overlayInfoX = rc.left;
    g_overlayInfoY = rc.top;
    g_overlayInfoW = w;
    g_overlayInfoH = h;
    g_overlayRenderedW = 0; // force re-render
    g_overlayRenderedH = 0;

    SetWindowPos(g_hOverlay, HWND_TOPMOST, rc.left, rc.top, w, h, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    RenderOverlayContent(g_hOverlay, w, h);
}

// Repositions (and optionally re-renders) the overlay.
// For move-only (size unchanged), skips the expensive RenderOverlayContent.
// For resize (size changed), always re-renders so the layered surface matches.
static void RepositionOverlay(int x, int y, int w, int h)
{
    if (!g_hOverlay)
        return;

    g_overlayInfoX = x;
    g_overlayInfoY = y;
    g_overlayInfoW = w;
    g_overlayInfoH = h;

    SetWindowPos(g_hOverlay, HWND_TOPMOST, x, y, w, h, SWP_NOACTIVATE | SWP_SHOWWINDOW);

    // Re-render only when the size changed or geometry text needs updating
    bool sizeChanged = (w != g_overlayRenderedW || h != g_overlayRenderedH);
    if (sizeChanged || g_settings.showGeometry)
    {
        RenderOverlayContent(g_hOverlay, w, h);
    }
}

static void HideOverlay()
{
    if (g_hOverlay)
    {
        ShowWindow(g_hOverlay, SW_HIDE);
    }
}

static void StopInteraction()
{
    g_interaction = {};
    HideOverlay();
}

static ResizeHandle GetClosestHandle(POINT pt, const RECT& rc)
{
    int cx = (rc.left + rc.right) / 2;
    int cy = (rc.top + rc.bottom) / 2;

    struct
    {
        int x;
        int y;
        ResizeHandle h;
    } handles[] = {
        { rc.left, rc.top, ResizeHandle::TopLeft },
        { cx, rc.top, ResizeHandle::Top },
        { rc.right, rc.top, ResizeHandle::TopRight },
        { rc.right, cy, ResizeHandle::Right },
        { rc.right, rc.bottom, ResizeHandle::BottomRight },
        { cx, rc.bottom, ResizeHandle::Bottom },
        { rc.left, rc.bottom, ResizeHandle::BottomLeft },
        { rc.left, cy, ResizeHandle::Left },
    };

    ResizeHandle closest = ResizeHandle::BottomRight;
    LONG minDist = (std::numeric_limits<LONG>::max)();
    for (auto& e : handles)
    {
        LONG dx = pt.x - e.x;
        LONG dy = pt.y - e.y;
        LONG dist = dx * dx + dy * dy;
        if (dist < minDist)
        {
            minDist = dist;
            closest = e.h;
        }
    }
    return closest;
}

static HCURSOR CursorForHandle(ResizeHandle handle)
{
    switch (handle)
    {
    case ResizeHandle::TopLeft:
    case ResizeHandle::BottomRight:
        return g_curSizeNWSE;
    case ResizeHandle::TopRight:
    case ResizeHandle::BottomLeft:
        return g_curSizeNESW;
    case ResizeHandle::Top:
    case ResizeHandle::Bottom:
        return g_curSizeNS;
    case ResizeHandle::Left:
    case ResizeHandle::Right:
        return g_curSizeWE;
    default:
        return g_curSizeAll;
    }
}

static void ReplayCapturedModifier(ModifierReplay replay)
{
    INPUT inputs[2] = {};
    inputs[0].type = INPUT_KEYBOARD;
    inputs[0].ki.wVk = static_cast<WORD>(g_modifierSession.key.vk);
    inputs[0].ki.wScan = static_cast<WORD>(g_modifierSession.key.scanCode);
    inputs[0].ki.dwFlags = (g_modifierSession.key.flags & LLKHF_EXTENDED) ? KEYEVENTF_EXTENDEDKEY : 0;
    inputs[1] = inputs[0];
    inputs[1].ki.dwFlags |= KEYEVENTF_KEYUP;

    switch (replay)
    {
    case ModifierReplay::DownOnly:
        SendInput(1, &inputs[0], sizeof(INPUT));
        break;
    case ModifierReplay::UpOnly:
        SendInput(1, &inputs[1], sizeof(INPUT));
        break;
    case ModifierReplay::DownAndUp:
        SendInput(ARRAYSIZE(inputs), inputs, sizeof(INPUT));
        break;
    }
}

// ---------------------------------------------------------------------------
// Deferred activation helpers.
// A modifier+button press does not start an interaction immediately; it is held
// "pending" until the cursor moves past the drag threshold (then it becomes a
// real drag/resize) or the button is released first (then the absorbed input is
// replayed so the target application receives a normal modifier+click).
// ---------------------------------------------------------------------------
static bool BeginInteraction(InteractionAction action, MouseButton button, HWND hwnd, POINT pt)
{
    RECT windowRect{};
    if (!GetWindowRect(hwnd, &windowRect))
    {
        return false;
    }

    g_interaction.phase = InteractionPhase::Active;
    g_interaction.action = action;
    g_interaction.button = button;
    g_interaction.target = hwnd;
    g_interaction.startPoint = pt;
    g_interaction.lastPoint = pt;
    g_interaction.windowRect = windowRect;
    g_interaction.firstUpdate = true;
    g_interaction.resizeHandle = action == InteractionAction::Resize ? GetClosestHandle(pt, windowRect) : ResizeHandle::None;

    g_modifierSession.consumed = true;
    g_modifierSession.disposition = ModifierHoldDisposition::Activated;

    PrepareOverlayMetrics(hwnd);
    ShowOverlay(
        windowRect,
        action == InteractionAction::Resize ? CursorForHandle(g_interaction.resizeHandle) : g_curSizeAll);
    TraceShortcutUse(
        true,
        action == InteractionAction::Resize ? GrabAndMoveShortcutAction::Resize : GrabAndMoveShortcutAction::Move,
        L"started");
    return true;
}

static void ArmPendingInteraction(InteractionAction action, MouseButton button, HWND hwnd, POINT pt)
{
    g_interaction = {};
    g_interaction.phase = InteractionPhase::Pending;
    g_interaction.action = action;
    g_interaction.button = button;
    g_interaction.target = hwnd;
    g_interaction.startPoint = pt;
}

// Replays a modifier+click to the target: first the absorbed modifier key-down
// (if it was swallowed), then the button click. The modifier key-up is not
// synthesized here - the real key-up still reaches the target when the user
// releases the modifier. The captured-modifier state is intentionally retained so
// the keyboard hook forwards that real key-up; replayedDown guards
// against replaying the key-down more than once (e.g. across repeated clicks).
static void ReplayPendingClick(MouseButton button, bool completeModifier = false)
{
    if (g_modifierSession.absorbed && !g_modifierSession.replayedDown)
    {
        g_modifierSession.replayedDown = true;
        ReplayCapturedModifier(ModifierReplay::DownOnly);
    }

    const bool isRight = button == MouseButton::Right;
    INPUT inputs[2] = {};
    inputs[0].type = INPUT_MOUSE;
    inputs[0].mi.dwFlags = isRight ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;
    inputs[1].type = INPUT_MOUSE;
    inputs[1].mi.dwFlags = isRight ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP;
    SendInput(2, inputs, sizeof(INPUT));

    if (completeModifier && g_modifierSession.replayedDown)
    {
        ReplayCapturedModifier(ModifierReplay::UpOnly);
        g_modifierSession = {};
    }
}

static void MarkButtonUpForSwallow(MouseButton button)
{
    g_swallowButtonUpMask |= ButtonBit(button);
}

static bool ConsumeSwallowedButtonUp(MouseButton button)
{
    const unsigned int bit = ButtonBit(button);
    if ((g_swallowButtonUpMask & bit) == 0)
    {
        return false;
    }

    g_swallowButtonUpMask &= ~bit;
    return true;
}

static void ClearStaleSwallowedButtonUp(MouseButton button)
{
    g_swallowButtonUpMask &= ~ButtonBit(button);
}

// Called when the modifier is released while a press is still pending (button
// held, no move yet). Delivers the click to the target now and swallows the
// matching physical button-up that will arrive later.
static void FlushPendingClickOnModifierRelease()
{
    if (g_interaction.phase == InteractionPhase::Pending)
    {
        const MouseButton button = g_interaction.button;
        StopInteraction();
        ReplayPendingClick(button);
        MarkButtonUpForSwallow(button);
    }
}

static bool IsSystemClass(HWND hwnd)
{
    wchar_t cls[256] = {};
    GetClassNameW(hwnd, cls, ARRAYSIZE(cls));

    // Desktop and primary/secondary taskbars
    if (wcscmp(cls, L"Progman") == 0 ||
        wcscmp(cls, L"Shell_TrayWnd") == 0 ||
        wcscmp(cls, L"Shell_SecondaryTrayWnd") == 0)
        return true;

    // System tray / notification area popups and overflow
    if (wcscmp(cls, L"NotifyIconOverflowWindow") == 0 ||
        wcscmp(cls, L"TopLevelWindowForOverflowXamlIsland") == 0)
        return true;

    // Tooltips (e.g. "Show hidden icons" tooltip)
    if (wcscmp(cls, L"tooltips_class32") == 0)
        return true;

    // Task View (Win+Tab)
    if (wcscmp(cls, L"MultitaskingViewFrame") == 0 ||
        wcscmp(cls, L"XamlExplorerHostIslandWindow") == 0)
        return true;

    // System tray flyouts (Quick Settings, calendar, input switcher)
    if (wcscmp(cls, L"Windows.UI.Composition.DesktopWindowContentBridge") == 0 ||
        wcscmp(cls, L"Shell_InputSwitchTopLevelWindow") == 0)
        return true;

    return false;
}

std::wstring ToUpperInvariant(std::wstring_view input)
{
    int required = LCMapStringEx(
        LOCALE_NAME_INVARIANT,
        LCMAP_UPPERCASE,
        input.data(),
        static_cast<int>(input.size()),
        nullptr,
        0,
        nullptr,
        nullptr,
        0);

    std::wstring result(required, L'\0');

    LCMapStringEx(
        LOCALE_NAME_INVARIANT,
        LCMAP_UPPERCASE,
        input.data(),
        static_cast<int>(input.size()),
        result.data(),
        required,
        nullptr,
        nullptr,
        0);

    return result;
}

static bool IsExcluded(HWND hwnd)
{
    if (IsSystemClass(hwnd))
        return true;

    // To identify these for adding a new exception:
    // 1. Resolve the hwnd class name.
    // 2. Resolve the process path.
    // 3. Add OutputDebugStringW() for the class name and process path.
    // 4. Build the executable.
    // 5. Check with the debugger (or with Sysinternals DebugView) the outputs.
    // 6. Delete the added code.
    // 7. Add the exception below, according to the pattern there.
    //
    // Shell experience windows: Start menu, Notifications (Win+N), Search,
    // Quick Settings (volume / network / battery).
    // These use the generic Windows.UI.Core.CoreWindow class, so filter by process.
    {
        wchar_t cls[256] = {};
        GetClassNameW(hwnd, cls, ARRAYSIZE(cls));
        if (wcscmp(cls, L"Windows.UI.Core.CoreWindow") == 0)
        {
            std::wstring processPath = ToUpperInvariant(get_process_path(hwnd));
            if (processPath.find(L"STARTMENUEXPERIENCEHOST.EXE") != std::wstring::npos ||
                processPath.find(L"SHELLEXPERIENCEHOST.EXE") != std::wstring::npos ||
                processPath.find(L"SEARCHHOST.EXE") != std::wstring::npos)
                return true;
        }
        else if (wcscmp(cls, L"ControlCenterWindow") == 0)
        {
            // The Quick Settings flyout.
            std::wstring processPath = ToUpperInvariant(get_process_path(hwnd));
            if (processPath.find(L"SHELLHOST.EXE") != std::wstring::npos)
                return true;
        }
        else if (wcscmp(cls, L"WindowsDashboard") == 0)
        {
            // The Windows 11 Widgets flyout.
            std::wstring processPath = ToUpperInvariant(get_process_path(hwnd));
            if (processPath.find(L"WIDGETBOARD.EXE") != std::wstring::npos)
                return true;
        }
    }

    const auto& apps = g_settings.excludedApps;
    if (!apps || apps->empty())
        return false;

    // Check process-path exclusion (cached per HWND – fix #4)
    bool pathExcluded = false;
    auto it = g_excludedCache.find(hwnd);
    if (it != g_excludedCache.end())
    {
        pathExcluded = it->second;
    }
    else
    {
        std::wstring processPath = get_process_path(hwnd);
        CharUpperBuffW(processPath.data(), static_cast<DWORD>(processPath.length()));
        pathExcluded = find_app_name_in_path(processPath, *apps);
        g_excludedCache[hwnd] = pathExcluded;
    }

    if (pathExcluded)
        return true;

    // Title-based exclusion is always evaluated live (titles can change)
    if (check_excluded_app_with_title(hwnd, *apps))
        return true;

    return false;
}

// ---------------------------------------------------------------------------
// Hook callbacks
// ---------------------------------------------------------------------------
static LRESULT CompleteHook(
    HHOOK hook,
    HookDisposition disposition,
    int nCode,
    WPARAM wParam,
    LPARAM lParam)
{
    ValidateInputState();

#if defined(_DEBUG) && defined(GRABANDMOVE_TRACE_INPUT)
    wchar_t trace[256]{};
    swprintf_s(
        trace,
        L"GrabAndMove input=%llu modifier=%d hold=%d phase=%d action=%d buttonMask=%u disposition=%d\n",
        static_cast<unsigned long long>(wParam),
        static_cast<int>(g_modifierSession.modifier),
        static_cast<int>(g_modifierSession.disposition),
        static_cast<int>(g_interaction.phase),
        static_cast<int>(g_interaction.action),
        g_swallowButtonUpMask,
        static_cast<int>(disposition));
    OutputDebugStringW(trace);
#endif

    switch (disposition)
    {
    case HookDisposition::Swallow:
        return 1;
    case HookDisposition::PassWithoutChaining:
        return 0;
    case HookDisposition::Chain:
    default:
        return CallNextHookEx(hook, nCode, wParam, lParam);
    }
}

static constexpr bool IsKeyDownMessage(WPARAM message)
{
    return message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
}

static constexpr bool IsKeyUpMessage(WPARAM message)
{
    return message == WM_KEYUP || message == WM_SYSKEYUP;
}

static constexpr bool IsAltKey(DWORD vkCode)
{
    return vkCode == VK_MENU || vkCode == VK_LMENU || vkCode == VK_RMENU;
}

static constexpr bool IsWinKey(DWORD vkCode)
{
    return vkCode == VK_LWIN || vkCode == VK_RWIN;
}

static constexpr bool IsModifierKey(DWORD vkCode, GrabAndMoveModifier modifier)
{
    return modifier == GrabAndMoveModifier::Win ? IsWinKey(vkCode) : IsAltKey(vkCode);
}

static bool HasConflictingInput(GrabAndMoveModifier modifier)
{
    if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) ||
        (GetAsyncKeyState(VK_SHIFT) & 0x8000) ||
        g_heldNonAltKeyCount > 0)
    {
        return true;
    }

    if (modifier == GrabAndMoveModifier::Win)
    {
        return (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
    }

    return (GetAsyncKeyState(VK_LWIN) & 0x8000) ||
           (GetAsyncKeyState(VK_RWIN) & 0x8000);
}

static void TrackHeldNonModifierKey(DWORD vkCode, WPARAM message)
{
    if (IsAltKey(vkCode) || IsWinKey(vkCode))
    {
        return;
    }

    const BYTE vk = static_cast<BYTE>(vkCode);
    if (IsKeyDownMessage(message))
    {
        if (!g_keyHeld[vk])
        {
            g_keyHeld[vk] = true;
            ++g_heldNonAltKeyCount;
        }
    }
    else if (IsKeyUpMessage(message) && g_keyHeld[vk])
    {
        g_keyHeld[vk] = false;
        if (g_heldNonAltKeyCount > 0)
        {
            --g_heldNonAltKeyCount;
        }
    }
}

static HookDisposition ReleaseModifierSession()
{
    g_modifierSession.pressed = false;
    FlushPendingClickOnModifierRelease();

    const bool interactionConsumedModifier =
        g_interaction.phase == InteractionPhase::Active || g_modifierSession.consumed;
    if (g_interaction.phase == InteractionPhase::Active)
    {
        MarkButtonUpForSwallow(g_interaction.button);
    }
    StopInteraction();

    HookDisposition disposition = HookDisposition::Chain;
    if (g_modifierSession.absorbed)
    {
        if (interactionConsumedModifier)
        {
            disposition = HookDisposition::Swallow;
        }
        else if (!g_modifierSession.replayedDown)
        {
            if (g_modifierSession.modifier == GrabAndMoveModifier::Win)
            {
                ReplayCapturedModifier(ModifierReplay::DownAndUp);
                disposition = HookDisposition::Swallow;
            }
            else
            {
                ReplayCapturedModifier(ModifierReplay::DownOnly);
            }
        }
    }

    g_modifierSession = {};
    return disposition;
}

static HookDisposition HandleKeyboardEvent(WPARAM message, const KBDLLHOOKSTRUCT& key)
{
    if (key.vkCode == g_swallowNextModifierUpVk)
    {
        if (IsKeyUpMessage(message))
        {
            g_swallowNextModifierUpVk = 0;
            return HookDisposition::Swallow;
        }

        if (IsKeyDownMessage(message))
        {
            g_swallowNextModifierUpVk = 0;
        }
    }

    if (HasModifierSession() && IsModifierKey(key.vkCode, g_modifierSession.modifier))
    {
        if (IsKeyDownMessage(message))
        {
            return g_modifierSession.absorbed ? HookDisposition::Swallow : HookDisposition::Chain;
        }

        if (IsKeyUpMessage(message))
        {
            return ReleaseModifierSession();
        }
    }

    if (HasModifierSession() &&
        !IsModifierKey(key.vkCode, g_modifierSession.modifier) &&
        g_modifierSession.absorbed &&
        !g_modifierSession.consumed &&
        !g_modifierSession.replayedDown)
    {
        ReplayCapturedModifier(ModifierReplay::DownOnly);
        g_modifierSession.absorbed = false;
        g_modifierSession.pressed = false;
        g_modifierSession.replayedDown = true;
    }

    TrackHeldNonModifierKey(key.vkCode, message);

    if (!IsModifierKey(key.vkCode, g_settings.modifierKey) || !IsKeyDownMessage(message) || HasModifierSession())
    {
        return HookDisposition::Chain;
    }

    HWND foreground = GetForegroundWindow();
    if ((foreground && IsExcluded(foreground)) ||
        IsSuppressedByGameMode() ||
        HasConflictingInput(g_settings.modifierKey))
    {
        return HookDisposition::Chain;
    }

    g_modifierSession = {};
    g_modifierSession.modifier = g_settings.modifierKey;
    g_modifierSession.pressed = true;
    g_modifierSession.absorbed =
        g_settings.modifierKey == GrabAndMoveModifier::Win || g_settings.shouldAbsorbAlt;
    g_modifierSession.key = { key.vkCode, key.scanCode, key.flags };

    return g_modifierSession.absorbed ? HookDisposition::Swallow : HookDisposition::Chain;
}

static LRESULT CALLBACK KeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode < 0)
    {
        return CallNextHookEx(g_hhkKeyboard, nCode, wParam, lParam);
    }

    const auto& key = *reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
    if (key.flags & LLKHF_INJECTED)
    {
        return CallNextHookEx(g_hhkKeyboard, nCode, wParam, lParam);
    }

    return CompleteHook(g_hhkKeyboard, HandleKeyboardEvent(wParam, key), nCode, wParam, lParam);
}

static HWND ResolveTargetWindow(POINT pt)
{
    auto normalizeTarget = [](HWND candidate) -> HWND {
        if (!candidate)
        {
            return nullptr;
        }

        if (HWND root = GetAncestor(candidate, GA_ROOT))
        {
            candidate = root;
        }

        if (!candidate || candidate == g_hOverlay || candidate == g_hMsgWnd || IsSystemClass(candidate))
        {
            return nullptr;
        }

        return candidate;
    };

    // Remote sessions can produce unstable hit-testing for topmost windows.
    // Prefer the actual foreground top-level window there.
    if (GetSystemMetrics(SM_REMOTESESSION))
    {
        if (HWND foreground = normalizeTarget(GetForegroundWindow()))
        {
            return foreground;
        }
    }

    HWND hwnd = WindowFromPoint(pt);
    hwnd = normalizeTarget(hwnd);

    if (!hwnd)
    {
        HWND foreground = GetForegroundWindow();
        if (HWND normalizedForeground = normalizeTarget(foreground))
        {
            RECT foregroundRect{};
            if (GetWindowRect(normalizedForeground, &foregroundRect) && PtInRect(&foregroundRect, pt))
            {
                hwnd = normalizedForeground;
            }
        }
    }

    return hwnd;
}

// Forward declarations for helpers called from MouseProc
static void HandleDragMove(POINT pt);
static void HandleDragResize(POINT pt);

static constexpr MouseButton ButtonFromDownMessage(WPARAM message)
{
    if (message == WM_LBUTTONDOWN)
    {
        return MouseButton::Left;
    }
    if (message == WM_RBUTTONDOWN)
    {
        return MouseButton::Right;
    }
    return MouseButton::None;
}

static constexpr MouseButton ButtonFromUpMessage(WPARAM message)
{
    if (message == WM_LBUTTONUP)
    {
        return MouseButton::Left;
    }
    if (message == WM_RBUTTONUP)
    {
        return MouseButton::Right;
    }
    return MouseButton::None;
}

static constexpr InteractionAction ActionForButton(MouseButton button)
{
    return button == MouseButton::Right ? InteractionAction::Resize : InteractionAction::Move;
}

static HookDisposition HandleActionButtonDown(MouseButton button, POINT point)
{
    const InteractionAction action = ActionForButton(button);

    if (g_interaction.phase == InteractionPhase::Active)
    {
        if (button != g_interaction.button)
        {
            MarkButtonUpForSwallow(button);
        }
        return HookDisposition::Swallow;
    }

    if (IsSuppressedByGameMode())
    {
        TraceShortcutUse(
            false,
            action == InteractionAction::Resize ? GrabAndMoveShortcutAction::Resize : GrabAndMoveShortcutAction::Move,
            L"game_mode");
        return HookDisposition::Chain;
    }

    HWND target = ResolveTargetWindow(point);
    if (!target || IsExcluded(target))
    {
        return HookDisposition::Chain;
    }

    if (action == InteractionAction::Resize &&
        !(GetWindowLongW(target, GWL_STYLE) & WS_THICKFRAME))
    {
        return HookDisposition::Chain;
    }

    if (g_modifierSession.disposition == ModifierHoldDisposition::Activated)
    {
        return BeginInteraction(action, button, target, point) ? HookDisposition::Swallow : HookDisposition::Chain;
    }

    if (g_interaction.phase == InteractionPhase::Pending)
    {
        return HookDisposition::Chain;
    }

    ArmPendingInteraction(action, button, target, point);
    return HookDisposition::Swallow;
}

static void RecoverStaleInteraction(MouseButton incomingButton)
{
    if (g_interaction.phase == InteractionPhase::Idle)
    {
        return;
    }

    const MouseButton button = g_interaction.button;
    if (g_interaction.phase == InteractionPhase::Pending)
    {
        StopInteraction();
        ReplayPendingClick(button, true);
    }
    else
    {
        StopInteraction();
    }

    if (incomingButton != button)
    {
        MarkButtonUpForSwallow(button);
    }
}

static HookDisposition HandleMouseEvent(WPARAM message, const MSLLHOOKSTRUCT& mouse)
{
    const MouseButton upButton = ButtonFromUpMessage(message);
    if (upButton != MouseButton::None && ConsumeSwallowedButtonUp(upButton))
    {
        return HookDisposition::Swallow;
    }

    const MouseButton downButton = ButtonFromDownMessage(message);
    if (downButton != MouseButton::None)
    {
        // A new down means a previously expected up for this button was lost.
        ClearStaleSwallowedButtonUp(downButton);
    }

    if (downButton != MouseButton::None && !IsActivationModifierPressed())
    {
        RecoverStaleInteraction(downButton);
    }

    if (g_interaction.phase == InteractionPhase::Idle &&
        g_hOverlay &&
        IsWindowVisible(g_hOverlay))
    {
        HideOverlay();
    }

    if (downButton != MouseButton::None && IsActivationModifierPressed())
    {
        if (downButton == MouseButton::Right &&
            g_interaction.phase != InteractionPhase::Active &&
            !g_settings.useAltResize)
        {
            return HookDisposition::Chain;
        }
        return HandleActionButtonDown(downButton, mouse.pt);
    }

    if (message == WM_MOUSEMOVE && g_interaction.phase == InteractionPhase::Pending)
    {
        int dx = mouse.pt.x - g_interaction.startPoint.x;
        int dy = mouse.pt.y - g_interaction.startPoint.y;
        if (dx < 0)
        {
            dx = -dx;
        }
        if (dy < 0)
        {
            dy = -dy;
        }

        if (dx >= GetSystemMetrics(SM_CXDRAG) || dy >= GetSystemMetrics(SM_CYDRAG))
        {
            const InteractionState pending = g_interaction;
            StopInteraction();
            if (BeginInteraction(
                    pending.action,
                    pending.button,
                    pending.target,
                    pending.startPoint))
            {
                if (pending.action == InteractionAction::Resize)
                {
                    HandleDragResize(mouse.pt);
                }
                else
                {
                    HandleDragMove(mouse.pt);
                }
            }
            else
            {
                g_modifierSession.disposition = ModifierHoldDisposition::Passthrough;
                ReplayPendingClick(pending.button);
                MarkButtonUpForSwallow(pending.button);
            }
        }
        return HookDisposition::PassWithoutChaining;
    }

    if (message == WM_MOUSEMOVE && g_interaction.phase == InteractionPhase::Active)
    {
        const ULONGLONG now = QpcMs();
        if (now - g_lastMoveTick >= THROTTLE_INTERVAL_MS)
        {
            g_lastMoveTick = now;
            if (g_interaction.action == InteractionAction::Resize)
            {
                HandleDragResize(mouse.pt);
            }
            else
            {
                HandleDragMove(mouse.pt);
            }
        }
        return HookDisposition::PassWithoutChaining;
    }

    if (upButton != MouseButton::None &&
        upButton == g_interaction.button &&
        g_interaction.phase == InteractionPhase::Pending)
    {
        g_modifierSession.disposition = ModifierHoldDisposition::Passthrough;
        const MouseButton button = g_interaction.button;
        StopInteraction();
        ReplayPendingClick(button);
        return HookDisposition::Swallow;
    }

    if (upButton != MouseButton::None &&
        upButton == g_interaction.button &&
        g_interaction.phase == InteractionPhase::Active)
    {
        if (g_interaction.action == InteractionAction::Move)
        {
            const int dx = mouse.pt.x - g_interaction.startPoint.x;
            const int dy = mouse.pt.y - g_interaction.startPoint.y;
            const int newX = g_interaction.windowRect.left + dx;
            const int newY = g_interaction.windowRect.top + dy;
            SetWindowPos(
                g_interaction.target,
                nullptr,
                newX,
                newY,
                0,
                0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
        }
        else
        {
            const RECT& rect = g_interaction.windowRect;
            SetWindowPos(
                g_interaction.target,
                nullptr,
                rect.left,
                rect.top,
                rect.right - rect.left,
                rect.bottom - rect.top,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
        }

        StopInteraction();
        return HookDisposition::Swallow;
    }

    return HookDisposition::Chain;
}

static LRESULT CALLBACK MouseProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode < 0)
    {
        return CallNextHookEx(g_hhkMouse, nCode, wParam, lParam);
    }

    const auto& mouse = *reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);
    if (mouse.flags & LLMHF_INJECTED)
    {
        return CallNextHookEx(g_hhkMouse, nCode, wParam, lParam);
    }

    return CompleteHook(g_hhkMouse, HandleMouseEvent(wParam, mouse), nCode, wParam, lParam);
}

// ---------------------------------------------------------------------------
// Drag / resize move helpers – called directly from the LL mouse hook.
// DeferWindowPos batches the target + overlay into one DWM composition pass.
// ---------------------------------------------------------------------------
static void HandleDragMove(POINT pt)
{
    if (g_interaction.phase != InteractionPhase::Active ||
        g_interaction.action != InteractionAction::Move ||
        !g_interaction.target)
    {
        return;
    }

    // On the first move, restore maximized windows
    if (g_interaction.firstUpdate)
    {
        g_interaction.firstUpdate = false;
        if (IsZoomed(g_interaction.target))
        {
            RECT maxRect{};
            GetWindowRect(g_interaction.target, &maxRect);
            int maxW = maxRect.right - maxRect.left;
            int maxH = maxRect.bottom - maxRect.top;

            ShowWindow(g_interaction.target, SW_RESTORE);

            GetWindowRect(g_interaction.target, &g_interaction.windowRect);
            int restoredW = g_interaction.windowRect.right - g_interaction.windowRect.left;
            int restoredH = g_interaction.windowRect.bottom - g_interaction.windowRect.top;

            // Preserve the relative grab position in both axes so the cursor stays
            // at the same proportional spot within the restored window.
            float ratioL = (maxW > 0) ? static_cast<float>(g_interaction.startPoint.x - maxRect.left) / maxW : 0.5f;
            float ratioT = (maxH > 0) ? static_cast<float>(g_interaction.startPoint.y - maxRect.top) / maxH : 0.5f;
            int newX = g_interaction.startPoint.x - static_cast<int>(restoredW * ratioL);
            int newY = g_interaction.startPoint.y - static_cast<int>(restoredH * ratioT);
            SetWindowPos(g_interaction.target, nullptr, newX, newY, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);

            g_interaction.startPoint = pt;
            g_interaction.windowRect = { newX, newY, newX + restoredW, newY + restoredH };

            // Corner radius / invisible-border margins differ once restored.
            PrepareOverlayMetrics(g_interaction.target);
        }
    }

    int dx = pt.x - g_interaction.startPoint.x;
    int dy = pt.y - g_interaction.startPoint.y;
    int newX = g_interaction.windowRect.left + dx;
    int newY = g_interaction.windowRect.top + dy;
    int w = g_interaction.windowRect.right - g_interaction.windowRect.left;
    int h = g_interaction.windowRect.bottom - g_interaction.windowRect.top;

    // Move target + overlay (separate SetWindowPos – DeferWindowPos doesn't
    // work reliably for cross-process target windows)
    SetWindowPos(g_interaction.target, nullptr, newX, newY, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
    RepositionOverlay(newX, newY, w, h);
}

static void HandleDragResize(POINT pt)
{
    if (g_interaction.phase != InteractionPhase::Active ||
        g_interaction.action != InteractionAction::Resize ||
        !g_interaction.target)
    {
        return;
    }

    // On the first resize, restore maximized windows
    if (g_interaction.firstUpdate)
    {
        g_interaction.firstUpdate = false;
        if (IsZoomed(g_interaction.target))
        {
            RECT maxRect = g_interaction.windowRect;
            int maxW = maxRect.right - maxRect.left;
            int maxH = maxRect.bottom - maxRect.top;

            float ratioL = (maxW > 0) ? static_cast<float>(pt.x - maxRect.left) / maxW : 0.5f;
            float ratioT = (maxH > 0) ? static_cast<float>(pt.y - maxRect.top) / maxH : 0.5f;

            ShowWindow(g_interaction.target, SW_RESTORE);
            GetWindowRect(g_interaction.target, &g_interaction.windowRect);

            int newW = g_interaction.windowRect.right - g_interaction.windowRect.left;
            int newH = g_interaction.windowRect.bottom - g_interaction.windowRect.top;

            int newLeft = pt.x - static_cast<int>(ratioL * newW);
            int newTop = pt.y - static_cast<int>(ratioT * newH);
            SetWindowPos(g_interaction.target, nullptr, newLeft, newTop, newW, newH, SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
            g_interaction.windowRect = { newLeft, newTop, newLeft + newW, newTop + newH };

            // Corner radius / invisible-border margins differ once restored.
            PrepareOverlayMetrics(g_interaction.target);

            g_interaction.lastPoint = pt;
            g_interaction.resizeHandle = GetClosestHandle(pt, g_interaction.windowRect);
        }
    }

    int dx = pt.x - g_interaction.lastPoint.x;
    int dy = pt.y - g_interaction.lastPoint.y;

    // Re-evaluate closest handle as the cursor moves
    ResizeHandle newHandle = GetClosestHandle(pt, g_interaction.windowRect);
    if (newHandle != g_interaction.resizeHandle)
    {
        g_interaction.resizeHandle = newHandle;
        HCURSOR hCur = CursorForHandle(g_interaction.resizeHandle);
        SetClassLongPtrW(g_hOverlay, GCLP_HCURSOR, reinterpret_cast<LONG_PTR>(hCur));
        SetCursor(hCur);
    }

    // Apply delta to the edges indicated by the current handle
    RECT nr = g_interaction.windowRect;
    switch (g_interaction.resizeHandle)
    {
    case ResizeHandle::TopLeft:
        nr.left += dx;
        nr.top += dy;
        break;
    case ResizeHandle::Top:
        nr.top += dy;
        break;
    case ResizeHandle::TopRight:
        nr.right += dx;
        nr.top += dy;
        break;
    case ResizeHandle::Right:
        nr.right += dx;
        break;
    case ResizeHandle::BottomRight:
        nr.right += dx;
        nr.bottom += dy;
        break;
    case ResizeHandle::Bottom:
        nr.bottom += dy;
        break;
    case ResizeHandle::BottomLeft:
        nr.left += dx;
        nr.bottom += dy;
        break;
    case ResizeHandle::Left:
        nr.left += dx;
        break;
    default:
        break;
    }

    // Enforce minimum window size
    bool leftMoving =
        g_interaction.resizeHandle == ResizeHandle::TopLeft ||
        g_interaction.resizeHandle == ResizeHandle::BottomLeft ||
        g_interaction.resizeHandle == ResizeHandle::Left;
    bool topMoving =
        g_interaction.resizeHandle == ResizeHandle::TopLeft ||
        g_interaction.resizeHandle == ResizeHandle::Top ||
        g_interaction.resizeHandle == ResizeHandle::TopRight;

    if (nr.right - nr.left < MIN_WINDOW_WIDTH)
    {
        if (leftMoving)
            nr.left = nr.right - MIN_WINDOW_WIDTH;
        else
            nr.right = nr.left + MIN_WINDOW_WIDTH;
    }
    if (nr.bottom - nr.top < MIN_WINDOW_HEIGHT)
    {
        if (topMoving)
            nr.top = nr.bottom - MIN_WINDOW_HEIGHT;
        else
            nr.bottom = nr.top + MIN_WINDOW_HEIGHT;
    }

    g_interaction.windowRect = nr;
    g_interaction.lastPoint = pt;

    int w = nr.right - nr.left;
    int h = nr.bottom - nr.top;

    // Move target + overlay (separate SetWindowPos – DeferWindowPos doesn't
    // work reliably for cross-process target windows)
    SetWindowPos(g_interaction.target, nullptr, nr.left, nr.top, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
    RepositionOverlay(nr.left, nr.top, w, h);
}

static void ApplyPendingSettings()
{
    std::optional<SettingsSnapshot> settings;
    {
        std::scoped_lock lock(g_pendingSettingsMutex);
        settings.swap(g_pendingSettings);
    }

    if (!settings)
    {
        return;
    }

    const bool geometryChanged = settings->showGeometry != g_settings.showGeometry;
    g_settings = std::move(*settings);
    g_excludedCache.clear();

    if (geometryChanged &&
        g_hOverlay &&
        IsWindowVisible(g_hOverlay) &&
        g_overlayInfoW > 0 &&
        g_overlayInfoH > 0)
    {
        RenderOverlayContent(g_hOverlay, g_overlayInfoW, g_overlayInfoH);
    }
}

static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_APPLY_SETTINGS:
        ApplyPendingSettings();
        return 0;

    case WM_CLOSE:
        DestroyWindow(hwnd);
        return 0;

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }

    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

static LRESULT CALLBACK OverlayWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_QUERYENDSESSION:
        return TRUE;

    case WM_ENDSESSION:
        if (wParam)
        {
            PostQuitMessage(0);
        }
        return 0;
    }

    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

// ---------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------
static constexpr wchar_t INSTANCE_MUTEX_NAME[] = L"Local\\PowerToys_GrabAndMove_InstanceMutex";

static void CleanupRuntime()
{
    if (g_hStopSettingsEvent)
    {
        SetEvent(g_hStopSettingsEvent);
    }
    if (g_settingsThread.joinable())
    {
        g_settingsThread.join();
    }

    if (g_hhkKeyboard)
    {
        UnhookWindowsHookEx(g_hhkKeyboard);
        g_hhkKeyboard = nullptr;
    }
    if (g_hhkMouse)
    {
        UnhookWindowsHookEx(g_hhkMouse);
        g_hhkMouse = nullptr;
    }
    if (g_hWinEventHook)
    {
        UnhookWinEvent(g_hWinEventHook);
        g_hWinEventHook = nullptr;
    }

    if (g_hOverlay)
    {
        DestroyWindow(g_hOverlay);
        g_hOverlay = nullptr;
    }
    if (g_hMsgWnd)
    {
        if (IsWindow(g_hMsgWnd))
        {
            DestroyWindow(g_hMsgWnd);
        }
        g_hMsgWnd = nullptr;
    }

    if (g_hReloadSettingsEvent)
    {
        CloseHandle(g_hReloadSettingsEvent);
        g_hReloadSettingsEvent = nullptr;
    }
    if (g_hExitEvent)
    {
        CloseHandle(g_hExitEvent);
        g_hExitEvent = nullptr;
    }
    if (g_hStopSettingsEvent)
    {
        CloseHandle(g_hStopSettingsEvent);
        g_hStopSettingsEvent = nullptr;
    }
    if (g_hInstanceMutex)
    {
        CloseHandle(g_hInstanceMutex);
        g_hInstanceMutex = nullptr;
    }

    if (g_gdiplusToken)
    {
        Gdiplus::GdiplusShutdown(g_gdiplusToken);
        g_gdiplusToken = 0;
    }
    if (g_traceRegistered)
    {
        TraceLoggingUnregister(g_hProvider);
        g_traceRegistered = false;
    }
}

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE, LPWSTR lpCmdLine, int)
{
    g_hInstance = hInstance;
    TraceLoggingRegister(g_hProvider);
    g_traceRegistered = true;

    // Prevent multiple instances
    g_hInstanceMutex = CreateMutexW(nullptr, TRUE, INSTANCE_MUTEX_NAME);
    if (!g_hInstanceMutex || GetLastError() == ERROR_ALREADY_EXISTS)
    {
        CleanupRuntime();
        return 1;
    }

    // Require runner PID on the command line; refuse to run standalone
    std::wstring pid = std::wstring(lpCmdLine);
    if (pid.empty())
    {
        MessageBoxW(nullptr, L"GrabAndMove can't run as a standalone. Start it from PowerToys.", L"GrabAndMove", MB_ICONERROR);
        CleanupRuntime();
        return 1;
    }

    SettingsSnapshot initialSettings;
    if (TryLoadSettingsFromFile(g_settings, initialSettings))
    {
        g_settings = std::move(initialSettings);
    }

    // Open the named event for settings reload notifications
    g_hReloadSettingsEvent = CreateEventW(
        nullptr,
        FALSE,
        FALSE,
        CommonSharedConstants::GRABANDMOVE_REFRESH_SETTINGS_EVENT);

    // Open the named event for exit signal from the module interface
    g_hExitEvent = CreateEventW(
        nullptr,
        FALSE,
        FALSE,
        CommonSharedConstants::GRABANDMOVE_EXIT_EVENT);
    g_hStopSettingsEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    if (!g_hReloadSettingsEvent || !g_hExitEvent || !g_hStopSettingsEvent)
    {
        CleanupRuntime();
        return 1;
    }

    // Initialise GDI+ for antialiased overlay border rendering
    Gdiplus::GdiplusStartupInput gdiplusStartupInput;
    if (Gdiplus::GdiplusStartup(&g_gdiplusToken, &gdiplusStartupInput, nullptr) != Gdiplus::Ok)
    {
        CleanupRuntime();
        return 1;
    }

    // Register a message-only window class
    WNDCLASSEXW wc = {};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = WndProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = CLASS_NAME;
    if (!RegisterClassExW(&wc))
    {
        CleanupRuntime();
        return 1;
    }

    // Register the overlay window class (layered per-pixel-alpha surface, ARROW cursor)
    WNDCLASSEXW overlayWindowClass = {};
    overlayWindowClass.cbSize = sizeof(overlayWindowClass);
    overlayWindowClass.lpfnWndProc = OverlayWndProc;
    overlayWindowClass.hInstance = hInstance;
    overlayWindowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    overlayWindowClass.hbrBackground = nullptr; // per-pixel alpha via UpdateLayeredWindow
    overlayWindowClass.lpszClassName = OVERLAY_CLASS_NAME;
    if (!RegisterClassExW(&overlayWindowClass))
    {
        CleanupRuntime();
        return 1;
    }

    // Create a message-only window (invisible)
    g_hMsgWnd = CreateWindowExW(0, CLASS_NAME, APP_TITLE, 0, 0, 0, 0, 0, HWND_MESSAGE, nullptr, hInstance, nullptr);
    if (!g_hMsgWnd)
    {
        CleanupRuntime();
        return 1;
    }

    // The message-only window does not receive session-end broadcasts. Keep a
    // hidden top-level overlay window so the process can exit its message loop.
    EnsureOverlayWindow();
    if (!g_hOverlay)
    {
        CleanupRuntime();
        return 1;
    }

    // Pre-load system cursors (fix #6 – avoid LoadCursorW in hot path)
    g_curSizeAll = LoadCursorW(nullptr, IDC_SIZEALL);
    g_curSizeNWSE = LoadCursorW(nullptr, IDC_SIZENWSE);
    g_curSizeNESW = LoadCursorW(nullptr, IDC_SIZENESW);
    g_curSizeWE = LoadCursorW(nullptr, IDC_SIZEWE);
    g_curSizeNS = LoadCursorW(nullptr, IDC_SIZENS);

    // Install global low-level hooks
    g_hhkKeyboard = SetWindowsHookExW(WH_KEYBOARD_LL, KeyboardProc, hInstance, 0);
    g_hhkMouse = SetWindowsHookExW(WH_MOUSE_LL, MouseProc, hInstance, 0);

    // Detect foreground switches to elevated processes (where key-up events stop arriving)
    g_hWinEventHook = SetWinEventHook(
        EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, nullptr, WinEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);

    if (!g_hhkKeyboard || !g_hhkMouse)
    {
        MessageBoxW(
            nullptr,
            L"Failed to install global hooks.\n"
            L"Try running as Administrator.",
            APP_TITLE,
            MB_ICONERROR | MB_OK);
        CleanupRuntime();
        return 1;
    }

    try
    {
        g_settingsThread = std::thread(SettingsWatcherThread, g_settings);
    }
    catch (...)
    {
        CleanupRuntime();
        return 1;
    }

    const HWND messageWindow = g_hMsgWnd;
    ProcessWaiter::OnProcessTerminate(pid, [messageWindow](DWORD) {
        PostMessage(messageWindow, WM_CLOSE, 0, 0);
    });

    // Message loop – required for low-level hooks to function
    MSG msg{};
    int exitCode = 0;
    for (;;)
    {
        const BOOL result = GetMessageW(&msg, nullptr, 0, 0);
        if (result == 0)
        {
            exitCode = static_cast<int>(msg.wParam);
            break;
        }
        if (result == -1)
        {
            exitCode = 1;
            break;
        }

        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    CleanupRuntime();
    return exitCode;
}
