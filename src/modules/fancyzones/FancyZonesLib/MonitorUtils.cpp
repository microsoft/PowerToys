#include "pch.h"
#include "MonitorUtils.h"

#include <WbemCli.h>
#include <comutil.h>

#include <FancyZonesLib/WindowUtils.h>
#include <FancyZonesLib/util.h>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

namespace MonitorUtils
{
    constexpr int CUSTOM_POSITIONING_LEFT_TOP_PADDING = 16;

    namespace WMI
    {
        FancyZonesDataTypes::DeviceId SplitWMIDeviceId(const std::wstring& str) noexcept
        {
            // format:  DISPLAY\{device id}\{instance id}
            // example: DISPLAY\GSM0001\4&125707d6&0&UID28741_0
            // output: { GSM0001, 4&125707d6&0&UID28741 }

            size_t nameStartPos = str.find_first_of('\\');
            size_t uidStartPos = str.find_last_of('\\');
            size_t uidEndPos = str.find_last_of('_');

            if (nameStartPos == std::string::npos || uidStartPos == std::string::npos || uidEndPos == std::string::npos)
            {
                return { .id = str };
            }

            return { .id = str.substr(nameStartPos + 1, uidStartPos - nameStartPos - 1), .instanceId = str.substr(uidStartPos + 1, uidEndPos - uidStartPos - 1) };
        }

        std::wstring GetWMIProp(IWbemClassObject* wbemClassObj, std::wstring_view prop)
        {
            if (!wbemClassObj)
            {
                return {};
            }

            VARIANT vtProp{};

            // Get the value of the Name property
            auto hres = wbemClassObj->Get(prop.data(), 0, &vtProp, 0, 0);
            if (FAILED(hres))
            {
                Logger::error(L"Get {} Error code = {} ", prop, get_last_error_or_default(hres));
                return {};
            }

            std::wstring result{};
            switch (vtProp.vt)
            {
            case VT_BSTR: //BSTR
            {
                result = vtProp.bstrVal;
            }
            break;
            case VT_ARRAY: // parray
            case 8195: // also parray
            {
                std::u32string str(static_cast<const char32_t*>(vtProp.parray->pvData));
                std::wstring wstr;
                for (const char32_t& c : str)
                {
                    wstr += (wchar_t)c;
                }

                result = wstr;
            }
            break;
            default:
            break;
            }

            VariantClear(&vtProp);
            return result;
        }

        std::vector<FancyZonesDataTypes::MonitorId> GetHardwareMonitorIds()
        {
            HRESULT hres;

            // Obtain the initial locator to Windows Management
            // on a particular host computer.
            IWbemLocator* pLocator = 0;

            hres = CoCreateInstance(CLSID_WbemLocator, 0, CLSCTX_INPROC_SERVER, IID_IWbemLocator, reinterpret_cast<LPVOID*>(&pLocator));
            if (FAILED(hres))
            {
                Logger::error(L"Failed to create IWbemLocator object. {}", get_last_error_or_default(hres));
                return {};
            }

            IWbemServices* pServices = 0;
            hres = pLocator->ConnectServer(_bstr_t(L"ROOT\\WMI"), NULL, NULL, 0, NULL, 0, 0, &pServices);
            if (FAILED(hres))
            {
                Logger::error(L"Could not connect WMI server. {}", get_last_error_or_default(hres));
                pLocator->Release();
                return {};
            }

            // Set the IWbemServices proxy so that impersonation
            // of the user (client) occurs.
            hres = CoSetProxyBlanket(pServices, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE, NULL, RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE, NULL, EOAC_NONE);
            if (FAILED(hres))
            {
                Logger::error(L"Could not set proxy blanket. {}", get_last_error_or_default(hres));
                pServices->Release();
                pLocator->Release();
                return {};
            }

            // Use the IWbemServices pointer to make requests of WMI.
            // Make requests here:
            IEnumWbemClassObject* pEnumerator = NULL;
            hres = pServices->ExecQuery(bstr_t("WQL"), bstr_t("SELECT * FROM WmiMonitorID"), WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY, NULL, &pEnumerator);
            if (FAILED(hres))
            {
                Logger::error(L"Query for monitors failed. {}", get_last_error_or_default(hres));
                pServices->Release();
                pLocator->Release();
                return {};
            }

            IWbemClassObject* pClassObject;
            ULONG uReturn = 0;
            
            std::vector<FancyZonesDataTypes::MonitorId> result{};
            while (pEnumerator)
            {
                hres = pEnumerator->Next(WBEM_INFINITE, 1, &pClassObject, &uReturn);

                if (0 == uReturn)
                {
                    break;
                }

                LPSAFEARRAY pFieldArray = NULL;
                hres = pClassObject->GetNames(NULL, WBEM_FLAG_ALWAYS, NULL, &pFieldArray);
                if (FAILED(hres))
                {
                    Logger::error(L"Failed to get field names. {}", get_last_error_or_default(hres));
                    break;
                }

                auto name = GetWMIProp(pClassObject, L"InstanceName");
                
                FancyZonesDataTypes::MonitorId data{};
                data.deviceId = SplitWMIDeviceId(name);
                data.serialNumber = GetWMIProp(pClassObject, L"SerialNumberID");

                Logger::info(L"InstanceName: {}", name);
                Logger::info(L"Serial number: {}", data.serialNumber);

                result.emplace_back(std::move(data));

                pClassObject->Release();
                pClassObject = NULL;
            }

            pServices->Release();
            pLocator->Release();
            pEnumerator->Release();

            return result;
        }
    }
    
    namespace Display
    {
        FancyZonesDataTypes::DeviceId SplitDisplayDeviceId(const std::wstring& str) noexcept
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

            return { .id = str.substr(nameStartPos + 1, uidStartPos - nameStartPos - 1), .instanceId = str.substr(uidStartPos + 1, uidEndPos - uidStartPos - 1) };
        }

        FancyZonesDataTypes::DeviceId ConvertObsoleteDeviceId(const std::wstring& str) noexcept
        {
            // format:  {device id}#{instance id}
            // example: GSM1388#4&125707d6&0&UID8388688
            // output:  { GSM1388, 4&125707d6&0&UID8388688 }

            size_t dividerPos = str.find_first_of('#');

            if (dividerPos == std::string::npos)
            {
                return { str, L"" };
            }

            return { .id = str.substr(0, dividerPos), .instanceId = str.substr(dividerPos + 1) };
        }

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

        std::pair<bool, std::vector<FancyZonesDataTypes::MonitorId>> GetDisplays()
        {
            bool success = true;
            std::vector<FancyZonesDataTypes::MonitorId> result{};
            
            auto allMonitors = FancyZonesUtils::GetAllMonitorInfo<&MONITORINFOEX::rcWork>();
            for (auto& monitorData : allMonitors)
            {
                auto monitorInfo = monitorData.second;

                DISPLAY_DEVICE displayDevice{ .cb = sizeof(displayDevice) };

                FancyZonesDataTypes::MonitorId monitorId {
                    .monitor = monitorData.first
                };

                bool foundActiveMonitor = false;

                DWORD displayDeviceIndex = 0;

                while (EnumDisplayDevicesW(monitorInfo.szDevice, displayDeviceIndex, &displayDevice, EDD_GET_DEVICE_INTERFACE_NAME))
                {
                    Logger::info(L"Get display device for display {} : {}", monitorInfo.szDevice, displayDevice.DeviceID);
                    if (WI_IsFlagSet(displayDevice.StateFlags, DISPLAY_DEVICE_ACTIVE) &&
                        WI_IsFlagClear(displayDevice.StateFlags, DISPLAY_DEVICE_MIRRORING_DRIVER))
                    {
                        // Find display devices associated with the display.
                        foundActiveMonitor = true;
                        break;
                    }
                    displayDeviceIndex++;
                }

                if (foundActiveMonitor)
                {
                    monitorId.deviceId = SplitDisplayDeviceId(displayDevice.DeviceID);
                    try
                    {
                        std::wstring numberStr = displayDevice.DeviceName; // \\.\DISPLAY1\Monitor0
                        numberStr = numberStr.substr(0, numberStr.find_last_of('\\')); // \\.\DISPLAY1
                        numberStr = remove_non_digits(numberStr);
                        monitorId.deviceId.number = std::stoi(numberStr);
                    }
                    catch (...)
                    {
                        Logger::error(L"Failed to get monitor number from {}", displayDevice.DeviceName);
                        monitorId.deviceId.number = 0;
                    }
                }
                else
                {
                    success = false;

                    // Use the display name as a fallback value when no proper device was found.
                    monitorId.deviceId.id = monitorInfo.szDevice;
                    monitorId.deviceId.instanceId = L"";

                    try
                    {
                        std::wstring numberStr = monitorInfo.szDevice; // \\.\DISPLAY1
                        numberStr = remove_non_digits(numberStr);
                        monitorId.deviceId.number = std::stoi(numberStr);
                    }
                    catch (...)
                    {
                        Logger::error(L"Failed to get display number from {}", monitorInfo.szDevice);
                        monitorId.deviceId.number = 0;
                    }
                }

                result.push_back(std::move(monitorId));
            }

            return {success, result};
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
                    FancyZonesWindowUtils::SizeWindowToRect(window, newPosition, false);
                }
            }
        }
    }
   
    std::vector<FancyZonesDataTypes::MonitorId> IdentifyMonitors() noexcept
    {
        Logger::info(L"Identifying monitors");

        auto displaysResult = Display::GetDisplays();
        auto monitors = WMI::GetHardwareMonitorIds();

        // retry 
        int retryCounter = 0;
        while (!displaysResult.first && retryCounter < 100)
        {
            Logger::info("Retry display identification");
            std::this_thread::sleep_for(std::chrono::milliseconds(30));
            displaysResult = Display::GetDisplays();
            retryCounter++;
        }

        for (const auto& monitor : monitors)
        {
            for (auto& display : displaysResult.second)
            {
                if (monitor.deviceId.id == display.deviceId.id)
                {
                    display.serialNumber = monitor.serialNumber;    
                }
            }
        }
        
        return displaysResult.second;
    }

    FancyZonesUtils::Rect GetWorkAreaRect(HMONITOR monitor)
    {
        if (monitor)
        {
            MONITORINFO mi{};
            mi.cbSize = sizeof(mi);
            if (GetMonitorInfoW(monitor, &mi))
            {
                return FancyZonesUtils::Rect(mi.rcWork);
            }
        }

        return FancyZonesUtils::Rect{};
    }
}