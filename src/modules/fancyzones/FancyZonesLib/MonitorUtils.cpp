#include "pch.h"
#include "MonitorUtils.h"

#include <FancyZonesLib/WindowUtils.h>
#include <FancyZonesLib/util.h>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

namespace MonitorUtils
{
    constexpr int CUSTOM_POSITIONING_LEFT_TOP_PADDING = 16;

    namespace WMI
    {
        
    }
    
    namespace Display
    {
        std::wstring TrimDeviceId(const std::wstring& deviceId) noexcept
        {
            // Example input: \\?\DISPLAY#TLX1388#4&125707d6&0&UID8388688#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
            // Example output: TLX1388#4&125707d6&0
            
            static const std::wstring defaultDeviceId = L"FallbackDevice";

            size_t start = deviceId.find(L'#');
            size_t end = deviceId.rfind(L'&');
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
        
        std::vector<std::wstring> GetDisplays()
        {
            auto allMonitors = FancyZonesUtils::GetAllMonitorInfo<&MONITORINFOEX::rcWork>();
            std::unordered_map<std::wstring, DWORD> displayDeviceIdxMap;
            std::vector<std::wstring> result{};

            for (auto& monitorData : allMonitors)
            {
                auto monitorInfo = monitorData.second;

                DISPLAY_DEVICE displayDevice{ .cb = sizeof(displayDevice) };
                std::wstring deviceId;
                auto enumRes = EnumDisplayDevicesW(monitorInfo.szDevice, displayDeviceIdxMap[monitorInfo.szDevice], &displayDevice, EDD_GET_DEVICE_INTERFACE_NAME);

                if (!enumRes)
                {
                    Logger::error(L"EnumDisplayDevicesW error: {}", get_last_error_or_default(GetLastError()));
                    continue;
                }

                Logger::info(L"Get display device: {}", displayDevice.DeviceID);
                result.emplace_back(std::move(TrimDeviceId(displayDevice.DeviceID)));
            }

            return result;
        }
    }

    namespace
    {
        inline int RectWidth(const RECT& rect)
        {
            return rect.right - rect.left;
        }

        inline int RectHeight(const RECT& rect)
        {
            return rect.bottom - rect.top;
        }

        RECT FitOnScreen(const RECT& windowRect, const RECT& originMonitorRect, const RECT& destMonitorRect)
    {
        // New window position on active monitor. If window fits the screen, this will be final position.
        int left = destMonitorRect.left + (windowRect.left - originMonitorRect.left);
        int top = destMonitorRect.top + (windowRect.top - originMonitorRect.top);
        int W = RectWidth(windowRect);
        int H = RectHeight(windowRect);

        if ((left < destMonitorRect.left) || (left + W > destMonitorRect.right))
        {
            // Set left window border to left border of screen (add padding). Resize window width if needed.
            left = destMonitorRect.left + CUSTOM_POSITIONING_LEFT_TOP_PADDING;
            W = min(W, RectWidth(destMonitorRect) - CUSTOM_POSITIONING_LEFT_TOP_PADDING);
        }
        if ((top < destMonitorRect.top) || (top + H > destMonitorRect.bottom))
        {
            // Set top window border to top border of screen (add padding). Resize window height if needed.
            top = destMonitorRect.top + CUSTOM_POSITIONING_LEFT_TOP_PADDING;
            H = min(H, RectHeight(destMonitorRect) - CUSTOM_POSITIONING_LEFT_TOP_PADDING);
        }

        return { .left = left,
                 .top = top,
                 .right = left + W,
                 .bottom = top + H };
    }
    }
    
    void OpenWindowOnActiveMonitor(HWND window, HMONITOR monitor) noexcept
    {
        // By default Windows opens new window on primary monitor.
        // Try to preserve window width and height, adjust top-left corner if needed.
        HMONITOR origin = MonitorFromWindow(window, MONITOR_DEFAULTTOPRIMARY);
        if (origin == monitor)
        {
            // Certain applications by design open in last known position, regardless of FancyZones.
            // If that position is on currently active monitor, skip custom positioning.
            return;
        }

        WINDOWPLACEMENT placement{};
        if (GetWindowPlacement(window, &placement))
        {
            MONITORINFOEX originMi;
            originMi.cbSize = sizeof(originMi);
            if (GetMonitorInfo(origin, &originMi))
            {
                MONITORINFOEX destMi;
                destMi.cbSize = sizeof(destMi);
                if (GetMonitorInfo(monitor, &destMi))
                {
                    RECT newPosition = FitOnScreen(placement.rcNormalPosition, originMi.rcWork, destMi.rcWork);
                    FancyZonesWindowUtils::SizeWindowToRect(window, newPosition);
                }
            }
        }
    }
   
    std::vector<MonitorData> IdentifyMonitors() noexcept
    {
        std::vector<MonitorData> result{};

        auto monitors = Display::GetDisplays();
        for (const auto& monitor : monitors)
        {
            result.push_back({ .deviceId = monitor });
        }
        
        return result;
    }
}