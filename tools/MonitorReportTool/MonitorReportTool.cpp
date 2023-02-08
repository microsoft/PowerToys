#include "pch.h"
#include "MonitorReportTool.h"

#include <WbemCli.h>
#include <dwmapi.h>
#include <comutil.h>

#include <unordered_map>

#include "ErrorMessage.h"
#include "Logger.h"

namespace FancyZonesUtils
{
    template<RECT MONITORINFO::* member>
    std::vector<std::pair<HMONITOR, MONITORINFOEX>> GetAllMonitorInfo()
    {
        using result_t = std::vector<std::pair<HMONITOR, MONITORINFOEX>>;
        result_t result;

        auto enumMonitors = [](HMONITOR monitor, HDC, LPRECT, LPARAM param) -> BOOL {
            MONITORINFOEX mi;
            mi.cbSize = sizeof(mi);
            result_t& result = *reinterpret_cast<result_t*>(param);
            if (GetMonitorInfo(monitor, &mi))
            {
                result.push_back({ monitor, mi });
            }

            return TRUE;
        };

        EnumDisplayMonitors(NULL, NULL, enumMonitors, reinterpret_cast<LPARAM>(&result));
        return result;
    }
}

void LogEnumDisplayMonitors()
{
    Logger::log(L" ---- EnumDisplayMonitors as in FancyZones ---- ");

    auto allMonitors = FancyZonesUtils::GetAllMonitorInfo<&MONITORINFOEX::rcWork>();
    std::unordered_map<std::wstring, DWORD> displayDeviceIdxMap;

    for (auto& monitorData : allMonitors)
    {
        auto monitorInfo = monitorData.second;

        DISPLAY_DEVICE displayDevice{ .cb = sizeof(displayDevice) };
        std::wstring deviceId;
        auto enumRes = EnumDisplayDevicesW(monitorInfo.szDevice, displayDeviceIdxMap[monitorInfo.szDevice], &displayDevice, EDD_GET_DEVICE_INTERFACE_NAME);

        if (enumRes == 0)
        {
            Logger::log(get_last_error_or_default(GetLastError()));
        }
        else
        {
            Logger::log(L"DeviceId: {}", std::wstring(displayDevice.DeviceID));
            Logger::log(L"DeviceKey: {}", std::wstring(displayDevice.DeviceKey));
            Logger::log(L"DeviceName: {}", std::wstring(displayDevice.DeviceName));
            Logger::log(L"DeviceString: {}", std::wstring(displayDevice.DeviceString));
            Logger::log(L"");
        }
    }

    Logger::log(L"");
}

void LogPrintDisplayDevice(const DISPLAY_DEVICE& displayDevice, bool internal)
{
    const bool active = displayDevice.StateFlags & DISPLAY_DEVICE_ACTIVE;
    const bool mirroring = displayDevice.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER;
    const bool modesPruned = displayDevice.StateFlags & DISPLAY_DEVICE_MODESPRUNED;
    const bool primaryDevice = displayDevice.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE;
    const bool removable = displayDevice.StateFlags & DISPLAY_DEVICE_REMOVABLE;
    const bool VGA_Compatible = displayDevice.StateFlags & DISPLAY_DEVICE_VGA_COMPATIBLE;

    Logger::log(L"{}DeviceId: {}", internal?L"--> ":L"", std::wstring(displayDevice.DeviceID));
    Logger::log(L"{}DeviceKey: {}", internal?L"--> ":L"", std::wstring(displayDevice.DeviceKey));
    Logger::log(L"{}DeviceName: {}", internal?L"--> ":L"", std::wstring(displayDevice.DeviceName));
    Logger::log(L"{}DeviceString: {}", internal?L"--> ":L"", std::wstring(displayDevice.DeviceString));
    Logger::log(L"{}StateFlags: {}", internal?L"--> ":L"", displayDevice.StateFlags);
    Logger::log(L"{}active: {}", internal?L"--> ":L"", active);
    Logger::log(L"{}mirroring: {}", internal?L"--> ":L"", mirroring);
    Logger::log(L"{}modesPruned: {}", internal?L"--> ":L"", modesPruned);
    Logger::log(L"{}primaryDevice: {}", internal?L"--> ":L"", primaryDevice);
    Logger::log(L"{}removable: {}", internal?L"--> ":L"", removable);
    Logger::log(L"{}VGA_Compatible: {}", internal?L"--> ":L"", VGA_Compatible);
    Logger::log(L"");
}

void LogExhaustiveDisplayDevices(bool use_EDD_GET_DEVICE_INTERFACE_NAME)
{
    Logger::log(L" ---- Exhaustive EnumDisplayDevicesW {} EDD_GET_DEVICE_INTERFACE_NAME ---- ", use_EDD_GET_DEVICE_INTERFACE_NAME?L"with":L"without");
    DISPLAY_DEVICE displayDevice{ .cb = sizeof(DISPLAY_DEVICE) };
    DWORD deviceIdx = 0;
    while (EnumDisplayDevicesW(nullptr, deviceIdx, &displayDevice, EDD_GET_DEVICE_INTERFACE_NAME))
    {
        LogPrintDisplayDevice(displayDevice, false);
        DISPLAY_DEVICE displayDeviceInternal{ .cb = sizeof(DISPLAY_DEVICE) };
        DWORD deviceIdxInternal = 0;
        while (EnumDisplayDevicesW(displayDevice.DeviceName, deviceIdxInternal, &displayDeviceInternal, EDD_GET_DEVICE_INTERFACE_NAME)) {
            Logger::log(L"Inside {} there's:", displayDevice.DeviceName);
            LogPrintDisplayDevice(displayDeviceInternal, true);
            deviceIdxInternal++;
        }
        deviceIdx++;
    }
}

void LogEnumDisplayMonitorsProper()
{

    auto allMonitors = FancyZonesUtils::GetAllMonitorInfo<&MONITORINFOEX::rcWork>();

    Logger::log(L" ---- FancyZonesUtils::GetAllMonitorInfo ---- ");
    for (auto& monitorData : allMonitors)
    {
        auto monitorInfo = monitorData.second;
        Logger::log(L"szDevice: {}", std::wstring(monitorInfo.szDevice));
        Logger::log(L"cbSize: {}", monitorInfo.cbSize);
        Logger::log(L"dwFlags: {}", monitorInfo.dwFlags);
        Logger::log(L"");
    }

    LogExhaustiveDisplayDevices(true);
    LogExhaustiveDisplayDevices(false);

    Logger::log(L"");
}

void LogWMIProp(IWbemClassObject* wbemClassObj, std::wstring_view prop)
{
    if (!wbemClassObj)
    {
        return;
    }

    VARIANT vtProp{};

    // Get the value of the Name property
    auto hres = wbemClassObj->Get(prop.data(), 0, &vtProp, 0, 0);
    if (FAILED(hres))
    {
        Logger::log(L"Get {} Error code = {} ", prop, get_last_error_or_default(hres));
        return;
    }

    switch (vtProp.vt)
    {
    case VT_I2: //short
    {
        Logger::log(L"{} : {}", prop, vtProp.iVal);
    }
    break;
    case VT_I4: //int, long
    {
        Logger::log(L"{} : {}", prop, vtProp.lVal);
    }
    break;
    case VT_BSTR: //BSTR
    {
        Logger::log(L"{} : {}", prop, vtProp.bstrVal);
    }
    break;
    case VT_UI1: //BYTE (unsigned char)
    {
        Logger::log(L"{} : {}", prop, vtProp.bVal);
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

        Logger::log(L"{} : {}", prop, wstr);
    }
    break;
    default:
    {
        Logger::log(L"{} : value is empty", prop);
    }
        break;
    }

    VariantClear(&vtProp);
}

void LogWMI()
{
    Logger::log(L" ---- WMI ---- ");

    HRESULT hres;

    // Initialize COM.
    hres = CoInitializeEx(0, COINIT_MULTITHREADED);
    if (FAILED(hres))
    {
        Logger::log(L"Failed to initialize COM library. Error code = ", hres);
        return;
    }

    // Initialize 
    hres = CoInitializeSecurity(NULL, -1, NULL, NULL, RPC_C_AUTHN_LEVEL_DEFAULT,
        RPC_C_IMP_LEVEL_IMPERSONATE, NULL, EOAC_NONE, NULL);

    if (FAILED(hres))
    {
        Logger::log(L"Failed to initialize security. Error code = ", hres);
        CoUninitialize();
        return;
    }

    // Obtain the initial locator to Windows Management
    // on a particular host computer.
    IWbemLocator* pLocator = 0;

    hres = CoCreateInstance(CLSID_WbemLocator, 0, CLSCTX_INPROC_SERVER, IID_IWbemLocator, reinterpret_cast<LPVOID*>(&pLocator));
    if (FAILED(hres))
    {
        Logger::log(L"Failed to create IWbemLocator object. Error code = ", hres);
        CoUninitialize();
        return;
    }

    IWbemServices* pServices = 0;
    hres = pLocator->ConnectServer(_bstr_t(L"ROOT\\WMI"), NULL, NULL, 0, NULL, 0, 0, &pServices);

    if (FAILED(hres))
    {
        Logger::log(L"Could not connect WMI server. Error code = ", hres);
        pLocator->Release();
        CoUninitialize();
        return;
    }

    Logger::log(L"Connected to ROOT\\WMI WMI namespace");
    Logger::log(L"");


    // Set the IWbemServices proxy so that impersonation
    // of the user (client) occurs.
    hres = CoSetProxyBlanket(pServices, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE, NULL,
        RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE, NULL, EOAC_NONE);

    if (FAILED(hres))
    {
        Logger::log(L"Could not set proxy blanket. Error code = ", hres);
        pServices->Release();
        pLocator->Release();
        CoUninitialize();
        return;
    }

    // Use the IWbemServices pointer to make requests of WMI. 
    // Make requests here:
    IEnumWbemClassObject* pEnumerator = NULL;

    hres = pServices->ExecQuery(bstr_t("WQL"), bstr_t("SELECT * FROM WmiMonitorID"),
        WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY, NULL, &pEnumerator);

    if (FAILED(hres))
    {
        Logger::log(L"Query for monitors failed. Error code = ", hres);
        pServices->Release();
        pLocator->Release();
        CoUninitialize();
        return;
    }

    IWbemClassObject* pClassObject;
    ULONG uReturn = 0;

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
            Logger::log(L"Failed to get field names. Error code = {}", get_last_error_or_default(hres));
            break;
        }

        LogWMIProp(pClassObject, L"InstanceName");
        
        LogWMIProp(pClassObject, L"YearOfManufacture");
        LogWMIProp(pClassObject, L"WeekOfManufacture");

        LogWMIProp(pClassObject, L"UserFriendlyNameLength");
        LogWMIProp(pClassObject, L"UserFriendlyName");
        LogWMIProp(pClassObject, L"ManufacturerName");

        LogWMIProp(pClassObject, L"SerialNumberID");
        LogWMIProp(pClassObject, L"ProductCodeID");
        
        Logger::log(L"");

        pClassObject->Release();
        pClassObject = NULL;
    }

    pServices->Release();
    pLocator->Release();
    pEnumerator->Release();

    CoUninitialize();
}

void LogWMICIMV2()
{
    Logger::log(L" ---- WMI ---- ");

    HRESULT hres;

    hres = CoInitializeEx(0, COINIT_MULTITHREADED);
    if (FAILED(hres))
    {
        Logger::log(L"Failed to initialize COM library. Error code = ", hres);
        return;
    }

    hres = CoInitializeSecurity(NULL, -1, NULL, NULL, RPC_C_AUTHN_LEVEL_DEFAULT,
        RPC_C_IMP_LEVEL_IMPERSONATE, NULL, EOAC_NONE, NULL);

    if (FAILED(hres))
    {
        Logger::log(L"Failed to initialize security. Error code = ", hres);
        CoUninitialize();
        return;
    }

    // Obtain the initial locator to Windows Management
    // on a particular host computer.
    IWbemLocator* pLocator = 0;

    hres = CoCreateInstance(CLSID_WbemLocator, 0, CLSCTX_INPROC_SERVER, IID_IWbemLocator, reinterpret_cast<LPVOID*>(&pLocator));
    if (FAILED(hres))
    {
        Logger::log(L"Failed to create IWbemLocator object. Error code = ", hres);
        CoUninitialize();
        return;
    }

    IWbemServices* pServices = 0;

    // Connect to the root\cimv2 namespace with the
    // current user and obtain pointer pSvc
    // to make IWbemServices calls.

    hres = pLocator->ConnectServer(_bstr_t(L"ROOT\\CIMV2"), NULL, NULL, 0, NULL, 0, 0, &pServices);

    if (FAILED(hres))
    {
        Logger::log(L"Could not connect WMI server. Error code = ", hres);
        pLocator->Release();
        CoUninitialize();
        return;
    }

    Logger::log(L"Connected to ROOT\\CIMV2 WMI namespace");
    Logger::log(L"");

    // Set the IWbemServices proxy so that impersonation
    // of the user (client) occurs.
    hres = CoSetProxyBlanket(pServices, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE, NULL,
        RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE, NULL, EOAC_NONE);

    if (FAILED(hres))
    {
        Logger::log(L"Could not set proxy blanket. Error code = ", hres);
        pServices->Release();
        pLocator->Release();
        CoUninitialize();
        return;
    }

    // Use the IWbemServices pointer to make requests of WMI. 
    // Make requests here:
    IEnumWbemClassObject* pEnumerator = NULL;
    hres = pServices->ExecQuery(bstr_t("WQL"), bstr_t("SELECT * FROM Win32_DesktopMonitor"),
        WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY, NULL, &pEnumerator);

    if (FAILED(hres))
    {
        Logger::log(L"Query for monitors failed. Error code = ", hres);
        pServices->Release();
        pLocator->Release();
        CoUninitialize();
        return;
    }

    IWbemClassObject* pClassObject;
    ULONG uReturn = 0;

    while (pEnumerator)
    {
        hres = pEnumerator->Next(WBEM_INFINITE, 1, &pClassObject, &uReturn);

        if (0 == uReturn)
        {
            break;
        }

        LogWMIProp(pClassObject, L"DeviceID");
        LogWMIProp(pClassObject, L"Caption");
        LogWMIProp(pClassObject, L"Description");
        LogWMIProp(pClassObject, L"MonitorManufacturer");
        LogWMIProp(pClassObject, L"MonitorType");
        LogWMIProp(pClassObject, L"Name");
        LogWMIProp(pClassObject, L"PNPDeviceID");
        LogWMIProp(pClassObject, L"Status");

        LogWMIProp(pClassObject, L"Availability");

        Logger::log(L"");

        pClassObject->Release();
        pClassObject = NULL;
    }

    pServices->Release();
    pLocator->Release();
    pEnumerator->Release();

    CoUninitialize();
}

void LogCCD()
{
    Logger::log(L" ---- CCD ---- ");

    LONG result = ERROR_SUCCESS;
    std::vector<DISPLAYCONFIG_PATH_INFO> paths;
    std::vector<DISPLAYCONFIG_MODE_INFO> modes;

    do
    {
        UINT32 pathCount{}, modeCount{};
        auto sizesResult = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS | QDC_INCLUDE_HMD | QDC_VIRTUAL_MODE_AWARE, &pathCount, &modeCount);

        if (sizesResult != ERROR_SUCCESS)
        {
            Logger::log(L"GetDisplayConfigBufferSizes error {}", get_last_error_or_default(sizesResult));
            return;
        }

        paths.resize(pathCount);
        paths.resize(modeCount);

        auto result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS | QDC_INCLUDE_HMD | QDC_VIRTUAL_MODE_AWARE, &pathCount, paths.data()
            , &modeCount, modes.data(), nullptr);

        // The function may have returned fewer paths/modes than estimated
        paths.resize(pathCount);
        modes.resize(modeCount);
    } while (result == ERROR_INSUFFICIENT_BUFFER);

    if (result != ERROR_SUCCESS)
    {
        Logger::log(L"QueryDisplayConfig error {}", get_last_error_or_default(result));
        return;
    }

    // For each active path
    for (auto& path : paths)
    {
        // Find the target (monitor) friendly name
        DISPLAYCONFIG_TARGET_DEVICE_NAME targetName = {};
        targetName.header.adapterId = path.targetInfo.adapterId;
        targetName.header.id = path.targetInfo.id;
        targetName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
        targetName.header.size = sizeof(targetName);
        result = DisplayConfigGetDeviceInfo(&targetName.header);

        if (result != ERROR_SUCCESS)
        {
            Logger::log(L"DisplayConfigGetDeviceInfo error {}", get_last_error_or_default(result));
        }

        // Find the adapter device name
        DISPLAYCONFIG_ADAPTER_NAME adapterName = {};
        adapterName.header.adapterId = path.targetInfo.adapterId;
        adapterName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME;
        adapterName.header.size = sizeof(adapterName);

        result = DisplayConfigGetDeviceInfo(&adapterName.header);

        if (result != ERROR_SUCCESS)
        {
            Logger::log(L"DisplayConfigGetDeviceInfo error {}", get_last_error_or_default(result));
            continue;
        }

        Logger::log(L"Monitor: {} connected to adapter {}"
            , (targetName.flags.friendlyNameFromEdid ? targetName.monitorFriendlyDeviceName : L"Unknown")
            , adapterName.adapterDevicePath);
    }
}

void LogInfo()
{
    Logger::log(L"Timestamp: {}", std::chrono::system_clock::now());
    Logger::log(L"");

    LogEnumDisplayMonitors();
    LogEnumDisplayMonitorsProper();
    LogWMICIMV2();
    LogWMI();
    LogCCD();

    Logger::log(L"=======================================");
    Logger::log(L"");
}

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                     _In_opt_ HINSTANCE hPrevInstance,
                     _In_ LPWSTR    lpCmdLine,
                     _In_ int       nCmdShow)
{
    UNREFERENCED_PARAMETER(hPrevInstance);
    UNREFERENCED_PARAMETER(lpCmdLine);

    Logger::init("MonitorReportTool");
    
    LogInfo();

    return 0;
}
