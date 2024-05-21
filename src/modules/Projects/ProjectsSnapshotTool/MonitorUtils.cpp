#include "pch.h"
#include "MonitorUtils.h"

#include <ShellScalingApi.h>

#include "../projects-common/MonitorEnumerator.h"
#include "OnThreadExecutor.h"

namespace Common
{
    namespace Display
    {
        namespace DPIAware
        {
            constexpr inline int DEFAULT_DPI = 96;

            void Convert(HMONITOR monitor_handle, float& width, float& height)
            {
                if (monitor_handle == NULL)
                {
                    const POINT ptZero = { 0, 0 };
                    monitor_handle = MonitorFromPoint(ptZero, MONITOR_DEFAULTTOPRIMARY);
                }

                UINT dpi_x, dpi_y;
                if (GetDpiForMonitor(monitor_handle, MDT_EFFECTIVE_DPI, &dpi_x, &dpi_y) == S_OK)
                {
                    width = width * dpi_x / DEFAULT_DPI;
                    height = height * dpi_y / DEFAULT_DPI;
                }
            }

            HRESULT GetScreenDPIForMonitor(HMONITOR targetMonitor, UINT& dpi)
            {
                if (targetMonitor != nullptr)
                {
                    UINT dummy = 0;
                    return GetDpiForMonitor(targetMonitor, MDT_EFFECTIVE_DPI, &dpi, &dummy);
                }
                else
                {
                    dpi = DPIAware::DEFAULT_DPI;
                    return E_FAIL;
                }
            }
        }
    }
}

namespace MonitorUtils
{
	namespace Display
	{
        constexpr inline bool not_digit(wchar_t ch)
        {
            return '0' <= ch && ch <= '9';
        }

        std::wstring remove_non_digits(const std::wstring& input)
        {
            std::wstring result;
            std::copy_if(input.begin(), input.end(), std::back_inserter(result), not_digit);
            return result;
        }

		std::pair<bool, std::vector<Project::Monitor>> GetDisplays()
		{
            bool success = true;
            std::vector<Project::Monitor> result{};

            auto allMonitors = MonitorEnumerator::Enumerate();
            for (auto& monitorData : allMonitors)
            {
                MONITORINFOEX monitorInfo = monitorData.second;
                MONITORINFOEX dpiUnawareMonitorInfo{};

                OnThreadExecutor dpiUnawareThread;
                dpiUnawareThread.submit(OnThreadExecutor::task_t{ [&] {
                      SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_UNAWARE);
                      SetThreadDpiHostingBehavior(DPI_HOSTING_BEHAVIOR_MIXED);

                      dpiUnawareMonitorInfo.cbSize = sizeof(dpiUnawareMonitorInfo);
                      if (!GetMonitorInfo(monitorData.first, &dpiUnawareMonitorInfo))
                      {
                          return;
                      }
                  } }).wait();

                float width = static_cast<float>(monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left);
                float height = static_cast<float>(monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top);
                
                float dpiUnawareWidth = static_cast<float>(dpiUnawareMonitorInfo.rcMonitor.right - dpiUnawareMonitorInfo.rcMonitor.left);
                float dpiUnawareHeight = static_cast<float>(dpiUnawareMonitorInfo.rcMonitor.bottom - dpiUnawareMonitorInfo.rcMonitor.top);
                
                UINT dpi = 0;
                if (Common::Display::DPIAware::GetScreenDPIForMonitor(monitorData.first, dpi) != S_OK)
                {
                    continue;
                }

                Project::Monitor monitorId{
                    .monitor = monitorData.first,
                    .dpi = dpi,
                    .monitorRectDpiAware = Project::Monitor::MonitorRect {
                        .top = monitorInfo.rcMonitor.top,
                        .left = monitorInfo.rcMonitor.left,
                        .width = static_cast<int>(std::roundf(width)),
                        .height = static_cast<int>(std::roundf(height)),
                    },
                    .monitorRectDpiUnaware = Project::Monitor::MonitorRect {
                        .top = dpiUnawareMonitorInfo.rcMonitor.top,
                        .left = dpiUnawareMonitorInfo.rcMonitor.left,
                        .width = static_cast<int>(std::roundf(dpiUnawareWidth)),
                        .height = static_cast<int>(std::roundf(dpiUnawareHeight)),
                    },
                };

                bool foundActiveMonitor = false;
                DISPLAY_DEVICE displayDevice{ .cb = sizeof(displayDevice) };
                DWORD displayDeviceIndex = 0;
                while (EnumDisplayDevicesW(monitorInfo.szDevice, displayDeviceIndex, &displayDevice, EDD_GET_DEVICE_INTERFACE_NAME))
                {
                    /* 
                    * if (WI_IsFlagSet(displayDevice.StateFlags, DISPLAY_DEVICE_ACTIVE) &&
                        WI_IsFlagClear(displayDevice.StateFlags, DISPLAY_DEVICE_MIRRORING_DRIVER))
                    */
                    if (((displayDevice.StateFlags & DISPLAY_DEVICE_ACTIVE) == DISPLAY_DEVICE_ACTIVE) && 
                        (displayDevice.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) == 0)
                    {
                        // Find display devices associated with the display.
                        foundActiveMonitor = true;
                        break;
                    }

                    displayDeviceIndex++;
                }

                if (foundActiveMonitor)
                {
                    auto deviceId = SplitDisplayDeviceId(displayDevice.DeviceID);
                    monitorId.id = deviceId.first;
                    monitorId.instanceId = deviceId.second;
                    try
                    {
                        std::wstring numberStr = displayDevice.DeviceName; // \\.\DISPLAY1\Monitor0
                        numberStr = numberStr.substr(0, numberStr.find_last_of('\\')); // \\.\DISPLAY1
                        numberStr = remove_non_digits(numberStr);
                        monitorId.number = std::stoi(numberStr);
                    }
                    catch (...)
                    {
                        monitorId.number = 0;
                    }
                }
                else
                {
                    success = false;

                    // Use the display name as a fallback value when no proper device was found.
                    monitorId.id = monitorInfo.szDevice;
                    monitorId.instanceId = L"";

                    try
                    {
                        std::wstring numberStr = monitorInfo.szDevice; // \\.\DISPLAY1
                        numberStr = remove_non_digits(numberStr);
                        monitorId.number = std::stoi(numberStr);
                    }
                    catch (...)
                    {
                        monitorId.number = 0;
                    }
                }

                result.push_back(std::move(monitorId));
            }

            return { success, result };
		}

		std::pair<std::wstring, std::wstring> SplitDisplayDeviceId(const std::wstring& str) noexcept
		{
            // format:  \\?\DISPLAY#{device id}#{instance id}#{some other id}
            // example: \\?\DISPLAY#GSM1388#4&125707d6&0&UID8388688#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
            // output:  { GSM1388, 4&125707d6&0&UID8388688 }

            size_t nameStartPos = str.find_first_of('#');
            size_t uidStartPos = str.find('#', nameStartPos + 1);
            size_t uidEndPos = str.find('#', uidStartPos + 1);

            if (nameStartPos == std::string::npos || uidStartPos == std::string::npos || uidEndPos == std::string::npos)
            {
                return { str, L"" };
            }

            return { str.substr(nameStartPos + 1, uidStartPos - nameStartPos - 1), str.substr(uidStartPos + 1, uidEndPos - uidStartPos - 1) };
		}
	}

	std::vector<Project::Monitor> IdentifyMonitors() noexcept
	{
        auto displaysResult = Display::GetDisplays();

        // retry 
        int retryCounter = 0;
        while (!displaysResult.first && retryCounter < 100)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(30));
            displaysResult = Display::GetDisplays();
            retryCounter++;
        }

        return displaysResult.second;
	}
}