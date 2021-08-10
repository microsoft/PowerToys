#include "pch.h"
#include "util.h"
#include "Settings.h"

#include <common/display/dpi_aware.h>
#include <common/utils/process_path.h>
#include <common/utils/window.h>

#include <array>
#include <sstream>
#include <complex>
#include <wil/Resource.h>

#include <fancyzones/FancyZonesLib/FancyZonesDataTypes.h>
#include <fancyzones/FancyZonesLib/FancyZonesWindowProperties.h>

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t PowerToysAppPowerLauncher[] = L"POWERLAUNCHER.EXE";
    const wchar_t PowerToysAppFZEditor[] = L"FANCYZONESEDITOR.EXE";
    const wchar_t SplashClassName[] = L"MsoSplash";
}

bool find_app_name_in_path(const std::wstring& where, const std::vector<std::wstring>& what)
{
    for (const auto& row : what)
    {
        const auto pos = where.rfind(row);
        const auto last_slash = where.rfind('\\');
        //Check that row occurs in where, and its last occurrence contains in itself the first character after the last backslash.
        if (pos != std::wstring::npos && pos <= last_slash + 1 && pos + row.length() > last_slash)
        {
            return true;
        }
    }
    return false;
}
namespace
{
    bool IsZonableByProcessPath(const std::wstring& processPath, const std::vector<std::wstring>& excludedApps)
    {
        // Filter out user specified apps
        CharUpperBuffW(const_cast<std::wstring&>(processPath).data(), (DWORD)processPath.length());
        if (find_app_name_in_path(processPath, excludedApps))
        {
            return false;
        }
        if (find_app_name_in_path(processPath, { NonLocalizable::PowerToysAppPowerLauncher }))
        {
            return false;
        }
        if (find_app_name_in_path(processPath, { NonLocalizable::PowerToysAppFZEditor }))
        {
            return false;
        }
        return true;
    }
}

namespace FancyZonesUtils
{
    std::wstring TrimDeviceId(const std::wstring& deviceId)
    {
        // We're interested in the unique part between the first and last #'s
        // Example input: \\?\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
        // Example output: DELA026#5&10a58c63&0&UID16777488
        static const std::wstring defaultDeviceId = L"FallbackDevice";
        if (deviceId.empty())
        {
            return defaultDeviceId;
        }

        size_t start = deviceId.find(L'#');
        size_t end = deviceId.rfind(L'#');
        if (start != std::wstring::npos && end != std::wstring::npos && start != end)
        {
            size_t size = end - (start + 1);
            return deviceId.substr(start + 1, size);
        }
        else
        {
            return defaultDeviceId;
        }
    }

    std::optional<FancyZonesDataTypes::DeviceIdData> ParseDeviceId(const std::wstring& str)
    {
        FancyZonesDataTypes::DeviceIdData data;

        std::wstring temp;
        std::wstringstream wss(str);

        /*
        Important fix for device info that contains a '_' in the name:
        1. first search for '#'
        2. Then split the remaining string by '_'
        */

        // Step 1: parse the name until the #, then to the '_'
        if (str.find(L'#') != std::string::npos)
        {
            std::getline(wss, temp, L'#');

            data.deviceName = temp;

            if (!std::getline(wss, temp, L'_'))
            {
                return std::nullopt;
            }

            data.deviceName += L"#" + temp;
        }
        else if (std::getline(wss, temp, L'_') && !temp.empty())
        {
            data.deviceName = temp;
        }
        else
        {
            return std::nullopt;
        }

        // Step 2: parse the rest of the id
        std::vector<std::wstring> parts;
        while (std::getline(wss, temp, L'_'))
        {
            parts.push_back(temp);
        }

        if (parts.size() != 3 && parts.size() != 4)
        {
            return std::nullopt;
        }

        /*
        Refer to ZoneWindowUtils::GenerateUniqueId parts contain:
        1. monitor id [string]
        2. width of device [int]
        3. height of device [int]
        4. virtual desktop id (GUID) [string]
        */
        try
        {
            for (const auto& c : parts[0])
            {
                std::stoi(std::wstring(&c));
            }

            for (const auto& c : parts[1])
            {
                std::stoi(std::wstring(&c));
            }

            data.width = std::stoi(parts[0]);
            data.height = std::stoi(parts[1]);
        }
        catch (const std::exception&)
        {
            return std::nullopt;
        }

        if (!SUCCEEDED(CLSIDFromString(parts[2].c_str(), &data.virtualDesktopId)))
        {
            return std::nullopt;
        }

        if (parts.size() == 4)
        {
            data.monitorId = parts[3]; //could be empty
        }

        return data;
    }

    typedef BOOL(WINAPI* GetDpiForMonitorInternalFunc)(HMONITOR, UINT, UINT*, UINT*);

    std::wstring GetDisplayDeviceId(const std::wstring& device, std::unordered_map<std::wstring, DWORD>& displayDeviceIdxMap)
    {
        DISPLAY_DEVICE displayDevice{ .cb = sizeof(displayDevice) };
        std::wstring deviceId;
        while (EnumDisplayDevicesW(device.c_str(), displayDeviceIdxMap[device], &displayDevice, EDD_GET_DEVICE_INTERFACE_NAME))
        {
            ++displayDeviceIdxMap[device];

            // Only take active monitors (presented as being "on" by the respective GDI view) and monitors that don't
            // represent a pseudo device used to mirror application drawing.
            if (WI_IsFlagSet(displayDevice.StateFlags, DISPLAY_DEVICE_ACTIVE) &&
                WI_IsFlagClear(displayDevice.StateFlags, DISPLAY_DEVICE_MIRRORING_DRIVER))
            {
                deviceId = displayDevice.DeviceID;
                break;
            }
        }

        if (deviceId.empty())
        {
            deviceId = GetSystemMetrics(SM_REMOTESESSION) ?
                           L"\\\\?\\DISPLAY#REMOTEDISPLAY#" :
                           L"\\\\?\\DISPLAY#LOCALDISPLAY#";
        }

        return deviceId;
    }

    UINT GetDpiForMonitor(HMONITOR monitor) noexcept
    {
        UINT dpi{};
        if (wil::unique_hmodule user32{ LoadLibrary(L"user32.dll") })
        {
            if (auto func = reinterpret_cast<GetDpiForMonitorInternalFunc>(GetProcAddress(user32.get(), "GetDpiForMonitorInternal")))
            {
                func(monitor, 0, &dpi, &dpi);
            }
        }

        if (dpi == 0)
        {
            if (wil::unique_hdc hdc{ GetDC(nullptr) })
            {
                dpi = GetDeviceCaps(hdc.get(), LOGPIXELSX);
            }
        }

        return (dpi == 0) ? DPIAware::DEFAULT_DPI : dpi;
    }

    void OrderMonitors(std::vector<std::pair<HMONITOR, RECT>>& monitorInfo)
    {
        const size_t nMonitors = monitorInfo.size();
        // blocking[i][j] - whether monitor i blocks monitor j in the ordering, i.e. monitor i should go before monitor j
        std::vector<std::vector<bool>> blocking(nMonitors, std::vector<bool>(nMonitors, false));

        // blockingCount[j] - the number of monitors which block monitor j
        std::vector<size_t> blockingCount(nMonitors, 0);

        for (size_t i = 0; i < nMonitors; i++)
        {
            RECT rectI = monitorInfo[i].second;
            for (size_t j = 0; j < nMonitors; j++)
            {
                RECT rectJ = monitorInfo[j].second;
                blocking[i][j] = rectI.top < rectJ.bottom && rectI.left < rectJ.right && i != j;
                if (blocking[i][j])
                {
                    blockingCount[j]++;
                }
            }
        }

        // used[i] - whether the sorting algorithm has used monitor i so far
        std::vector<bool> used(nMonitors, false);

        // the sorted sequence of monitors
        std::vector<std::pair<HMONITOR, RECT>> sortedMonitorInfo;

        for (size_t iteration = 0; iteration < nMonitors; iteration++)
        {
            // Indices of candidates to become the next monitor in the sequence
            std::vector<size_t> candidates;

            // First, find indices of all unblocked monitors
            for (size_t i = 0; i < nMonitors; i++)
            {
                if (blockingCount[i] == 0 && !used[i])
                {
                    candidates.push_back(i);
                }
            }

            // In the unlikely event that there are no unblocked monitors, declare all unused monitors as candidates.
            if (candidates.empty())
            {
                for (size_t i = 0; i < nMonitors; i++)
                {
                    if (!used[i])
                    {
                        candidates.push_back(i);
                    }
                }
            }

            // Pick the lexicographically smallest monitor as the next one
            size_t smallest = candidates[0];
            for (size_t j = 1; j < candidates.size(); j++)
            {
                size_t current = candidates[j];

                // Compare (top, left) lexicographically
                if (std::tie(monitorInfo[current].second.top, monitorInfo[current].second.left) <
                    std::tie(monitorInfo[smallest].second.top, monitorInfo[smallest].second.left))
                {
                    smallest = current;
                }
            }

            used[smallest] = true;
            sortedMonitorInfo.push_back(monitorInfo[smallest]);
            for (size_t i = 0; i < nMonitors; i++)
            {
                if (blocking[smallest][i])
                {
                    blockingCount[i]--;
                }
            }
        }

        monitorInfo = std::move(sortedMonitorInfo);
    }

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

    void SizeWindowToRect(HWND window, RECT rect) noexcept
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

        ::SetWindowPlacement(window, &placement);
        // Do it again, allowing Windows to resize the window and set correct scaling
        // This fixes Issue #365
        ::SetWindowPlacement(window, &placement);
    }

    bool HasNoVisibleOwner(HWND window) noexcept
    {
        auto owner = GetWindow(window, GW_OWNER);
        if (owner == nullptr)
        {
            return true; // There is no owner at all
        }
        if (!IsWindowVisible(owner))
        {
            return true; // Owner is invisible
        }
        RECT rect;
        if (!GetWindowRect(owner, &rect))
        {
            return false; // Could not get the rect, return true (and filter out the window) just in case
        }
        // It is enough that the window is zero-sized in one dimension only.
        return rect.top == rect.bottom || rect.left == rect.right;
    }

    bool IsStandardWindow(HWND window)
    {
        if (GetAncestor(window, GA_ROOT) != window || !IsWindowVisible(window))
        {
            return false;
        }
        auto style = GetWindowLong(window, GWL_STYLE);
        auto exStyle = GetWindowLong(window, GWL_EXSTYLE);
        // WS_POPUP need to have a border or minimize/maximize buttons,
        // otherwise the window is "not interesting"
        if ((style & WS_POPUP) == WS_POPUP &&
            (style & WS_THICKFRAME) == 0 &&
            (style & WS_MINIMIZEBOX) == 0 &&
            (style & WS_MAXIMIZEBOX) == 0)
        {
            return false;
        }
        if ((style & WS_CHILD) == WS_CHILD ||
            (style & WS_DISABLED) == WS_DISABLED ||
            (exStyle & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW ||
            (exStyle & WS_EX_NOACTIVATE) == WS_EX_NOACTIVATE)
        {
            return false;
        }
        std::array<char, 256> class_name;
        GetClassNameA(window, class_name.data(), static_cast<int>(class_name.size()));
        if (is_system_window(window, class_name.data()))
        {
            return false;
        }
        auto process_path = get_process_path(window);
        // Check for Cortana:
        if (strcmp(class_name.data(), "Windows.UI.Core.CoreWindow") == 0 &&
            process_path.ends_with(L"SearchUI.exe"))
        {
            return false;
        }

        return true;
    }

    bool IsCandidateForLastKnownZone(HWND window, const std::vector<std::wstring>& excludedApps) noexcept
    {
        auto zonable = IsStandardWindow(window) && HasNoVisibleOwner(window);
        if (!zonable)
        {
            return false;
        }

        return IsZonableByProcessPath(get_process_path(window), excludedApps);
    }

    bool IsCandidateForZoning(HWND window, const std::vector<std::wstring>& excludedApps) noexcept
    {
        if (!IsStandardWindow(window))
        {
            return false;
        }

        return IsZonableByProcessPath(get_process_path(window), excludedApps);
    }

    bool IsWindowMaximized(HWND window) noexcept
    {
        WINDOWPLACEMENT placement{};
        if (GetWindowPlacement(window, &placement) &&
            placement.showCmd == SW_SHOWMAXIMIZED)
        {
            return true;
        }
        return false;
    }

    void SaveWindowSizeAndOrigin(HWND window) noexcept
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
            int width = rect.right - rect.left;
            int height = rect.bottom - rect.top;
            int originX = rect.left;
            int originY = rect.top;

            DPIAware::InverseConvert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), width, height);
            DPIAware::InverseConvert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), originX, originY);

            std::array<int, 2> windowSizeData = { width, height };
            std::array<int, 2> windowOriginData = { originX, originY };
            HANDLE rawData;
            memcpy(&rawData, windowSizeData.data(), sizeof rawData);
            SetPropW(window, ZonedWindowProperties::PropertyRestoreSizeID, rawData);
            memcpy(&rawData, windowOriginData.data(), sizeof rawData);
            SetPropW(window, ZonedWindowProperties::PropertyRestoreOriginID, rawData);
        }
    }

    void RestoreWindowSize(HWND window) noexcept
    {
        auto windowSizeData = GetPropW(window, ZonedWindowProperties::PropertyRestoreSizeID);
        if (windowSizeData)
        {
            std::array<int, 2> windowSize;
            memcpy(windowSize.data(), &windowSizeData, sizeof windowSize);

            // {width, height}
            DPIAware::Convert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), windowSize[0], windowSize[1]);

            RECT rect;
            if (GetWindowRect(window, &rect))
            {
                rect.right = rect.left + windowSize[0];
                rect.bottom = rect.top + windowSize[1];
                SizeWindowToRect(window, rect);
            }

            ::RemoveProp(window, ZonedWindowProperties::PropertyRestoreSizeID);
        }
    }

    void RestoreWindowOrigin(HWND window) noexcept
    {
        auto windowOriginData = GetPropW(window, ZonedWindowProperties::PropertyRestoreOriginID);
        if (windowOriginData)
        {
            std::array<int, 2> windowOrigin;
            memcpy(windowOrigin.data(), &windowOriginData, sizeof windowOrigin);

            // {width, height}
            DPIAware::Convert(MonitorFromWindow(window, MONITOR_DEFAULTTONULL), windowOrigin[0], windowOrigin[1]);

            RECT rect;
            if (GetWindowRect(window, &rect))
            {
                int xOffset = windowOrigin[0] - rect.left;
                int yOffset = windowOrigin[1] - rect.top;

                rect.left += xOffset;
                rect.right += xOffset;
                rect.top += yOffset;
                rect.bottom += yOffset;
                SizeWindowToRect(window, rect);
            }

            ::RemoveProp(window, ZonedWindowProperties::PropertyRestoreOriginID);
        }
    }

    bool IsValidGuid(const std::wstring& str)
    {
        GUID id;
        return SUCCEEDED(CLSIDFromString(str.c_str(), &id));
    }

    std::optional<std::wstring> GuidToString(const GUID& guid) noexcept
    {
        wil::unique_cotaskmem_string guidString;
        if (SUCCEEDED(StringFromCLSID(guid, &guidString)))
        {
            return guidString.get();
        }

        return std::nullopt;
    }

    bool IsValidDeviceId(const std::wstring& str)
    {
        std::wstring monitorName;
        std::wstring temp;
        std::vector<std::wstring> parts;
        std::wstringstream wss(str);

        /*
        Important fix for device info that contains a '_' in the name:
        1. first search for '#'
        2. Then split the remaining string by '_'
        */

        // Step 1: parse the name until the #, then to the '_'
        if (str.find(L'#') != std::string::npos)
        {
            std::getline(wss, temp, L'#');

            monitorName = temp;

            if (!std::getline(wss, temp, L'_'))
            {
                return false;
            }

            monitorName += L"#" + temp;
            parts.push_back(monitorName);
        }

        // Step 2: parse the rest of the id
        while (std::getline(wss, temp, L'_'))
        {
            parts.push_back(temp);
        }

        if (parts.size() != 4)
        {
            return false;
        }

        /*
        Refer to ZoneWindowUtils::GenerateUniqueId parts contain:
        1. monitor id [string]
        2. width of device [int]
        3. height of device [int]
        4. virtual desktop id (GUID) [string]
    */
        try
        {
            //check if resolution contain only digits
            for (const auto& c : parts[1])
            {
                std::stoi(std::wstring(&c));
            }
            for (const auto& c : parts[2])
            {
                std::stoi(std::wstring(&c));
            }
        }
        catch (const std::exception&)
        {
            return false;
        }

        if (!IsValidGuid(parts[3]) || parts[0].empty())
        {
            return false;
        }

        return true;
    }

    std::wstring GenerateUniqueId(HMONITOR monitor, const std::wstring& deviceId, const std::wstring& virtualDesktopId)
    {
        MONITORINFOEXW mi;
        mi.cbSize = sizeof(mi);
        if (!virtualDesktopId.empty() && GetMonitorInfo(monitor, &mi))
        {
            Rect const monitorRect(mi.rcMonitor);
            // Unique identifier format: <parsed-device-id>_<width>_<height>_<virtual-desktop-id>
            return TrimDeviceId(deviceId) +
                   L'_' +
                   std::to_wstring(monitorRect.width()) +
                   L'_' +
                   std::to_wstring(monitorRect.height()) +
                   L'_' +
                   virtualDesktopId;
        }
        return {};
    }

    std::wstring GenerateUniqueIdAllMonitorsArea(const std::wstring& virtualDesktopId)
    {
        std::wstring result{ ZonedWindowProperties::MultiMonitorDeviceID };

        RECT combinedResolution = GetAllMonitorsCombinedRect<&MONITORINFO::rcMonitor>();

        result += L'_';
        result += std::to_wstring(combinedResolution.right - combinedResolution.left);
        result += L'_';
        result += std::to_wstring(combinedResolution.bottom - combinedResolution.top);
        result += L'_';
        result += virtualDesktopId;

        return result;
    }

    size_t ChooseNextZoneByPosition(DWORD vkCode, RECT windowRect, const std::vector<RECT>& zoneRects) noexcept
    {
        using complex = std::complex<double>;
        const size_t invalidResult = zoneRects.size();
        const double inf = 1e100;
        const double eccentricity = 2.0;

        auto rectCenter = [](RECT rect) {
            return complex{
                0.5 * rect.left + 0.5 * rect.right,
                0.5 * rect.top + 0.5 * rect.bottom
            };
        };

        auto distance = [&](complex arrowDirection, complex zoneDirection) {
            double result = inf;

            try
            {
                double scalarProduct = (arrowDirection * conj(zoneDirection)).real();
                if (scalarProduct <= 0.0)
                {
                    return inf;
                }

                // no need to divide by abs(arrowDirection) because it's = 1
                double cosAngle = scalarProduct / abs(zoneDirection);
                double tanAngle = abs(tan(acos(cosAngle)));

                if (tanAngle > 10)
                {
                    // The angle is too wide
                    return inf;
                }

                // find the intersection with the ellipse with given eccentricity and major axis along arrowDirection
                double intersectY = 2 * eccentricity / (1.0 + eccentricity * eccentricity * tanAngle * tanAngle);
                double distanceEstimate = scalarProduct / intersectY;

                if (std::isfinite(distanceEstimate))
                {
                    result = distanceEstimate;
                }
            }
            catch (...)
            {
            }

            return result;
        };
        std::vector<std::pair<size_t, complex>> candidateCenters;
        for (size_t i = 0; i < zoneRects.size(); i++)
        {
            auto center = rectCenter(zoneRects[i]);

            // Offset the zone slightly, to differentiate in case there are overlapping zones
            center += 0.001 * (i + 1);

            candidateCenters.emplace_back(i, center);
        }

        complex directionVector, windowCenter = rectCenter(windowRect);

        switch (vkCode)
        {
        case VK_UP:
            directionVector = { 0.0, -1.0 };
            break;
        case VK_DOWN:
            directionVector = { 0.0, 1.0 };
            break;
        case VK_LEFT:
            directionVector = { -1.0, 0.0 };
            break;
        case VK_RIGHT:
            directionVector = { 1.0, 0.0 };
            break;
        default:
            return invalidResult;
        }

        size_t closestIdx = invalidResult;
        double smallestDistance = inf;

        for (auto [zoneIdx, zoneCenter] : candidateCenters)
        {
            double dist = distance(directionVector, zoneCenter - windowCenter);
            if (dist < smallestDistance)
            {
                smallestDistance = dist;
                closestIdx = zoneIdx;
            }
        }

        return closestIdx;
    }

    RECT PrepareRectForCycling(RECT windowRect, RECT zoneWindowRect, DWORD vkCode) noexcept
    {
        LONG deltaX = 0, deltaY = 0;
        switch (vkCode)
        {
        case VK_UP:
            deltaY = zoneWindowRect.bottom - zoneWindowRect.top;
            break;
        case VK_DOWN:
            deltaY = zoneWindowRect.top - zoneWindowRect.bottom;
            break;
        case VK_LEFT:
            deltaX = zoneWindowRect.right - zoneWindowRect.left;
            break;
        case VK_RIGHT:
            deltaX = zoneWindowRect.left - zoneWindowRect.right;
        }

        windowRect.left += deltaX;
        windowRect.right += deltaX;
        windowRect.top += deltaY;
        windowRect.bottom += deltaY;

        return windowRect;
    }

    bool IsProcessOfWindowElevated(HWND window)
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

    bool IsSplashScreen(HWND window)
    {
        wchar_t className[MAX_PATH];
        if (GetClassName(window, className, MAX_PATH) == 0)
        {
            return false;
        }

        return wcscmp(NonLocalizable::SplashClassName, className) == 0;
    }

}
