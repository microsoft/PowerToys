#include "DisplayUtils.h"

#include <algorithm>
#include <cwctype>
#include <iterator>

#include <dpi_aware.h>
#include <MonitorEnumerator.h>

#include <utils/OnThreadExecutor.h>

namespace DisplayUtils
{
    std::wstring remove_non_digits(const std::wstring& input)
    {
        std::wstring result;
        std::copy_if(input.begin(), input.end(), std::back_inserter(result), [](wchar_t ch) { return std::iswdigit(ch); });
        return result;
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

    std::pair<bool, std::vector<DisplayUtils::DisplayData>> GetDisplays()
    {
        bool success = true;
        std::vector<DisplayUtils::DisplayData> result{};
        auto allMonitors = MonitorEnumerator::Enumerate();

        OnThreadExecutor dpiUnawareThread;
        dpiUnawareThread.submit(OnThreadExecutor::task_t{ [&] {
            SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_UNAWARE);
            SetThreadDpiHostingBehavior(DPI_HOSTING_BEHAVIOR_MIXED);
        } }).wait();

        for (auto& monitorData : allMonitors)
        {
            MONITORINFOEX monitorInfo = monitorData.second;
            MONITORINFOEX dpiUnawareMonitorInfo{};

            dpiUnawareThread.submit(OnThreadExecutor::task_t{ [&] {
                dpiUnawareMonitorInfo.cbSize = sizeof(dpiUnawareMonitorInfo);
                if (!GetMonitorInfo(monitorData.first, &dpiUnawareMonitorInfo))
                {
                    return;
                }
            } }).wait();

            UINT dpi = 0;
            if (DPIAware::GetScreenDPIForMonitor(monitorData.first, dpi) != S_OK)
            {
                success = false;
                break;
            }

            DisplayUtils::DisplayData data{
                .monitor = monitorData.first,
                .dpi = dpi,
                .monitorRectDpiAware = monitorInfo.rcMonitor,
                .monitorRectDpiUnaware = dpiUnawareMonitorInfo.rcMonitor,
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
                data.id = deviceId.first;
                data.instanceId = deviceId.second;
                try
                {
                    std::wstring numberStr = displayDevice.DeviceName; // \\.\DISPLAY1\Monitor0
                    numberStr = numberStr.substr(0, numberStr.find_last_of('\\')); // \\.\DISPLAY1
                    numberStr = remove_non_digits(numberStr);
                    data.number = std::stoi(numberStr);
                }
                catch (...)
                {
                    success = false;
                    break;
                }
            }
            else
            {
                success = false;

                // Use the display name as a fallback value when no proper device was found.
                data.id = monitorInfo.szDevice;
                data.instanceId = L"";

                try
                {
                    std::wstring numberStr = monitorInfo.szDevice; // \\.\DISPLAY1
                    numberStr = remove_non_digits(numberStr);
                    data.number = std::stoi(numberStr);
                }
                catch (...)
                {
                    success = false;
                    break;
                }
            }

            result.push_back(data);
        }

        return { success, result };
    }

}
