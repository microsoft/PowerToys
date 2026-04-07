#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif

#include <windows.h>
#include <shellapi.h>
#include <commctrl.h>
#include <string>
#include <thread>

#include <common/SettingsAPI/settings_objects.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/process_path.h>
#include <common/utils/excluded_apps.h>

#include "resource.h"

// Disable compiler warning 26451
#pragma warning(disable : 26451)

// ---------------------------------------------------------------------------
// Globals
// ---------------------------------------------------------------------------
static HINSTANCE g_hInstance   = nullptr;
static HHOOK     g_hhkKeyboard = nullptr;
static HHOOK     g_hhkMouse    = nullptr;
static HWND      g_hMsgWnd     = nullptr;

static bool      g_altPressed  = false;
static bool      g_dragging    = false;
static bool      g_dragFirstMove = false; // true until first WM_MOUSEMOVE of a drag
static HWND      g_dragTarget  = nullptr;
static POINT     g_dragStart   = {};     // cursor pos at drag start
static RECT      g_dragWndRect = {};     // window rect at drag start
static HWND      g_hOverlay    = nullptr; // semi-transparent overlay during drag

static bool      g_shouldAbsorbAlt = false; // true if we want to absorb Alt on the next keydown (set when Alt is pressed without dragging, cleared on next non-Alt key or Alt keyup)
static bool      g_altAbsorbed     = false; // true if we absorbed an Alt keydown
static bool      g_dragConsumedAlt = false; // true if a drag consumed the absorbed Alt
static DWORD     g_absorbedVk      = 0;     // VK code of absorbed Alt key
static DWORD     g_absorbedScanCode = 0;    // scan code for replay
static DWORD     g_absorbedFlags   = 0;     // flags for replay (extended key, etc.)

// Resize handle identifiers
enum ResizeHandle {
    RESIZE_NONE,
    RESIZE_TOP_LEFT,
    RESIZE_TOP,
    RESIZE_TOP_RIGHT,
    RESIZE_RIGHT,
    RESIZE_BOTTOM_RIGHT,
    RESIZE_BOTTOM,
    RESIZE_BOTTOM_LEFT,
    RESIZE_LEFT
};

static bool         g_resizing        = false;
static bool         g_resizeFirstMove = false;
static HWND         g_resizeTarget    = nullptr;
static POINT        g_resizeLast      = {};     // cursor pos from previous frame
static RECT         g_resizeWndRect   = {};     // current window rect (updated each frame)
static ResizeHandle g_currentHandle   = RESIZE_NONE;

static const int MIN_WINDOW_WIDTH  = 150;
static const int MIN_WINDOW_HEIGHT = 50;

static const DWORD THROTTLE_INTERVAL_MS = 8; // min ms between SetWindowPos calls, aim for 125 fps
static DWORD       g_lastMoveTick       = 0;  // tick of last applied move/resize

static const wchar_t* const CLASS_NAME         = L"WinPos_MsgWnd";
static const wchar_t* const OVERLAY_CLASS_NAME  = L"WinPos_Overlay";
static const wchar_t* const APP_TITLE           = L"WinPos";

// Must match CommonSharedConstants::WINPOS_REFRESH_SETTINGS_EVENT in shared_constants.h
static const wchar_t* const WINPOS_REFRESH_SETTINGS_EVENT = L"Local\\PowerToysWinPos-RefreshSettingsEvent-a7b3c1d2-4e5f-6a7b-8c9d-0e1f2a3b4c5d";

static std::vector<std::wstring> g_excludedApps;

static HANDLE g_hReloadSettingsEvent = nullptr;
static std::thread g_settingsThread;
static bool g_running = true;

// ---------------------------------------------------------------------------
// Settings file helpers
// ---------------------------------------------------------------------------
static void LoadSettingsFromFile() {
    try
    {
        PowerToysSettings::PowerToyValues values =
            PowerToysSettings::PowerToyValues::load_from_settings_file(L"WinPos");

        if (auto v = values.get_bool_value(L"shouldAbsorbAlt"))
        {
            g_shouldAbsorbAlt = *v;
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

            g_excludedApps = std::move(apps);
        }
    }
    catch (...)
    {
        // Keep defaults on error
    }
}

static void SettingsWatcherThread() {
    while (g_running) {
        DWORD wait = WaitForSingleObject(g_hReloadSettingsEvent, 1000);
        if (!g_running) break;
        if (wait == WAIT_OBJECT_0) {
            LoadSettingsFromFile();
        }
    }
}

// ---------------------------------------------------------------------------
// Overlay window helpers
// ---------------------------------------------------------------------------
static LRESULT CALLBACK OverlayWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    // WM_NCHITTEST -> HTTRANSPARENT so clicks pass through to windows below,
    // except we still get the SIZEALL cursor because we set it on the class.
    // Actually, HTTRANSPARENT makes the window invisible to hit-testing, so
    // the cursor won't show.  Instead, return HTCLIENT and let the class
    // cursor do the work – the hook swallows the clicks anyway.
    if (msg == WM_NCHITTEST)
        return HTCLIENT;
    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

static void CreateOverlay(const RECT& rc, LPCWSTR cursorName) {
    if (g_hOverlay) {
        DestroyWindow(g_hOverlay);
        g_hOverlay = nullptr;
    }

    int w = rc.right  - rc.left;
    int h = rc.bottom - rc.top;

    g_hOverlay = CreateWindowExW(
        WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
        OVERLAY_CLASS_NAME,
        nullptr,
        WS_POPUP | WS_VISIBLE,
        rc.left, rc.top, w, h,
        nullptr, nullptr, g_hInstance, nullptr);
    SetClassLongPtrW(g_hOverlay, GCLP_HCURSOR,
        reinterpret_cast<LONG_PTR>(LoadCursorW(nullptr, cursorName)));
    

    if (g_hOverlay) {
        // White background at 30 % opacity  (255 * 0.30 ≈ 76)
        SetLayeredWindowAttributes(g_hOverlay, 0, 76, LWA_ALPHA);
    }
}

static void MoveOverlay(int x, int y, int w, int h) {
    if (g_hOverlay) {
        SetWindowPos(g_hOverlay, HWND_TOPMOST, x, y, w, h,
                     SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }
}

static void DestroyOverlay() {
    if (g_hOverlay) {
        DestroyWindow(g_hOverlay);
        g_hOverlay = nullptr;
    }
}

static void StopDragging()
{
    g_dragging = false;
    g_dragTarget = nullptr;
    DestroyOverlay();
}

static ResizeHandle GetClosestHandle(POINT pt, const RECT& rc) {
    int cx = (rc.left + rc.right)  / 2;
    int cy = (rc.top  + rc.bottom) / 2;

    struct { int x; int y; ResizeHandle h; } handles[] = {
        { rc.left,  rc.top,    RESIZE_TOP_LEFT     },
        { cx,       rc.top,    RESIZE_TOP           },
        { rc.right, rc.top,    RESIZE_TOP_RIGHT    },
        { rc.right, cy,        RESIZE_RIGHT         },
        { rc.right, rc.bottom, RESIZE_BOTTOM_RIGHT },
        { cx,       rc.bottom, RESIZE_BOTTOM        },
        { rc.left,  rc.bottom, RESIZE_BOTTOM_LEFT  },
        { rc.left,  cy,        RESIZE_LEFT          },
    };

    ResizeHandle closest = RESIZE_BOTTOM_RIGHT;
    LONG minDist = MAXLONG;
    for (auto& e : handles) {
        LONG dx = pt.x - e.x;
        LONG dy = pt.y - e.y;
        LONG dist = dx * dx + dy * dy;
        if (dist < minDist) {
            minDist = dist;
            closest = e.h;
        }
    }
    return closest;
}

static LPCWSTR CursorForHandle(ResizeHandle handle) {
    switch (handle) {
    case RESIZE_TOP_LEFT:     case RESIZE_BOTTOM_RIGHT: return IDC_SIZENWSE;
    case RESIZE_TOP_RIGHT:    case RESIZE_BOTTOM_LEFT:  return IDC_SIZENESW;
    case RESIZE_TOP:          case RESIZE_BOTTOM:       return IDC_SIZENS;
    case RESIZE_LEFT:         case RESIZE_RIGHT:        return IDC_SIZEWE;
    default:                                            return IDC_ARROW;
    }
}

static void StopResizing()
{
    g_resizing = false;
    g_resizeTarget = nullptr;
    g_currentHandle = RESIZE_NONE;
    DestroyOverlay();
}

static void ReplayAbsorbedAlt() {
    INPUT inp = {};
    inp.type = INPUT_KEYBOARD;
    inp.ki.wVk    = static_cast<WORD>(g_absorbedVk);
    inp.ki.wScan  = static_cast<WORD>(g_absorbedScanCode);
    inp.ki.dwFlags = (g_absorbedFlags & LLKHF_EXTENDED) ? KEYEVENTF_EXTENDEDKEY : 0;
    SendInput(1, &inp, sizeof(INPUT));
}

// ---------------------------------------------------------------------------
// System tray helpers
// ---------------------------------------------------------------------------
static NOTIFYICONDATAW g_nid = {};

static void AddTrayIcon(HWND hwnd) {
    g_nid.cbSize           = sizeof(NOTIFYICONDATAW);
    g_nid.hWnd             = hwnd;
    g_nid.uID              = 1;
    g_nid.uFlags           = NIF_ICON | NIF_MESSAGE | NIF_TIP;
    g_nid.uCallbackMessage = WM_TRAYICON;
    g_nid.hIcon            = LoadIconW(g_hInstance, MAKEINTRESOURCEW(IDI_APP_ICON));
    wcscpy_s(g_nid.szTip, APP_TITLE);
    Shell_NotifyIconW(NIM_ADD, &g_nid);
}

static void RemoveTrayIcon() {
    Shell_NotifyIconW(NIM_DELETE, &g_nid);
}

static void ShowTrayMenu(HWND hwnd) {
    HMENU hMenu = CreatePopupMenu();
    if (!hMenu) return;

    AppendMenuW(hMenu, MF_STRING, IDM_EXIT, L"Exit");

    POINT pt;
    GetCursorPos(&pt);

    // Required so the menu dismisses when clicking outside it
    SetForegroundWindow(hwnd);
    TrackPopupMenu(hMenu, TPM_RIGHTBUTTON, pt.x, pt.y, 0, hwnd, nullptr);
    PostMessageW(hwnd, WM_NULL, 0, 0);

    DestroyMenu(hMenu);
}

// ---------------------------------------------------------------------------
// Hook callbacks
// ---------------------------------------------------------------------------
static LRESULT CALLBACK KeyboardProc(int nCode, WPARAM wParam, LPARAM lParam) {
    if (nCode >= 0) {
        auto* kb = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);

        // Ignore injected events (includes our own replayed keys)
        if (kb->flags & LLKHF_INJECTED)
            goto forward;

        bool isAltKey = (kb->vkCode == VK_MENU || kb->vkCode == VK_LMENU || kb->vkCode == VK_RMENU);

        if (isAltKey) {
            if (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN) {
                g_altPressed = true;
                if (g_shouldAbsorbAlt && !g_altAbsorbed) {
                    g_altAbsorbed      = true;
                    g_dragConsumedAlt  = false;
                    g_absorbedVk       = kb->vkCode;
                    g_absorbedScanCode = kb->scanCode;
                    g_absorbedFlags    = kb->flags;
                    return 1; // swallow the Alt keydown
                }
            } else if (wParam == WM_KEYUP || wParam == WM_SYSKEYUP) {
                g_altPressed = false;
                bool wasDragging = g_dragging;
                bool wasResizing = g_resizing;
                if (g_dragging) {
                    StopDragging();
                }
                if (g_resizing) {
                    StopResizing();
                }
                if (g_altAbsorbed) {
                    g_altAbsorbed = false;
                    if (wasDragging || wasResizing || g_dragConsumedAlt) {
                        // Alt was used for a drag/resize; swallow the keyup too
                        g_dragConsumedAlt = false;
                        return 1;
                    }
                    // No drag happened; replay the keydown, then let keyup through
                    ReplayAbsorbedAlt();
                }
            }
        } else {
            // Non-Alt key while Alt was absorbed without a drag: replay Alt first
            if (g_altAbsorbed && !g_dragConsumedAlt) {
                g_altAbsorbed = false;
                ReplayAbsorbedAlt();
            }
        }
    }

forward:
    return CallNextHookEx(g_hhkKeyboard, nCode, wParam, lParam);
}

static bool IsSystemClass(HWND hwnd) {
    wchar_t cls[64] = {};
    GetClassNameW(hwnd, cls, 64);
    return (wcscmp(cls, L"Progman") == 0 || wcscmp(cls, L"Shell_TrayWnd") == 0);
}

static bool IsExcluded(HWND hwnd) {
    if (IsSystemClass(hwnd))
        return true;

    if (g_excludedApps.empty())
        return false;

    std::wstring processPath = get_process_path(hwnd);
    CharUpperBuffW(processPath.data(), static_cast<DWORD>(processPath.length()));

    if (find_app_name_in_path(processPath, g_excludedApps))
        return true;

    if (check_excluded_app_with_title(hwnd, g_excludedApps))
        return true;

    return false;
}

static LRESULT CALLBACK MouseProc(int nCode, WPARAM wParam, LPARAM lParam) {
    if (nCode >= 0) {
        auto* ms = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);

        // Ignore injected events to avoid feedback loops
        if (ms->flags & LLMHF_INJECTED)
            goto forward;

        // ----- Alt + Left Button Down: start drag -----
        if (wParam == WM_LBUTTONDOWN && g_altPressed) {
            POINT pt = ms->pt;
            HWND hwnd = WindowFromPoint(pt);
            if (hwnd) {
                // Walk up to the top-level owner window
                HWND root = GetAncestor(hwnd, GA_ROOT);
                if (root) hwnd = root;

                // Don't drag excluded windows (desktop, taskbar, user-excluded apps)
                if (IsExcluded(hwnd))
                    goto forward;

                g_dragging      = true;
                g_dragFirstMove = true;
                g_dragConsumedAlt = true;
                g_dragTarget    = hwnd;
                g_dragStart     = pt;
                GetWindowRect(hwnd, &g_dragWndRect);

                // Create the semi-transparent overlay on top of the target
                CreateOverlay(g_dragWndRect, IDC_SIZEALL);

                // Swallow the click so the target window doesn't get it
                return 1;
            }
        }

        // ----- Mouse Move while dragging -----
        if (wParam == WM_MOUSEMOVE && g_dragging && g_dragTarget) {
            // On the first move, restore maximized windows
            if (g_dragFirstMove) {
                g_dragFirstMove = false;
                if (IsZoomed(g_dragTarget)) {
                    // Remember the maximized width so we can place the
                    // cursor proportionally on the restored window.
                    RECT maxRect;
                    GetWindowRect(g_dragTarget, &maxRect);
                    int maxW = maxRect.right - maxRect.left;

                    // Restore the window (uses its previous normal placement)
                    ShowWindow(g_dragTarget, SW_RESTORE);

                    // Re-read the restored size
                    GetWindowRect(g_dragTarget, &g_dragWndRect);
                    int restoredW = g_dragWndRect.right - g_dragWndRect.left;
                    int restoredH = g_dragWndRect.bottom - g_dragWndRect.top;

                    // Place the restored window so the cursor keeps the same
                    // proportional X position and stays at the title bar.
                    float ratio = (maxW > 0)
                        ? static_cast<float>(g_dragStart.x - maxRect.left) / maxW
                        : 0.5f;
                    int newX = g_dragStart.x - static_cast<int>(restoredW * ratio);
                    int newY = g_dragStart.y - (GetSystemMetrics(SM_CYFRAME)
                                                + GetSystemMetrics(SM_CYCAPTION) / 2);
                    SetWindowPos(g_dragTarget, nullptr, newX, newY,
                                 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);

                    // Reset drag baseline to current cursor / new window rect
                    g_dragStart = ms->pt;
                    g_dragWndRect = { newX, newY, newX + restoredW, newY + restoredH };
                }
            }

            POINT pt = ms->pt;
            int dx = pt.x - g_dragStart.x;
            int dy = pt.y - g_dragStart.y;

            int newX = g_dragWndRect.left + dx;
            int newY = g_dragWndRect.top  + dy;
            int w    = g_dragWndRect.right  - g_dragWndRect.left;
            int h    = g_dragWndRect.bottom - g_dragWndRect.top;

            // Throttle: skip expensive window ops if < THROTTLE_INTERVAL_MS
            DWORD now = GetTickCount();
            if (now - g_lastMoveTick >= THROTTLE_INTERVAL_MS) {
                g_lastMoveTick = now;
                SetWindowPos(g_dragTarget, nullptr,
                             newX, newY, 0, 0,
                             SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
                MoveOverlay(newX, newY, w, h);
            }

            return 0;  // let the move go through
        }

        // ----- Left Button Up: end drag -----
        if (wParam == WM_LBUTTONUP && g_dragging) {
            // Flush final position (may have been throttled)
            POINT pt = ms->pt;
            int dx = pt.x - g_dragStart.x;
            int dy = pt.y - g_dragStart.y;
            int newX = g_dragWndRect.left + dx;
            int newY = g_dragWndRect.top  + dy;
            int w    = g_dragWndRect.right  - g_dragWndRect.left;
            int h    = g_dragWndRect.bottom - g_dragWndRect.top;
            SetWindowPos(g_dragTarget, nullptr,
                         newX, newY, 0, 0,
                         SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            StopDragging();
            return 1;  // swallow the release
        }

        // ----- Alt + Right Button Down: start resize -----
        if (wParam == WM_RBUTTONDOWN && g_altPressed) {
            POINT pt = ms->pt;
            HWND hwnd = WindowFromPoint(pt);
            if (hwnd) {
                HWND root = GetAncestor(hwnd, GA_ROOT);
                if (root) hwnd = root;

                if (IsExcluded(hwnd))
                    goto forward;

                g_resizing        = true;
                g_resizeFirstMove = true;
                g_dragConsumedAlt = true;
                g_resizeTarget    = hwnd;
                g_resizeLast      = pt;
                GetWindowRect(hwnd, &g_resizeWndRect);

                g_currentHandle = GetClosestHandle(pt, g_resizeWndRect);
                CreateOverlay(g_resizeWndRect, CursorForHandle(g_currentHandle));

                return 1;   // swallow the right button press
            }
        }

        // ----- Mouse Move while resizing -----
        if (wParam == WM_MOUSEMOVE && g_resizing && g_resizeTarget) {
            // On the first resize, restore maximized windows, moved proportionally around the cursor position
            if (g_resizeFirstMove) {
                g_resizeFirstMove = false;
                if (IsZoomed(g_resizeTarget)) {
                    POINT cur = ms->pt;
                    RECT maxRect = g_resizeWndRect; // maximized rect
                    int maxW = maxRect.right - maxRect.left;
                    int maxH = maxRect.bottom - maxRect.top;

                    // Cursor ratios relative to the maximized window edges
                    float ratioL = (maxW > 0) ? static_cast<float>(cur.x - maxRect.left)   / maxW : 0.5f;
                    float ratioT = (maxH > 0) ? static_cast<float>(cur.y - maxRect.top)    / maxH : 0.5f;

                    ShowWindow(g_resizeTarget, SW_RESTORE);
                    GetWindowRect(g_resizeTarget, &g_resizeWndRect);

                    int newW = g_resizeWndRect.right - g_resizeWndRect.left;
                    int newH = g_resizeWndRect.bottom - g_resizeWndRect.top;

                    // Position the restored window so the cursor keeps the same ratios
                    int newLeft = cur.x - static_cast<int>(ratioL * newW);
                    int newTop  = cur.y - static_cast<int>(ratioT * newH);
                    SetWindowPos(g_resizeTarget, nullptr, newLeft, newTop, newW, newH,
                                 SWP_NOZORDER | SWP_NOACTIVATE);
                    GetWindowRect(g_resizeTarget, &g_resizeWndRect);

                    g_resizeLast = cur;
                    g_currentHandle = GetClosestHandle(cur, g_resizeWndRect);
                }
            }

            POINT pt = ms->pt;
            int dx = pt.x - g_resizeLast.x;
            int dy = pt.y - g_resizeLast.y;

            // Re-evaluate closest handle as the cursor moves
            ResizeHandle newHandle = GetClosestHandle(pt, g_resizeWndRect);
            if (newHandle != g_currentHandle) {
                g_currentHandle = newHandle;
                HCURSOR hCur = LoadCursorW(nullptr, CursorForHandle(g_currentHandle));
                SetClassLongPtrW(g_hOverlay, GCLP_HCURSOR,
                                 reinterpret_cast<LONG_PTR>(hCur));
                SetCursor(hCur);
            }

            // Apply delta to the edges indicated by the current handle
            RECT nr = g_resizeWndRect;
            switch (g_currentHandle) {
            case RESIZE_TOP_LEFT:     nr.left += dx; nr.top    += dy; break;
            case RESIZE_TOP:                         nr.top    += dy; break;
            case RESIZE_TOP_RIGHT:    nr.right += dx; nr.top   += dy; break;
            case RESIZE_RIGHT:        nr.right += dx;                 break;
            case RESIZE_BOTTOM_RIGHT: nr.right += dx; nr.bottom += dy; break;
            case RESIZE_BOTTOM:                       nr.bottom += dy; break;
            case RESIZE_BOTTOM_LEFT:  nr.left += dx; nr.bottom += dy; break;
            case RESIZE_LEFT:         nr.left += dx;                  break;
            default: break;
            }

            // Enforce minimum window size
            bool leftMoving = (g_currentHandle == RESIZE_TOP_LEFT ||
                               g_currentHandle == RESIZE_BOTTOM_LEFT ||
                               g_currentHandle == RESIZE_LEFT);
            bool topMoving  = (g_currentHandle == RESIZE_TOP_LEFT ||
                               g_currentHandle == RESIZE_TOP ||
                               g_currentHandle == RESIZE_TOP_RIGHT);

            if (nr.right - nr.left < MIN_WINDOW_WIDTH) {
                if (leftMoving) nr.left  = nr.right - MIN_WINDOW_WIDTH;
                else            nr.right = nr.left  + MIN_WINDOW_WIDTH;
            }
            if (nr.bottom - nr.top < MIN_WINDOW_HEIGHT) {
                if (topMoving) nr.top    = nr.bottom - MIN_WINDOW_HEIGHT;
                else           nr.bottom = nr.top    + MIN_WINDOW_HEIGHT;
            }

            g_resizeWndRect = nr;
            g_resizeLast = pt;

            // Throttle: skip expensive window ops if < THROTTLE_INTERVAL_MS
            DWORD now = GetTickCount();
            if (now - g_lastMoveTick >= THROTTLE_INTERVAL_MS) {
                g_lastMoveTick = now;
                int w = nr.right  - nr.left;
                int h = nr.bottom - nr.top;
                SetWindowPos(g_resizeTarget, nullptr,
                             nr.left, nr.top, w, h,
                             SWP_NOZORDER | SWP_NOACTIVATE);
                MoveOverlay(nr.left, nr.top, w, h);
            }

            return 0;   // let the move go through
        }

        // ----- Right Button Up: end resize -----
        if (wParam == WM_RBUTTONUP && g_resizing) {
            // Flush final size (may have been throttled)
            RECT nr = g_resizeWndRect;
            int w = nr.right  - nr.left;
            int h = nr.bottom - nr.top;
            SetWindowPos(g_resizeTarget, nullptr,
                         nr.left, nr.top, w, h,
                         SWP_NOZORDER | SWP_NOACTIVATE);
            StopResizing();
            return 1;   // swallow the right button release
        }
    }

forward:
    return CallNextHookEx(g_hhkMouse, nCode, wParam, lParam);
}

// ---------------------------------------------------------------------------
// Message-only window procedure
// ---------------------------------------------------------------------------
static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    switch (msg) {
    case WM_TRAYICON:
        if (LOWORD(lParam) == WM_RBUTTONUP) {
            ShowTrayMenu(hwnd);
        }
        return 0;

    case WM_COMMAND:
        if (LOWORD(wParam) == IDM_EXIT) {
            PostQuitMessage(0);
        }
        return 0;

    case WM_DESTROY:
        RemoveTrayIcon();
        PostQuitMessage(0);
        return 0;
    }

    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

// ---------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------
int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE, LPWSTR, int) {
    g_hInstance = hInstance;

    // Load initial settings from JSON before anything else
    LoadSettingsFromFile();

    // Open the named event for settings reload notifications
    g_hReloadSettingsEvent = CreateEventW(nullptr, FALSE, FALSE, WINPOS_REFRESH_SETTINGS_EVENT);
    if (g_hReloadSettingsEvent) {
        g_settingsThread = std::thread(SettingsWatcherThread);
    }

    // Ensure common controls are initialised (for Shell_NotifyIcon)
    INITCOMMONCONTROLSEX icc = { sizeof(icc), ICC_STANDARD_CLASSES };
    InitCommonControlsEx(&icc);

    // Register a message-only window class
    WNDCLASSEXW wc = {};
    wc.cbSize        = sizeof(wc);
    wc.lpfnWndProc   = WndProc;
    wc.hInstance      = hInstance;
    wc.lpszClassName  = CLASS_NAME;
    if (!RegisterClassExW(&wc)) return 1;

    // Register the overlay window class (white background, ARROW cursor)
    WNDCLASSEXW owc = {};
    owc.cbSize        = sizeof(owc);
    owc.lpfnWndProc   = OverlayWndProc;
    owc.hInstance      = hInstance;
    owc.hCursor        = LoadCursorW(nullptr, IDC_ARROW);
    owc.hbrBackground  = static_cast<HBRUSH>(GetStockObject(WHITE_BRUSH));
    owc.lpszClassName  = OVERLAY_CLASS_NAME;
    if (!RegisterClassExW(&owc)) return 1;

    // Create a message-only window (invisible)
    g_hMsgWnd = CreateWindowExW(0, CLASS_NAME, APP_TITLE,
                                0, 0, 0, 0, 0,
                                HWND_MESSAGE, nullptr, hInstance, nullptr);
    if (!g_hMsgWnd) return 1;

    // Install global low-level hooks
    g_hhkKeyboard = SetWindowsHookExW(WH_KEYBOARD_LL, KeyboardProc, hInstance, 0);
    g_hhkMouse    = SetWindowsHookExW(WH_MOUSE_LL,    MouseProc,    hInstance, 0);

    if (!g_hhkKeyboard || !g_hhkMouse) {
        MessageBoxW(nullptr,
                    L"Failed to install global hooks.\n"
                    L"Try running as Administrator.",
                    APP_TITLE, MB_ICONERROR | MB_OK);
        if (g_hhkKeyboard) UnhookWindowsHookEx(g_hhkKeyboard);
        if (g_hhkMouse)    UnhookWindowsHookEx(g_hhkMouse);
        return 1;
    }

    // Add system tray icon
    AddTrayIcon(g_hMsgWnd);

    // Message loop – required for low-level hooks to function
    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0) > 0) {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    // Cleanup
    g_running = false;
    if (g_hReloadSettingsEvent) {
        SetEvent(g_hReloadSettingsEvent); // wake the thread so it exits
    }
    if (g_settingsThread.joinable()) {
        g_settingsThread.join();
    }
    if (g_hReloadSettingsEvent) {
        CloseHandle(g_hReloadSettingsEvent);
    }
    UnhookWindowsHookEx(g_hhkKeyboard);
    UnhookWindowsHookEx(g_hhkMouse);
    RemoveTrayIcon();

    return static_cast<int>(msg.wParam);
}
