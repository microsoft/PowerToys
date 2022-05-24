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
    Logger::log(L" ---- EnumDisplayMonitors ---- ");

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
        }
    }
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
    hres = CoInitializeSecurity(
        NULL,
        -1,      // COM negotiates service                  
        NULL,    // Authentication services
        NULL,    // Reserved
        RPC_C_AUTHN_LEVEL_DEFAULT,    // authentication
        RPC_C_IMP_LEVEL_IMPERSONATE,  // Impersonation
        NULL,             // Authentication info 
        EOAC_NONE,        // Additional capabilities
        NULL              // Reserved
    );
    if (FAILED(hres))
    {
        Logger::log(L"Failed to initialize security. Error code = ", hres);
        CoUninitialize();
        return;
    }

    // Obtain the initial locator to Windows Management
    // on a particular host computer.
    IWbemLocator* pLoc = 0;

    hres = CoCreateInstance(CLSID_WbemLocator, 0, CLSCTX_INPROC_SERVER, IID_IWbemLocator, (LPVOID*)&pLoc);
    if (FAILED(hres))
    {
        Logger::log(L"Failed to create IWbemLocator object. Error code = ", hres);
        CoUninitialize();
        return;
    }

    IWbemServices* pSvc = 0;

    // Connect to the root\cimv2 namespace with the
    // current user and obtain pointer pSvc
    // to make IWbemServices calls.

    hres = pLoc->ConnectServer(
        _bstr_t(L"ROOT\\CIMV2"), // WMI namespace
        NULL,                    // User name
        NULL,                    // User password
        0,                       // Locale
        NULL,                    // Security flags                 
        0,                       // Authority       
        0,                       // Context object
        &pSvc                    // IWbemServices proxy
    );

    if (FAILED(hres))
    {
        Logger::log(L"Could not connect WMI server. Error code = ", hres);
        pLoc->Release();
        CoUninitialize();
        return;
    }

    Logger::log(L"Connected to ROOT\\CIMV2 WMI namespace");

    // Set the IWbemServices proxy so that impersonation
    // of the user (client) occurs.
    hres = CoSetProxyBlanket(
        pSvc,                         // the proxy to set
        RPC_C_AUTHN_WINNT,            // authentication service
        RPC_C_AUTHZ_NONE,             // authorization service
        NULL,                         // Server principal name
        RPC_C_AUTHN_LEVEL_CALL,       // authentication level
        RPC_C_IMP_LEVEL_IMPERSONATE,  // impersonation level
        NULL,                         // client identity 
        EOAC_NONE                     // proxy capabilities     
    );

    if (FAILED(hres))
    {
        Logger::log(L"Could not set proxy blanket. Error code = ", hres);
        pSvc->Release();
        pLoc->Release();
        CoUninitialize();
        return;
    }


    // Use the IWbemServices pointer to make requests of WMI. 
    // Make requests here:

    IEnumWbemClassObject* pEnumerator = NULL;
    hres = pSvc->ExecQuery(
        bstr_t("WQL"),
        bstr_t("SELECT * FROM Win32_DesktopMonitor"),
        WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY,
        NULL,
        &pEnumerator);

    if (FAILED(hres))
    {
        Logger::log(L"Query for monitors failed. Error code = ", hres);
        pSvc->Release();
        pLoc->Release();
        CoUninitialize();
        return;
    }
    
    IWbemClassObject* pclsObj;
    ULONG uReturn = 0;

    while (pEnumerator)
    {
        hres = pEnumerator->Next(WBEM_INFINITE, 1,
            &pclsObj, &uReturn);

        if (0 == uReturn)
        {
            break;
        }

        VARIANT vtProp;

        // Get the value of the Name property
        hres = pclsObj->Get(L"DeviceID", 0, &vtProp, 0, 0);
        if (FAILED(hres))
        {
            Logger::log(L"Error code = ", hres);
            continue;
        }
        Logger::log(L"DeviceID : {}", vtProp.bstrVal);
        VariantClear(&vtProp);

        hres = pclsObj->Get(L"SystemName", 0, &vtProp, 0, 0);
        if (FAILED(hres))
        {
            Logger::log(L"Error code = ", hres);
            continue;
        }
        Logger::log(L"SystemName : {}", vtProp.bstrVal);
        VariantClear(&vtProp);

        hres = pclsObj->Get(L"Description", 0, &vtProp, 0, 0);
        if (FAILED(hres))
        {
            Logger::log(L"Error code = ", hres);
            continue;
        }
        Logger::log(L"Description : {}", vtProp.bstrVal);
        VariantClear(&vtProp);

        hres = pclsObj->Get(L"DisplayType", 0, &vtProp, 0, 0);
        if (FAILED(hres))
        {
            Logger::log(L"Error code = ", hres);
            continue;
        }
        if (vtProp.bstrVal)
        {
            Logger::log(L"DisplayType : {}", vtProp.bstrVal);
        }
        VariantClear(&vtProp);

        hres = pclsObj->Get(L"MonitorManufacturer", 0, &vtProp, 0, 0);
        if (FAILED(hres))
        {
            Logger::log(L"Error code = ", hres);
            continue;
        }
        if (vtProp.bstrVal)
        {
            Logger::log(L"MonitorManufacturer : {}", vtProp.bstrVal);
        }
        VariantClear(&vtProp);

        hres = pclsObj->Get(L"Name", 0, &vtProp, 0, 0);
        if (vtProp.bstrVal)
        {
            Logger::log(L"Name : {}", vtProp.bstrVal);
        }
        VariantClear(&vtProp);

        hres = pclsObj->Get(L"Caption", 0, &vtProp, 0, 0);
        if (vtProp.bstrVal)
        {
            Logger::log(L"Caption : {}", vtProp.bstrVal);
        }
        VariantClear(&vtProp);

        hres = pclsObj->Get(L"SystemCreationClassName", 0, &vtProp, 0, 0);
        if (vtProp.bstrVal)
        {
            Logger::log(L"SystemCreationClassName : {}", vtProp.bstrVal);
        }
        VariantClear(&vtProp);

        Logger::log(L"");

        pclsObj->Release();
        pclsObj = NULL;
    }

    // Cleanup
    // ========

    pSvc->Release();
    pLoc->Release();
    pEnumerator->Release();

    CoUninitialize();
}

void LogInfo()
{
    Logger::log(L"Timestamp: {}", std::chrono::system_clock::now());
    Logger::log(L"");

    LogEnumDisplayMonitors();
    LogWMI();

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
