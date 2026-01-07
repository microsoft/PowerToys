//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Utility functions
//
//==============================================================================
#include "pch.h"
#include "Utility.h"
#include <string>

#pragma comment(lib, "uxtheme.lib")

//----------------------------------------------------------------------------
// Dark Mode - Static/Global State
//----------------------------------------------------------------------------
static bool g_darkModeInitialized = false;
static bool g_darkModeEnabled = false;
static HBRUSH g_darkBackgroundBrush = nullptr;
static HBRUSH g_darkControlBrush = nullptr;
static HBRUSH g_darkSurfaceBrush = nullptr;

// Preferred App Mode values for Windows 10/11 dark mode
enum class PreferredAppMode
{
    Default,
    AllowDark,
    ForceDark,
    ForceLight,
    Max
};

// Undocumented ordinals from uxtheme.dll for dark mode support
using fnSetPreferredAppMode = PreferredAppMode(WINAPI*)(PreferredAppMode appMode);
using fnAllowDarkModeForWindow = bool(WINAPI*)(HWND hWnd, bool allow);
using fnShouldAppsUseDarkMode = bool(WINAPI*)();
using fnRefreshImmersiveColorPolicyState = void(WINAPI*)();
using fnFlushMenuThemes = void(WINAPI*)();

static fnSetPreferredAppMode pSetPreferredAppMode = nullptr;
static fnAllowDarkModeForWindow pAllowDarkModeForWindow = nullptr;
static fnShouldAppsUseDarkMode pShouldAppsUseDarkMode = nullptr;
static fnRefreshImmersiveColorPolicyState pRefreshImmersiveColorPolicyState = nullptr;
static fnFlushMenuThemes pFlushMenuThemes = nullptr;

//----------------------------------------------------------------------------
//
// InitializeDarkModeSupport
//
// Initialize dark mode function pointers from uxtheme.dll
//
//----------------------------------------------------------------------------
static void InitializeDarkModeSupport()
{
    if (g_darkModeInitialized)
        return;

    g_darkModeInitialized = true;

    HMODULE hUxTheme = GetModuleHandleW(L"uxtheme.dll");
    if (hUxTheme)
    {
        // These are undocumented ordinal exports
        // Ordinal 135: SetPreferredAppMode (Windows 10 1903+)
        pSetPreferredAppMode = reinterpret_cast<fnSetPreferredAppMode>(
            GetProcAddress(hUxTheme, MAKEINTRESOURCEA(135)));
        // Ordinal 133: AllowDarkModeForWindow
        pAllowDarkModeForWindow = reinterpret_cast<fnAllowDarkModeForWindow>(
            GetProcAddress(hUxTheme, MAKEINTRESOURCEA(133)));
        // Ordinal 132: ShouldAppsUseDarkMode
        pShouldAppsUseDarkMode = reinterpret_cast<fnShouldAppsUseDarkMode>(
            GetProcAddress(hUxTheme, MAKEINTRESOURCEA(132)));
        // Ordinal 104: RefreshImmersiveColorPolicyState
        pRefreshImmersiveColorPolicyState = reinterpret_cast<fnRefreshImmersiveColorPolicyState>(
            GetProcAddress(hUxTheme, MAKEINTRESOURCEA(104)));
        // Ordinal 136: FlushMenuThemes
        pFlushMenuThemes = reinterpret_cast<fnFlushMenuThemes>(
            GetProcAddress(hUxTheme, MAKEINTRESOURCEA(136)));

        // Allow dark mode for the application
        if (pSetPreferredAppMode)
        {
            // Use ForceDark when system is in dark mode, otherwise AllowDark
            // This ensures popup menus follow the dark theme
            if (pShouldAppsUseDarkMode && pShouldAppsUseDarkMode())
            {
                pSetPreferredAppMode(PreferredAppMode::ForceDark);
            }
            else
            {
                pSetPreferredAppMode(PreferredAppMode::AllowDark);
            }
        }

        // Flush menu themes to apply dark mode to context menus
        if (pFlushMenuThemes)
        {
            pFlushMenuThemes();
        }
    }

    // Check initial dark mode state
    RefreshDarkModeState();
}

//----------------------------------------------------------------------------
//
// IsDarkModeEnabled
//
//----------------------------------------------------------------------------
bool IsDarkModeEnabled()
{
    InitializeDarkModeSupport();

    // Check the undocumented API first
    if (pShouldAppsUseDarkMode)
    {
        return pShouldAppsUseDarkMode();
    }

    // Fallback: Check registry for system theme preference
    HKEY hKey;
    if (RegOpenKeyExW(HKEY_CURRENT_USER,
        L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
        0, KEY_READ, &hKey) == ERROR_SUCCESS)
    {
        DWORD value = 1;
        DWORD size = sizeof(value);
        RegQueryValueExW(hKey, L"AppsUseLightTheme", nullptr, nullptr,
            reinterpret_cast<LPBYTE>(&value), &size);
        RegCloseKey(hKey);
        return value == 0; // 0 = dark mode, 1 = light mode
    }

    return false;
}

//----------------------------------------------------------------------------
//
// RefreshDarkModeState
//
//----------------------------------------------------------------------------
void RefreshDarkModeState()
{
    InitializeDarkModeSupport();

    if (pRefreshImmersiveColorPolicyState)
    {
        pRefreshImmersiveColorPolicyState();
    }

    // Update preferred app mode based on current system theme
    if (pSetPreferredAppMode && pShouldAppsUseDarkMode)
    {
        if (pShouldAppsUseDarkMode())
        {
            pSetPreferredAppMode(PreferredAppMode::ForceDark);
        }
        else
        {
            pSetPreferredAppMode(PreferredAppMode::ForceLight);
        }
    }

    // Flush menu themes to apply dark mode to context menus
    if (pFlushMenuThemes)
    {
        pFlushMenuThemes();
    }

    g_darkModeEnabled = IsDarkModeEnabled();
}

//----------------------------------------------------------------------------
//
// SetDarkModeForWindow
//
//----------------------------------------------------------------------------
void SetDarkModeForWindow(HWND hWnd, bool enable)
{
    InitializeDarkModeSupport();

    if (pAllowDarkModeForWindow)
    {
        pAllowDarkModeForWindow(hWnd, enable);
    }

    // Use DWMWA_USE_IMMERSIVE_DARK_MODE attribute (Windows 10 build 17763+)
    // Attribute 20 is DWMWA_USE_IMMERSIVE_DARK_MODE
    BOOL useDarkMode = enable ? TRUE : FALSE;
    HMODULE hDwmapi = GetModuleHandleW(L"dwmapi.dll");
    if (hDwmapi)
    {
        using fnDwmSetWindowAttribute = HRESULT(WINAPI*)(HWND, DWORD, LPCVOID, DWORD);
        auto pDwmSetWindowAttribute = reinterpret_cast<fnDwmSetWindowAttribute>(
            GetProcAddress(hDwmapi, "DwmSetWindowAttribute"));
        if (pDwmSetWindowAttribute)
        {
            // Try attribute 20 first (Windows 11 / newer Windows 10)
            HRESULT hr = pDwmSetWindowAttribute(hWnd, 20, &useDarkMode, sizeof(useDarkMode));
            if (FAILED(hr))
            {
                // Fall back to attribute 19 (older Windows 10)
                pDwmSetWindowAttribute(hWnd, 19, &useDarkMode, sizeof(useDarkMode));
            }
        }
    }
}

//----------------------------------------------------------------------------
//
// GetDarkModeBrush / GetDarkModeControlBrush / GetDarkModeSurfaceBrush
//
//----------------------------------------------------------------------------
HBRUSH GetDarkModeBrush()
{
    if (!g_darkBackgroundBrush)
    {
        g_darkBackgroundBrush = CreateSolidBrush(DarkMode::BackgroundColor);
    }
    return g_darkBackgroundBrush;
}

HBRUSH GetDarkModeControlBrush()
{
    if (!g_darkControlBrush)
    {
        g_darkControlBrush = CreateSolidBrush(DarkMode::ControlColor);
    }
    return g_darkControlBrush;
}

HBRUSH GetDarkModeSurfaceBrush()
{
    if (!g_darkSurfaceBrush)
    {
        g_darkSurfaceBrush = CreateSolidBrush(DarkMode::SurfaceColor);
    }
    return g_darkSurfaceBrush;
}

//----------------------------------------------------------------------------
//
// ApplyDarkModeToDialog
//
//----------------------------------------------------------------------------
void ApplyDarkModeToDialog(HWND hDlg)
{
    if (IsDarkModeEnabled())
    {
        SetDarkModeForWindow(hDlg, true);

        // Set dark theme for the dialog
        SetWindowTheme(hDlg, L"DarkMode_Explorer", nullptr);

        // Apply dark theme to common controls (buttons, edit boxes, etc.)
        EnumChildWindows(hDlg, [](HWND hChild, LPARAM) -> BOOL {
            wchar_t className[64] = { 0 };
            GetClassNameW(hChild, className, _countof(className));

            // Apply appropriate theme based on control type
            if (_wcsicmp(className, L"Button") == 0)
            {
                // Check if this is a checkbox or radio button
                LONG style = GetWindowLong(hChild, GWL_STYLE);
                LONG buttonType = style & BS_TYPEMASK;
                if (buttonType == BS_CHECKBOX || buttonType == BS_AUTOCHECKBOX ||
                    buttonType == BS_3STATE || buttonType == BS_AUTO3STATE ||
                    buttonType == BS_RADIOBUTTON || buttonType == BS_AUTORADIOBUTTON)
                {
                    // Subclass checkbox/radio for dark mode painting - but keep DarkMode_Explorer theme
                    // for proper hit testing (empty theme can break mouse interaction)
                    SetWindowTheme(hChild, L"DarkMode_Explorer", nullptr);
                    SetWindowSubclass(hChild, CheckboxSubclassProc, 2, 0);
                }
                else if (buttonType == BS_GROUPBOX)
                {
                    // Subclass group box for dark mode painting
                    SetWindowTheme(hChild, L"", L"");
                    SetWindowSubclass(hChild, GroupBoxSubclassProc, 4, 0);
                }
                else
                {
                    SetWindowTheme(hChild, L"DarkMode_Explorer", nullptr);
                }
            }
            else if (_wcsicmp(className, L"Edit") == 0)
            {
                // Use empty theme and subclass for dark mode border drawing
                SetWindowTheme(hChild, L"", L"");
                SetWindowSubclass(hChild, EditControlSubclassProc, 3, 0);
            }
            else if (_wcsicmp(className, L"ComboBox") == 0)
            {
                SetWindowTheme(hChild, L"DarkMode_CFD", nullptr);
            }
            else if (_wcsicmp(className, L"SysListView32") == 0 ||
                     _wcsicmp(className, L"SysTreeView32") == 0)
            {
                SetWindowTheme(hChild, L"DarkMode_Explorer", nullptr);
            }
            else if (_wcsicmp(className, L"msctls_trackbar32") == 0)
            {
                // Subclass trackbar controls for dark mode painting
                SetWindowTheme(hChild, L"", L"");
                SetWindowSubclass(hChild, SliderSubclassProc, 1, 0);
            }
            else if (_wcsicmp(className, L"SysTabControl32") == 0)
            {
                // Use empty theme for tab control to allow dark background
                SetWindowTheme(hChild, L"", L"");
            }
            else if (_wcsicmp(className, L"msctls_updown32") == 0)
            {
                SetWindowTheme(hChild, L"DarkMode_Explorer", nullptr);
            }
            else if (_wcsicmp(className, L"msctls_hotkey32") == 0)
            {
                // Subclass hotkey controls for dark mode painting
                SetWindowTheme(hChild, L"", L"");
                SetWindowSubclass(hChild, HotkeyControlSubclassProc, 1, 0);
            }
            else if (_wcsicmp(className, L"Static") == 0)
            {
                // Check if this is a text label (not an owner-draw or image control)
                LONG style = GetWindowLong(hChild, GWL_STYLE);
                LONG staticType = style & SS_TYPEMASK;
                
                wchar_t text[128] = { 0 };
                GetWindowTextW(hChild, text, _countof(text));
                OutputDebugStringW((std::wstring(L"[Dark] Static '") + text + L"' style=0x" + std::to_wstring(style) + L" type=" + std::to_wstring(staticType) + L"\n").c_str());
                
                if (staticType == SS_LEFT || staticType == SS_CENTER || staticType == SS_RIGHT ||
                    staticType == SS_LEFTNOWORDWRAP || staticType == SS_SIMPLE)
                {
                    // Subclass text labels for proper dark mode painting
                    OutputDebugStringW((std::wstring(L"[Dark] Subclassing static: ") + text + L"\n").c_str());
                    SetWindowTheme(hChild, L"", L"");
                    SetWindowSubclass(hChild, StaticTextSubclassProc, 5, 0);
                }
                else
                {
                    // Other static controls (icons, bitmaps, frames) - just remove theme
                    SetWindowTheme(hChild, L"", L"");
                }
            }
            else
            {
                SetWindowTheme(hChild, L"DarkMode_Explorer", nullptr);
            }
            return TRUE;
        }, 0);
    }
    else
    {
        // Light mode - remove dark mode
        SetDarkModeForWindow(hDlg, false);
        SetWindowTheme(hDlg, nullptr, nullptr);
        
        EnumChildWindows(hDlg, [](HWND hChild, LPARAM) -> BOOL {
            // Remove subclass from controls
            wchar_t className[64] = { 0 };
            GetClassNameW(hChild, className, _countof(className));
            if (_wcsicmp(className, L"msctls_hotkey32") == 0)
            {
                RemoveWindowSubclass(hChild, HotkeyControlSubclassProc, 1);
            }
            else if (_wcsicmp(className, L"msctls_trackbar32") == 0)
            {
                RemoveWindowSubclass(hChild, SliderSubclassProc, 1);
            }
            else if (_wcsicmp(className, L"Button") == 0)
            {
                LONG style = GetWindowLong(hChild, GWL_STYLE);
                LONG buttonType = style & BS_TYPEMASK;
                if (buttonType == BS_CHECKBOX || buttonType == BS_AUTOCHECKBOX ||
                    buttonType == BS_3STATE || buttonType == BS_AUTO3STATE ||
                    buttonType == BS_RADIOBUTTON || buttonType == BS_AUTORADIOBUTTON)
                {
                    RemoveWindowSubclass(hChild, CheckboxSubclassProc, 2);
                }
                else if (buttonType == BS_GROUPBOX)
                {
                    RemoveWindowSubclass(hChild, GroupBoxSubclassProc, 4);
                }
            }
            else if (_wcsicmp(className, L"Edit") == 0)
            {
                RemoveWindowSubclass(hChild, EditControlSubclassProc, 3);
            }
            else if (_wcsicmp(className, L"Static") == 0)
            {
                RemoveWindowSubclass(hChild, StaticTextSubclassProc, 5);
            }
            SetWindowTheme(hChild, nullptr, nullptr);
            return TRUE;
        }, 0);
    }
}

//----------------------------------------------------------------------------
//
// HandleDarkModeCtlColor
//
//----------------------------------------------------------------------------
HBRUSH HandleDarkModeCtlColor(HDC hdc, HWND hCtrl, UINT message)
{
    if (!IsDarkModeEnabled())
    {
        return nullptr;
    }

    switch (message)
    {
    case WM_CTLCOLORDLG:
        SetBkColor(hdc, DarkMode::BackgroundColor);
        SetTextColor(hdc, DarkMode::TextColor);
        return GetDarkModeBrush();

    case WM_CTLCOLORSTATIC:
        SetBkMode(hdc, TRANSPARENT);
        // Use dimmed color for disabled static controls
        if (!IsWindowEnabled(hCtrl))
        {
            SetTextColor(hdc, RGB(100, 100, 100));
        }
        else
        {
            SetTextColor(hdc, DarkMode::TextColor);
        }
        return GetDarkModeBrush();

    case WM_CTLCOLORBTN:
        SetBkColor(hdc, DarkMode::ControlColor);
        SetTextColor(hdc, DarkMode::TextColor);
        return GetDarkModeControlBrush();

    case WM_CTLCOLOREDIT:
        SetBkColor(hdc, DarkMode::SurfaceColor);
        SetTextColor(hdc, DarkMode::TextColor);
        return GetDarkModeSurfaceBrush();

    case WM_CTLCOLORLISTBOX:
        SetBkColor(hdc, DarkMode::SurfaceColor);
        SetTextColor(hdc, DarkMode::TextColor);
        return GetDarkModeSurfaceBrush();
    }

    return nullptr;
}

//----------------------------------------------------------------------------
//
// ApplyDarkModeToMenu
//
// Uses undocumented uxtheme functions to enable dark mode for menus
//
//----------------------------------------------------------------------------
void ApplyDarkModeToMenu(HMENU hMenu)
{
    if (!hMenu)
    {
        return;
    }

    if (!IsDarkModeEnabled())
    {
        // Light mode - clear any dark background
        MENUINFO mi = { sizeof(mi) };
        mi.fMask = MIM_BACKGROUND | MIM_APPLYTOSUBMENUS;
        mi.hbrBack = nullptr;
        SetMenuInfo(hMenu, &mi);
        return;
    }

    // For popup menus, we need to use MENUINFO to set the background
    MENUINFO mi = { sizeof(mi) };
    mi.fMask = MIM_BACKGROUND | MIM_APPLYTOSUBMENUS;
    mi.hbrBack = GetDarkModeSurfaceBrush();
    SetMenuInfo(hMenu, &mi);
}

//----------------------------------------------------------------------------
//
// RefreshWindowTheme
//
// Forces a window and all its children to redraw with current theme
//
//----------------------------------------------------------------------------
void RefreshWindowTheme(HWND hWnd)
{
    if (!hWnd)
    {
        return;
    }

    // Reapply theme to this window
    ApplyDarkModeToDialog(hWnd);

    // Force redraw
    RedrawWindow(hWnd, nullptr, nullptr, RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_FRAME);
}

//----------------------------------------------------------------------------
//
// CleanupDarkModeResources
//
//----------------------------------------------------------------------------
void CleanupDarkModeResources()
{
    if (g_darkBackgroundBrush)
    {
        DeleteObject(g_darkBackgroundBrush);
        g_darkBackgroundBrush = nullptr;
    }
    if (g_darkControlBrush)
    {
        DeleteObject(g_darkControlBrush);
        g_darkControlBrush = nullptr;
    }
    if (g_darkSurfaceBrush)
    {
        DeleteObject(g_darkSurfaceBrush);
        g_darkSurfaceBrush = nullptr;
    }
}

//----------------------------------------------------------------------------
//
// InitializeDarkMode
//
// Public wrapper to initialize dark mode support early in app startup
//
//----------------------------------------------------------------------------
void InitializeDarkMode()
{
    InitializeDarkModeSupport();
}

//----------------------------------------------------------------------------
//
// ForceRectInBounds
//
//----------------------------------------------------------------------------
RECT ForceRectInBounds( RECT rect, const RECT& bounds )
{
    if( rect.left < bounds.left )
    {
        rect.right += bounds.left - rect.left;
        rect.left = bounds.left;
    }
    if( rect.top < bounds.top )
    {
        rect.bottom += bounds.top - rect.top;
        rect.top = bounds.top;
    }
    if( rect.right > bounds.right )
    {
        rect.left -= rect.right - bounds.right;
        rect.right = bounds.right;
    }
    if( rect.bottom > bounds.bottom )
    {
        rect.top -= rect.bottom - bounds.bottom;
        rect.bottom = bounds.bottom;
    }
    return rect;
}

//----------------------------------------------------------------------------
//
// GetDpiForWindow
//
//----------------------------------------------------------------------------
UINT GetDpiForWindowHelper( HWND window )
{
    auto function = reinterpret_cast<UINT (WINAPI *)(HWND)>(GetProcAddress( GetModuleHandleW( L"user32.dll" ), "GetDpiForWindow" ));
    if( function )
    {
        return function( window );
    }

    wil::unique_hdc hdc{GetDC( nullptr )};
    return static_cast<UINT>(GetDeviceCaps( hdc.get(), LOGPIXELSX ));
}

//----------------------------------------------------------------------------
//
// GetMonitorRectFromCursor
//
//----------------------------------------------------------------------------
RECT GetMonitorRectFromCursor()
{
    POINT point;
    GetCursorPos( &point );
    MONITORINFO monitorInfo{};
    monitorInfo.cbSize = sizeof( monitorInfo );
    GetMonitorInfoW( MonitorFromPoint( point, MONITOR_DEFAULTTONEAREST ), &monitorInfo );
    return monitorInfo.rcMonitor;
}

//----------------------------------------------------------------------------
//
// RectFromPointsMinSize
//
//----------------------------------------------------------------------------
#ifdef _MSC_VER
    // avoid making RectFromPointsMinSize constexpr since that leads to link errors
    #pragma warning(push)
    #pragma warning(disable: 26497)
#endif

RECT RectFromPointsMinSize( POINT a, POINT b, LONG minSize )
{
    RECT rect;
    if( a.x <= b.x )
    {
        rect.left = a.x;
        rect.right = b.x + 1;
        if( (rect.right - rect.left) < minSize )
        {
            rect.right = rect.left + minSize;
        }
    }
    else
    {
        rect.left = b.x;
        rect.right = a.x + 1;
        if( (rect.right - rect.left) < minSize )
        {
            rect.left = rect.right - minSize;
        }
    }
    if( a.y <= b.y )
    {
        rect.top = a.y;
        rect.bottom = b.y + 1;
        if( (rect.bottom - rect.top) < minSize )
        {
            rect.bottom = rect.top + minSize;
        }
    }
    else
    {
        rect.top = b.y;
        rect.bottom = a.y + 1;
        if( (rect.bottom - rect.top) < minSize )
        {
            rect.top = rect.bottom - minSize;
        }
    }
    return rect;
}
#ifdef _MSC_VER
    #pragma warning(pop)
#endif
//----------------------------------------------------------------------------
//
// ScaleForDpi
//
//----------------------------------------------------------------------------
int ScaleForDpi( int value, UINT dpi )
{
    return MulDiv( value, static_cast<int>(dpi), USER_DEFAULT_SCREEN_DPI );
}

//----------------------------------------------------------------------------
//
// ScalePointInRects
//
//----------------------------------------------------------------------------
POINT ScalePointInRects( POINT point, const RECT& source, const RECT& target )
{
    const SIZE sourceSize{ source.right - source.left, source.bottom - source.top };
    const POINT sourceCenter{ source.left + sourceSize.cx / 2, source.top + sourceSize.cy / 2 };
    const SIZE targetSize{ target.right - target.left, target.bottom - target.top };
    const POINT targetCenter{ target.left + targetSize.cx / 2, target.top + targetSize.cy / 2 };

    return { targetCenter.x + MulDiv( point.x - sourceCenter.x, targetSize.cx, sourceSize.cx ),
             targetCenter.y + MulDiv( point.y - sourceCenter.y, targetSize.cy, sourceSize.cy ) };
}

//----------------------------------------------------------------------------
//
// ScaleDialogForDpi
//
// Scales a dialog and all its child controls for the specified DPI.
// oldDpi defaults to DPI_BASELINE (96) for initial scaling.
//
//----------------------------------------------------------------------------
void ScaleDialogForDpi( HWND hDlg, UINT newDpi, UINT oldDpi )
{
    if( newDpi == oldDpi || newDpi == 0 || oldDpi == 0 )
    {
        return;
    }

    // Scale the dialog window itself
    RECT dialogRect;
    GetWindowRect( hDlg, &dialogRect );
    int dialogWidth = MulDiv( dialogRect.right - dialogRect.left, newDpi, oldDpi );
    int dialogHeight = MulDiv( dialogRect.bottom - dialogRect.top, newDpi, oldDpi );
    SetWindowPos( hDlg, nullptr, 0, 0, dialogWidth, dialogHeight, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE );

    // Enumerate and scale all child controls
    HWND hChild = GetWindow( hDlg, GW_CHILD );
    while( hChild != nullptr )
    {
        RECT childRect;
        GetWindowRect( hChild, &childRect );
        MapWindowPoints( nullptr, hDlg, reinterpret_cast<LPPOINT>(&childRect), 2 );

        int x = MulDiv( childRect.left, newDpi, oldDpi );
        int y = MulDiv( childRect.top, newDpi, oldDpi );
        int width = MulDiv( childRect.right - childRect.left, newDpi, oldDpi );
        int height = MulDiv( childRect.bottom - childRect.top, newDpi, oldDpi );

        SetWindowPos( hChild, nullptr, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE );

        // Scale the font for the control
        HFONT hFont = reinterpret_cast<HFONT>(SendMessage( hChild, WM_GETFONT, 0, 0 ));
        if( hFont != nullptr )
        {
            LOGFONT lf{};
            if( GetObject( hFont, sizeof(lf), &lf ) )
            {
                lf.lfHeight = MulDiv( lf.lfHeight, newDpi, oldDpi );
                HFONT hNewFont = CreateFontIndirect( &lf );
                if( hNewFont )
                {
                    SendMessage( hChild, WM_SETFONT, reinterpret_cast<WPARAM>(hNewFont), TRUE );
                    // Note: The old font might be shared, so we don't delete it here
                    // The system will clean up fonts when the dialog is destroyed
                }
            }
        }

        hChild = GetWindow( hChild, GW_HWNDNEXT );
    }

    // Also scale the dialog's own font
    HFONT hDialogFont = reinterpret_cast<HFONT>(SendMessage( hDlg, WM_GETFONT, 0, 0 ));
    if( hDialogFont != nullptr )
    {
        LOGFONT lf{};
        if( GetObject( hDialogFont, sizeof(lf), &lf ) )
        {
            lf.lfHeight = MulDiv( lf.lfHeight, newDpi, oldDpi );
            HFONT hNewFont = CreateFontIndirect( &lf );
            if( hNewFont )
            {
                SendMessage( hDlg, WM_SETFONT, reinterpret_cast<WPARAM>(hNewFont), TRUE );
            }
        }
    }
}

//----------------------------------------------------------------------------
//
// HandleDialogDpiChange
//
// Handles WM_DPICHANGED message for dialogs. Call this from the dialog's
// WndProc when WM_DPICHANGED is received.
//
//----------------------------------------------------------------------------
void HandleDialogDpiChange( HWND hDlg, WPARAM wParam, LPARAM lParam, UINT& currentDpi )
{
    UINT newDpi = HIWORD( wParam );
    if( newDpi != currentDpi && newDpi != 0 )
    {
        const RECT* pSuggestedRect = reinterpret_cast<const RECT*>(lParam);
        
        // Scale the dialog controls from the current DPI to the new DPI
        ScaleDialogForDpi( hDlg, newDpi, currentDpi );
        
        // Move and resize the dialog to the suggested rectangle
        SetWindowPos( hDlg, nullptr,
            pSuggestedRect->left,
            pSuggestedRect->top,
            pSuggestedRect->right - pSuggestedRect->left,
            pSuggestedRect->bottom - pSuggestedRect->top,
            SWP_NOZORDER | SWP_NOACTIVATE );
        
        currentDpi = newDpi;
    }
}
