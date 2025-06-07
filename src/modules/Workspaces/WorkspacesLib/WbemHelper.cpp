#include "pch.h"
#include "WbemHelper.h"

#include <comdef.h>
#include <Wbemidl.h>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

#pragma comment(lib, "wbemuuid.lib")

std::unique_ptr<WbemHelper> WbemHelper::Create()
{
    auto instance = std::unique_ptr<WbemHelper>(new WbemHelper());
    if (instance->Initialize())
    {
        return instance;
    }

    return nullptr;
}

WbemHelper::~WbemHelper()
{
    if (m_services)
    {
        m_services->Release();
    }

    if (m_locator)
    {
        m_locator->Release();
    }
}

std::wstring WbemHelper::GetCommandLineArgs(DWORD processID) const
{
    static std::wstring property = L"CommandLine";
    std::wstring query = L"SELECT " + property + L" FROM Win32_Process WHERE ProcessId = " + std::to_wstring(processID);
    return Query(query, property);
}

std::wstring WbemHelper::GetExecutablePath(DWORD processID) const
{
    static std::wstring property = L"ExecutablePath";
    std::wstring query = L"SELECT " + property + L" FROM Win32_Process WHERE ProcessId = " + std::to_wstring(processID);
    return Query(query, property);
}

bool WbemHelper::Initialize()
{
    // Obtain the initial locator to WMI.
    HRESULT hres = CoCreateInstance(CLSID_WbemLocator, 0, CLSCTX_INPROC_SERVER, IID_IWbemLocator, reinterpret_cast<LPVOID*>(&m_locator));
    if (FAILED(hres))
    {
        Logger::error(L"Failed to create IWbemLocator object. Error: {}", get_last_error_or_default(hres));
        return false;
    }

    // Connect to WMI through the IWbemLocator::ConnectServer method.
    hres = m_locator->ConnectServer(_bstr_t(L"ROOT\\CIMV2"), NULL, NULL, 0, NULL, 0, 0, &m_services);
    if (FAILED(hres))
    {
        Logger::error(L"Could not connect to WMI. Error: {}", get_last_error_or_default(hres));
        return false;
    }

    // Set security levels on the proxy.
    hres = CoSetProxyBlanket(m_services, RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE, NULL, RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE, NULL, EOAC_NONE);
    if (FAILED(hres))
    {
        Logger::error(L"Could not set proxy blanket. Error: {}", get_last_error_or_default(hres));
        return false;
    }

    return true;
}

std::wstring WbemHelper::Query(const std::wstring& query, const std::wstring& propertyName) const
{
    if (!m_locator || !m_services)
    {
        return L"";
    }

    IEnumWbemClassObject* pEnumerator = NULL;

    HRESULT hres = m_services->ExecQuery(bstr_t("WQL"), bstr_t(query.c_str()), WBEM_FLAG_FORWARD_ONLY | WBEM_FLAG_RETURN_IMMEDIATELY, NULL, &pEnumerator);
    if (FAILED(hres))
    {
        Logger::error(L"Query for process failed. Error: {}", get_last_error_or_default(hres));
        return L"";
    }

    IWbemClassObject* pClassObject = NULL;
    ULONG uReturn = 0;
    std::wstring result = L"";
    while (pEnumerator)
    {
        HRESULT hr = pEnumerator->Next(WBEM_INFINITE, 1, &pClassObject, &uReturn);
        if (uReturn == 0)
        {
            break;
        }

        VARIANT vtProp;
        hr = pClassObject->Get(propertyName.c_str(), 0, &vtProp, 0, 0);
        if (SUCCEEDED(hr) && vtProp.vt == VT_BSTR)
        {
            result = vtProp.bstrVal;
        }
        VariantClear(&vtProp);

        pClassObject->Release();
    }

    pEnumerator->Release();

    return result;
}
