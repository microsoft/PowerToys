#include "pch.h"
#include "WindowCornersUtil.h"

#include <common/utils/winapi_error.h>

#include <dwmapi.h>

// Placeholder enums since dwmapi.h doesn't have these until SDK 22000.
// TODO: Remove once SDK targets 22000 or above.
enum DWMWINDOWATTRIBUTE_CUSTOM
{
    DWMWA_WINDOW_CORNER_PREFERENCE = 33
};

enum DWM_WINDOW_CORNER_PREFERENCE
{
    DWMWCP_DEFAULT = 0,
    DWMWCP_DONOTROUND = 1,
    DWMWCP_ROUND = 2,
    DWMWCP_ROUNDSMALL = 3
};

int WindowCornerUtils::CornerPreference(HWND window) noexcept
{
    int cornerPreference = -1;
    auto res = DwmGetWindowAttribute(window, DWMWA_WINDOW_CORNER_PREFERENCE, &cornerPreference, sizeof(cornerPreference));
    if (res != S_OK)
    {
        // no need to spam with error log if arg is invalid (on Windows 10)
        if (res != E_INVALIDARG)
        {
            Logger::error(L"Get corner preference error: {}", get_last_error_or_default(res)); 
        }
    }

    return cornerPreference;
}

int WindowCornerUtils::CornersRadius(HWND window) noexcept
{
    int cornerPreference = CornerPreference(window);

    switch (cornerPreference)
    {
    case DWM_WINDOW_CORNER_PREFERENCE::DWMWCP_ROUND:
        return 8;
    case DWM_WINDOW_CORNER_PREFERENCE::DWMWCP_ROUNDSMALL:
        return 4;
    case DWM_WINDOW_CORNER_PREFERENCE::DWMWCP_DEFAULT:
        return 8;
    default:
        return 0;
    }
}
