#include "pch.h"
#include "WindowUtils.h"

#include <common/display/dpi_aware.h>
#include <common/logger/logger.h>
#include <common/utils/process_path.h>
#include <common/utils/winapi_error.h>
#include <common/utils/window.h>
#include <common/utils/excluded_apps.h>

#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/Settings.h>

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t PowerToysAppFZEditor[] = L"POWERTOYS.FANCYZONESEDITOR.EXE";
    const wchar_t SplashClassName[] = L"MsoSplash";
    const wchar_t CoreWindow[] = L"Windows.UI.Core.CoreWindow";
    const wchar_t SearchUI[] = L"SearchUI.exe";
    const wchar_t SystemAppsFolder[] = L"SYSTEMAPPS";
}

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

namespace
{   
    BOOL CALLBACK saveDisplayToVector(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data)
    {
        reinterpret_cast<std::vector<HMONITOR>*>(data)->emplace_back(monitor);
        return true;
    }

    bool allMonitorsHaveSameDpiScaling()
    {
        std::vector<HMONITOR> monitors;
        EnumDisplayMonitors(NULL, NULL, saveDisplayToVector, reinterpret_cast<LPARAM>(&monitors));

        if (monitors.size() < 2)
        {
            return true;
        }

        UINT firstMonitorDpiX;
        UINT firstMonitorDpiY;

        if (S_OK != GetDpiForMonitor(monitors[0], MDT_EFFECTIVE_DPI, &firstMonitorDpiX, &firstMonitorDpiY))
        {
            return false;
        }

        for (int i = 1; i < monitors.size(); i++)
        {
            UINT iteratedMonitorDpiX;
            UINT iteratedMonitorDpiY;

            if (S_OK != GetDpiForMonitor(monitors[i], MDT_EFFECTIVE_DPI, &iteratedMonitorDpiX, &iteratedMonitorDpiY) ||
                iteratedMonitorDpiX != firstMonitorDpiX)
            {
                return false;
            }
        }

        return true;
    }

    void ScreenToWorkAreaCoords(HWND window, RECT& rect)
    {
        // First, find the correct monitor. The monitor cannot be found using the given rect itself, we must first
        // translate it to relative workspace coordinates.
        HMONITOR monitor = MonitorFromRect(&rect, MONITOR_DEFAULTTOPRIMARY);
        MONITORINFOEXW monitorInfo{ sizeof(MONITORINFOEXW) };
        GetMonitorInfoW(monitor, &monitorInfo);

        auto xOffset = monitorInfo.rcWork.left - monitorInfo.rcMonitor.left;
        auto yOffset = monitorInfo.rcWork.top - monitorInfo.rcMonitor.top;

        auto referenceRect = rect;

        referenceRect.left -= xOffset;
        referenceRect.right -= xOffset;
        referenceRect.top -= yOffset;
        referenceRect.bottom -= yOffset;

        // Now, this rect should be used to determine the monitor and thus taskbar size. This fixes
        // scenarios where the zone lies approximately between two monitors, and the taskbar is on the left.
        monitor = MonitorFromRect(&referenceRect, MONITOR_DEFAULTTOPRIMARY);
        GetMonitorInfoW(monitor, &monitorInfo);

        xOffset = monitorInfo.rcWork.left - monitorInfo.rcMonitor.left;
        yOffset = monitorInfo.rcWork.top - monitorInfo.rcMonitor.top;

        rect.left -= xOffset;
        rect.right -= xOffset;
        rect.top -= yOffset;
        rect.bottom -= yOffset;

        const auto level = DPIAware::GetAwarenessLevel(GetWindowDpiAwarenessContext(window));
        const bool accountForUnawareness = level < DPIAware::PER_MONITOR_AWARE;

        if (accountForUnawareness && !allMonitorsHaveSameDpiScaling())
        {
            rect.left = max(monitorInfo.rcMonitor.left, rect.left);
            rect.right = min(monitorInfo.rcMonitor.right - xOffset, rect.right);
            rect.top = max(monitorInfo.rcMonitor.top, rect.top);
            rect.bottom = min(monitorInfo.rcMonitor.bottom - yOffset, rect.bottom);
        }
    }
}


bool FancyZonesWindowUtils::IsSplashScreen(HWND window)
{
    wchar_t className[MAX_PATH];
    if (GetClassName(window, className, MAX_PATH) == 0)
    {
        return false;
    }

    return wcscmp(NonLocalizable::SplashClassName, className) == 0;
}

bool FancyZonesWindowUtils::IsWindowMaximized(HWND window) noexcept
{
    WINDOWPLACEMENT placement{};
    if (GetWindowPlacement(window, &placement) &&
        placement.showCmd == SW_SHOWMAXIMIZED)
    {
        return true;
    }
    return false;
}

bool FancyZonesWindowUtils::HasVisibleOwner(HWND window) noexcept
{
    auto owner = GetWindow(window, GW_OWNER);
    if (owner == nullptr)
    {
        return false; // There is no owner at all
    }
    if (!IsWindowVisible(owner))
    {
        return false; // Owner is invisible
    }
    RECT rect;
    if (!GetWindowRect(owner, &rect))
    {
        return true; // Could not get the rect, return true (and filter out the window) just in case
    }
    // It is enough that the window is zero-sized in one dimension only.
    return rect.top != rect.bottom && rect.left != rect.right;
}

bool FancyZonesWindowUtils::IsStandardWindow(HWND window)
{
    if (GetAncestor(window, GA_ROOT) != window)
    {
        return false;
    }

    auto style = GetWindowLong(window, GWL_STYLE);
    auto exStyle = GetWindowLong(window, GWL_EXSTYLE);

    bool isToolWindow = (exStyle & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW;
    bool isVisible = (style & WS_VISIBLE) == WS_VISIBLE;
    if (isToolWindow || !isVisible)
    {
        return false;
    }

    std::array<char, 256> class_name;
    GetClassNameA(window, class_name.data(), static_cast<int>(class_name.size()));
    if (is_system_window(window, class_name.data()))
    {
        return false;
    }

    return true;
}

bool FancyZonesWindowUtils::IsPopupWindow(HWND window) noexcept
{
    auto style = GetWindowLong(window, GWL_STYLE);
    return ((style & WS_POPUP) == WS_POPUP);
}

bool FancyZonesWindowUtils::HasThickFrameAndMinimizeMaximizeButtons(HWND window) noexcept
{
    auto style = GetWindowLong(window, GWL_STYLE);
    return ((style & WS_THICKFRAME) == WS_THICKFRAME
        && (style & WS_MINIMIZEBOX) == WS_MINIMIZEBOX
        && (style & WS_MAXIMIZEBOX) == WS_MAXIMIZEBOX);
}

bool FancyZonesWindowUtils::IsCandidateForZoning(HWND window)
{
    bool isStandard = IsStandardWindow(window);
    if (!isStandard)
    {
        return false;
    }
    
    // popup could be the window we don't want to snap: start menu, notification popup, tray window, etc.
    // also, popup could be the windows we want to snap disregarding the "allowSnapPopupWindows" setting, e.g. Telegram
    bool isPopup = IsPopupWindow(window);
    if (isPopup && !HasThickFrameAndMinimizeMaximizeButtons(window) && !FancyZonesSettings::settings().allowSnapPopupWindows)
    {
        return false;
    }
    
    // allow child windows 
    auto hasOwner = HasVisibleOwner(window);
    if (hasOwner && !FancyZonesSettings::settings().allowSnapChildWindows)
    {
        return false;
    }

    std::wstring processPath = get_process_path_waiting_uwp(window);
    CharUpperBuffW(const_cast<std::wstring&>(processPath).data(), (DWORD)processPath.length());
    if (IsExcludedByUser(processPath))
    {
        return false;
    }

    if (IsExcludedByDefault(processPath))
    {
        return false;
    }

    return true;
}

bool FancyZonesWindowUtils::IsProcessOfWindowElevated(HWND window)
{
    DWORD pid = 0;
    GetWindowThreadProcessId(window, &pid);
    if (!pid)
    {
        return false;
    }

    wil::unique_handle hProcess{ OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,
                                             FALSE,
                                             pid) };

    wil::unique_handle token;
    bool elevated = false;

    if (OpenProcessToken(hProcess.get(), TOKEN_QUERY, &token))
    {
        TOKEN_ELEVATION elevation;
        DWORD size;
        if (GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size))
        {
            return elevation.TokenIsElevated != 0;
        }
    }
    return false;
}

bool FancyZonesWindowUtils::IsExcludedByUser(const std::wstring& processPath) noexcept
{
    return (find_app_name_in_path(processPath, FancyZonesSettings::settings().excludedAppsArray));
}

bool FancyZonesWindowUtils::IsExcludedByDefault(const std::wstring& processPath) noexcept
{
    static std::vector<std::wstring> defaultExcludedFolders = { NonLocalizable::SystemAppsFolder };
    if (find_folder_in_path(processPath, defaultExcludedFolders))
    {
        return true;
    }
    
    static std::vector<std::wstring> defaultExcludedApps = { NonLocalizable::PowerToysAppFZEditor, NonLocalizable::CoreWindow, NonLocalizable::SearchUI };
    return (find_app_name_in_path(processPath, defaultExcludedApps));
}

void FancyZonesWindowUtils::SwitchToWindow(HWND window) noexcept
{
    // Check if the window is minimized
    if (IsIconic(window))
    {
        // Show the window since SetForegroundWindow fails on minimized windows
        if (!ShowWindow(window, SW_RESTORE))
        {
            Logger::error(L"ShowWindow failed");
        }
    }

    // This is a hack to bypass the restriction on setting the foreground window
    INPUT inputs[1] = { { .type = INPUT_MOUSE } };
    SendInput(ARRAYSIZE(inputs), inputs, sizeof(INPUT));

    if (!SetForegroundWindow(window))
    {
        Logger::error(L"SetForegroundWindow failed");
    }
}

void FancyZonesWindowUtils::SizeWindowToRect(HWND window, RECT rect) noexcept
{
    WINDOWPLACEMENT placement{};
    ::GetWindowPlacement(window, &placement);

    // Wait if SW_SHOWMINIMIZED would be removed from window (Issue #1685)
    for (int i = 0; i < 5 && (placement.showCmd == SW_SHOWMINIMIZED); ++i)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
        ::GetWindowPlacement(window, &placement);
    }

    // Do not restore minimized windows. We change their placement though so they restore to the correct zone.
    if ((placement.showCmd != SW_SHOWMINIMIZED) &&
        (placement.showCmd != SW_MINIMIZE))
    {
        placement.showCmd = SW_RESTORE;
    }

    // Remove maximized show command to make sure window is moved to the correct zone.
    if (placement.showCmd == SW_SHOWMAXIMIZED)
    {
        placement.showCmd = SW_RESTORE;
        placement.flags &= ~WPF_RESTORETOMAXIMIZED;
    }

    ScreenToWorkAreaCoords(window, rect);

    placement.rcNormalPosition = rect;
    placement.flags |= WPF_ASYNCWINDOWPLACEMENT;

    auto result = ::SetWindowPlacement(window, &placement);
    if (!result)
    {
        Logger::error(L"SetWindowPlacement failed, {}", get_last_error_or_default(GetLastError()));
    }
    
    // Do it again, allowing Windows to resize the window and set correct scaling
    // This fixes Issue #365
    result = ::SetWindowPlacement(window, &placement);
    if (!result)
    {
        Logger::error(L"SetWindowPlacement failed, {}", get_last_error_or_default(GetLastError()));
    }
}

void FancyZonesWindowUtils::SaveWindowSizeAndOrigin(HWND window) noexcept
{
    HANDLE handle = GetPropW(window, ZonedWindowProperties::PropertyRestoreSizeID);
    if (handle)
    {
        // Size already set, skip
        return;
    }

    RECT rect;
    if (GetWindowRect(window, &rect))
    {
        float width = static_cast<float>(rect.right - rect.left);
        float height = static_cast<float>(rect.bottom - rect.top);
        float originX = static_cast<float>(rect.left);
        float originY = static_cast<float>(rect.top);

        DPIAware::InverseConvert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), width, height);
        DPIAware::InverseConvert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), originX, originY);

        std::array<int, 2> windowSizeData = { static_cast<int>(width), static_cast<int>(height) };
        std::array<int, 2> windowOriginData = { static_cast<int>(originX), static_cast<int>(originY) };
        HANDLE rawData;
        memcpy(&rawData, windowSizeData.data(), sizeof rawData);
        SetPropW(window, ZonedWindowProperties::PropertyRestoreSizeID, rawData);
        memcpy(&rawData, windowOriginData.data(), sizeof rawData);
        SetPropW(window, ZonedWindowProperties::PropertyRestoreOriginID, rawData);
    }
}

void FancyZonesWindowUtils::RestoreWindowSize(HWND window) noexcept
{
    auto windowSizeData = GetPropW(window, ZonedWindowProperties::PropertyRestoreSizeID);
    if (windowSizeData)
    {
        std::array<int, 2> windowSize;
        memcpy(windowSize.data(), &windowSizeData, sizeof windowSize);

        float windowWidth = static_cast<float>(windowSize[0]), windowHeight = static_cast<float>(windowSize[1]);

        // {width, height}
        DPIAware::Convert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), windowWidth, windowHeight);

        RECT rect;
        if (GetWindowRect(window, &rect))
        {
            rect.right = rect.left + static_cast<int>(windowWidth);
            rect.bottom = rect.top + static_cast<int>(windowHeight);
            Logger::info("Restore window size");
            SizeWindowToRect(window, rect);
        }

        ::RemoveProp(window, ZonedWindowProperties::PropertyRestoreSizeID);
    }
}

void FancyZonesWindowUtils::RestoreWindowOrigin(HWND window) noexcept
{
    auto windowOriginData = GetPropW(window, ZonedWindowProperties::PropertyRestoreOriginID);
    if (windowOriginData)
    {
        std::array<int, 2> windowOrigin;
        memcpy(windowOrigin.data(), &windowOriginData, sizeof windowOrigin);

        float windowWidth = static_cast<float>(windowOrigin[0]), windowHeight = static_cast<float>(windowOrigin[1]);

        // {width, height}
        DPIAware::Convert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), windowWidth, windowHeight);

        RECT rect;
        if (GetWindowRect(window, &rect))
        {
            int xOffset = windowOrigin[0] - rect.left;
            int yOffset = windowOrigin[1] - rect.top;

            rect.left += xOffset;
            rect.right += xOffset;
            rect.top += yOffset;
            rect.bottom += yOffset;

            Logger::info("Restore window origin");
            SizeWindowToRect(window, rect);
        }

        ::RemoveProp(window, ZonedWindowProperties::PropertyRestoreOriginID);
    }
}

RECT FancyZonesWindowUtils::AdjustRectForSizeWindowToRect(HWND window, RECT rect, HWND windowOfRect) noexcept
{
    RECT newWindowRect = rect;

    RECT windowRect{};
    ::GetWindowRect(window, &windowRect);

    // Take care of borders
    RECT frameRect{};
    if (SUCCEEDED(DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, &frameRect, sizeof(frameRect))))
    {
        LONG leftMargin = frameRect.left - windowRect.left;
        LONG rightMargin = frameRect.right - windowRect.right;
        LONG bottomMargin = frameRect.bottom - windowRect.bottom;
        newWindowRect.left -= leftMargin;
        newWindowRect.right -= rightMargin;
        newWindowRect.bottom -= bottomMargin;
    }

    // Take care of windows that cannot be resized
    if ((::GetWindowLong(window, GWL_STYLE) & WS_SIZEBOX) == 0)
    {
        newWindowRect.right = newWindowRect.left + (windowRect.right - windowRect.left);
        newWindowRect.bottom = newWindowRect.top + (windowRect.bottom - windowRect.top);
    }

    // Convert to screen coordinates
    MapWindowRect(windowOfRect, nullptr, &newWindowRect);

    return newWindowRect;
}

void FancyZonesWindowUtils::DisableRoundCorners(HWND window) noexcept
{
    HANDLE handle = GetPropW(window, ZonedWindowProperties::PropertyCornerPreference);
    if (!handle)
    {
        int cornerPreference = DWMWCP_DEFAULT;
        // save corner preference if it wasn't set already
        DwmGetWindowAttribute(window, DWMWA_WINDOW_CORNER_PREFERENCE, &cornerPreference, sizeof(cornerPreference));

        static_assert(sizeof(int) == 4);
        static_assert(sizeof(HANDLE) == 8);
        static_assert(sizeof(HANDLE) == sizeof(uint64_t));

        // 0 is a valid value, so use a high bit to distinguish between 0 and a GetProp fail
        uint64_t cornerPreference64 = static_cast<uint64_t>(cornerPreference);
        cornerPreference64 = (cornerPreference64 & 0xFFFFFFFF) | 0x100000000;

        HANDLE preferenceHandle = {};
        memcpy(&preferenceHandle, &cornerPreference64, sizeof(HANDLE));

        if (!SetPropW(window, ZonedWindowProperties::PropertyCornerPreference, preferenceHandle))
        {
            Logger::error(L"Failed to save corner preference, {}", get_last_error_or_default(GetLastError()));
        }
    }

    // Set window corner preference on Windows 11 to "Do not round"
    int cornerPreference = DWMWCP_DONOTROUND;
    if (!SUCCEEDED(DwmSetWindowAttribute(window, DWMWA_WINDOW_CORNER_PREFERENCE, &cornerPreference, sizeof(cornerPreference))))
    {
        Logger::error(L"Failed to set DWMWCP_DONOTROUND corner preference");
    }
}

void FancyZonesWindowUtils::ResetRoundCornersPreference(HWND window) noexcept
{
    HANDLE handle = GetPropW(window, ZonedWindowProperties::PropertyCornerPreference);
    if (handle)
    {
        static_assert(sizeof(int) == 4);
        static_assert(sizeof(HANDLE) == 8);
        static_assert(sizeof(HANDLE) == sizeof(uint64_t));

        uint64_t cornerPreference64 = {};
        memcpy(&cornerPreference64, &handle, sizeof(uint64_t));
        cornerPreference64 = cornerPreference64 & 0xFFFFFFFF;

        int cornerPreference = static_cast<int>(cornerPreference64);

        if (!SUCCEEDED(DwmSetWindowAttribute(window, DWMWA_WINDOW_CORNER_PREFERENCE, &cornerPreference, sizeof(cornerPreference))))
        {
            Logger::error(L"Failed to set saved corner preference");
        }

        RemovePropW(window, ZonedWindowProperties::PropertyCornerPreference);
    }
}

void FancyZonesWindowUtils::MakeWindowTransparent(HWND window)
{
    int const pos = -GetSystemMetrics(SM_CXVIRTUALSCREEN) - 8;
    if (wil::unique_hrgn hrgn{ CreateRectRgn(pos, 0, (pos + 1), 1) })
    {
        DWM_BLURBEHIND bh = { DWM_BB_ENABLE | DWM_BB_BLURREGION, TRUE, hrgn.get(), FALSE };
        DwmEnableBlurBehindWindow(window, &bh);
    }
}
