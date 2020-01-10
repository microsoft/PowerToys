#include "stdafx.h"
#include <commctrl.h>
#include "Settings.h"

const wchar_t c_rootRegPath[] = L"Software\\Microsoft\\ImageResizer";
const wchar_t c_enabled[] = L"Enabled";
const bool c_enabledDefault = true;

bool CSettings::GetEnabled()
{
    return GetRegBoolValue(c_enabled, c_enabledDefault);
}

bool CSettings::SetEnabled(_In_ bool enabled)
{
    return SetRegBoolValue(c_enabled, enabled);
}

bool CSettings::SetRegBoolValue(_In_ PCWSTR valueName, _In_ bool value)
{
    DWORD dwValue = value ? 1 : 0;
    return SetRegDWORDValue(valueName, dwValue);
}

bool CSettings::GetRegBoolValue(_In_ PCWSTR valueName, _In_ bool defaultValue)
{
    DWORD value = GetRegDWORDValue(valueName, (defaultValue == 0) ? false : true);
    return (value == 0) ? false : true;
}

bool CSettings::SetRegDWORDValue(_In_ PCWSTR valueName, _In_ DWORD value)
{
    return (SUCCEEDED(HRESULT_FROM_WIN32(SHSetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName, REG_DWORD, &value, sizeof(value)))));
}

DWORD CSettings::GetRegDWORDValue(_In_ PCWSTR valueName, _In_ DWORD defaultValue)
{
    DWORD retVal = defaultValue;
    DWORD type = REG_DWORD;
    DWORD dwEnabled = 0;
    DWORD cb = sizeof(dwEnabled);
    if (SHGetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName, &type, &dwEnabled, &cb) == ERROR_SUCCESS)
    {
        retVal = dwEnabled;
    }

    return retVal;
}

bool CSettings::SetRegStringValue(_In_ PCWSTR valueName, _In_ PCWSTR value)
{
    ULONG cb = (DWORD)((wcslen(value) + 1) * sizeof(*value));
    return (SUCCEEDED(HRESULT_FROM_WIN32(SHSetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName, REG_SZ, (const BYTE*)value, cb))));
}

bool CSettings::GetRegStringValue(_In_ PCWSTR valueName, __out_ecount(cchBuf) PWSTR value, DWORD cchBuf)
{
    if (cchBuf > 0)
    {
        value[0] = L'\0';
    }

    DWORD type = REG_SZ;
    ULONG cb = cchBuf * sizeof(*value);
    return (SUCCEEDED(HRESULT_FROM_WIN32(SHGetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName, &type, value, &cb) == ERROR_SUCCESS)));
}