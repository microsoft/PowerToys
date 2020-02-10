#pragma once

#include <shlwapi.h>

namespace RegistryHelpers
{
    static PCWSTR REG_SETTINGS = L"Software\\SuperFancyZones";
    static PCWSTR APP_ZONE_HISTORY_SUBKEY = L"AppZoneHistory";

    inline HRESULT GetCurrentVirtualDesktop(_Out_ GUID* id)
    {
        *id = GUID_NULL;

        DWORD sessionId;
        ProcessIdToSessionId(GetCurrentProcessId(), &sessionId);

        wchar_t sessionKeyPath[256]{};
        RETURN_IF_FAILED(
            StringCchPrintfW(
                sessionKeyPath,
                ARRAYSIZE(sessionKeyPath),
                L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\SessionInfo\\%d\\VirtualDesktops",
                sessionId));
        wil::unique_hkey key{};
        GUID value{};

        if (RegOpenKeyExW(HKEY_CURRENT_USER, sessionKeyPath, 0, KEY_ALL_ACCESS, &key) == ERROR_SUCCESS)
        {
            DWORD size = sizeof(value);
            if (RegQueryValueExW(key.get(), L"CurrentVirtualDesktop", 0, nullptr, reinterpret_cast<BYTE*>(&value), &size) == ERROR_SUCCESS)
            {
                *id = value;
                return S_OK;
            }
        }
        return E_FAIL;
    }
}