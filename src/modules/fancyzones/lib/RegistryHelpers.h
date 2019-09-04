#pragma once

#include <shlwapi.h>

namespace RegistryHelpers
{
    static PCWSTR REG_SETTINGS = L"Software\\SuperFancyZones";
    static PCWSTR APP_ZONE_HISTORY_SUBKEY = L"AppZoneHistory";

    inline PCWSTR GetKey(_In_opt_ PCWSTR monitorId, PWSTR key, size_t keyLength)
    {
        if (monitorId)
        {
            StringCchPrintf(key, keyLength, L"%s\\%s", REG_SETTINGS, monitorId);
        }
        else
        {
            StringCchPrintf(key, keyLength, L"%s", REG_SETTINGS);
        }
        return key;
    }

    inline HKEY OpenKey(_In_opt_ PCWSTR monitorId)
    {
        HKEY hkey;
        wchar_t key[256];
        GetKey(monitorId, key, ARRAYSIZE(key));
        if (RegOpenKeyExW(HKEY_CURRENT_USER, key, 0, KEY_ALL_ACCESS, &hkey) == ERROR_SUCCESS)
        {
            return hkey;
        }
        return nullptr;
    }

    inline HKEY CreateKey(PCWSTR monitorId)
    {
        HKEY hkey;
        wchar_t key[256]{};
        GetKey(monitorId, key, ARRAYSIZE(key));
        if (RegCreateKeyExW(HKEY_CURRENT_USER, key, 0, nullptr, REG_OPTION_NON_VOLATILE, KEY_ALL_ACCESS, nullptr, &hkey, nullptr) == ERROR_SUCCESS)
        {
            return hkey;
        }
        return nullptr;
    }

    inline LSTATUS GetAppLastZone(HWND window, PCWSTR appPath, _Out_ PINT iZoneIndex)
    {
        *iZoneIndex = -1; 

        LSTATUS res{};
        if (auto monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL))
        {
            wchar_t keyPath[256]{};
            StringCchPrintf(keyPath, ARRAYSIZE(keyPath), L"%s\\%s\\%x", REG_SETTINGS, APP_ZONE_HISTORY_SUBKEY, monitor);

            DWORD zoneIndex;
            DWORD dataType = REG_DWORD;
            DWORD dataSize = sizeof(DWORD);
            res = SHRegGetUSValueW(keyPath, appPath, &dataType, &zoneIndex, &dataSize, FALSE, nullptr, 0);
            if (res == ERROR_SUCCESS)
            {
                *iZoneIndex = static_cast<INT>(zoneIndex);
            }
        }
        return res;
    }

    // Pass -1 for the zoneIndex to delete the entry from the registry
    inline void SaveAppLastZone(HWND window, PCWSTR appPath, DWORD zoneIndex)
    {
        LSTATUS res{};
        if (auto monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL))
        {
            wchar_t keyPath[256]{};
            StringCchPrintf(keyPath, ARRAYSIZE(keyPath), L"%s\\%s\\%x", REG_SETTINGS, APP_ZONE_HISTORY_SUBKEY, monitor);
            if (zoneIndex == -1)
            {
                SHDeleteValueW(HKEY_CURRENT_USER, keyPath, appPath);
            }
            else
            {
                SHRegSetUSValueW(keyPath, appPath, REG_DWORD, &zoneIndex, sizeof(zoneIndex), SHREGSET_FORCE_HKCU);
            }
        }
    }

    inline void GetString(PCWSTR uniqueId, PCWSTR setting, PWSTR value, DWORD cbValue)
    {
        wchar_t key[256]{};
        GetKey(uniqueId, key, ARRAYSIZE(key));
        SHRegGetUSValueW(key, setting, nullptr, value, &cbValue, FALSE, nullptr, 0);
    }

    inline void SetString(PCWSTR uniqueId, PCWSTR setting, PCWSTR value)
    {
        wchar_t key[256]{};
        GetKey(uniqueId, key, ARRAYSIZE(key));
        SHRegSetUSValueW(key, setting, REG_SZ, value, sizeof(value) * static_cast<DWORD>(wcslen(value)), SHREGSET_FORCE_HKCU);
    }

    template<typename t>
    inline void GetValue(PCWSTR monitorId, PCWSTR setting, t* value, DWORD size)
    {
        wchar_t key[256]{};
        GetKey(monitorId, key, ARRAYSIZE(key));
        SHRegGetUSValueW(key, setting, nullptr, value, &size, FALSE, nullptr, 0);
    }

    template<typename t>
    inline void SetValue(PCWSTR monitorId, PCWSTR setting, t value, DWORD size)
    {
        wchar_t key[256]{};
        GetKey(monitorId, key, ARRAYSIZE(key));
        SHRegSetUSValueW(key, setting, REG_BINARY, &value, size, SHREGSET_FORCE_HKCU);
    }

    inline void DeleteZoneSet(PCWSTR monitorId, GUID guid)
    {
        wil::unique_cotaskmem_string zoneSetId;
        if (SUCCEEDED_LOG(StringFromCLSID(guid, &zoneSetId)))
        {
            wchar_t key[256]{};
            GetKey(monitorId, key, ARRAYSIZE(key));
            SHDeleteValueW(HKEY_CURRENT_USER, key, zoneSetId.get());
        }
    }

    inline void DeleteAllZoneSets(PCWSTR monitorId)
    {
        wchar_t key[256]{};
        GetKey(monitorId, key, ARRAYSIZE(key));
        SHDeleteKey(HKEY_CURRENT_USER, key);
    }

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