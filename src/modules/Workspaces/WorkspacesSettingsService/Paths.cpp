// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "Paths.h"

#include <windows.h>
#include <sddl.h>
#include <shlobj.h>
#include <pathcch.h>

#pragma comment(lib, "Shell32.lib")
#pragma comment(lib, "Pathcch.lib")
#pragma comment(lib, "Advapi32.lib")

namespace WorkspacesSvc
{
    namespace
    {
        std::wstring GetProgramDataFolder()
        {
            PWSTR path = nullptr;
            if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_ProgramData, 0, nullptr, &path)))
            {
                std::wstring result(path);
                CoTaskMemFree(path);
                return result;
            }
            return L"C:\\ProgramData";
        }
    }

    std::wstring GetWorkspacesRoot()
    {
        return GetProgramDataFolder() + L"\\Microsoft\\PowerToys\\Workspaces";
    }

    std::wstring GetUserWorkspacesFolder(const std::wstring& userSidString)
    {
        return GetWorkspacesRoot() + L"\\" + userSidString;
    }

    std::wstring GetUserWorkspacesFile(const std::wstring& userSidString)
    {
        return GetUserWorkspacesFolder(userSidString) + L"\\workspaces.json";
    }

    std::wstring GetUserLegacyBackupFile(const std::wstring& userSidString)
    {
        return GetUserWorkspacesFolder(userSidString) + L"\\workspaces.json.legacy.bak";
    }

    std::wstring GetPowerToysInstallFolder()
    {
        // The MSI writes InstallFolder under HKLM\SOFTWARE\Classes\PowerToys
        // for per-machine installs.  This is the authoritative location the
        // service uses to validate the caller image path.
        HKEY hKey = nullptr;
        if (RegOpenKeyExW(HKEY_LOCAL_MACHINE,
                          L"SOFTWARE\\Classes\\PowerToys",
                          0,
                          KEY_READ | KEY_WOW64_64KEY,
                          &hKey) != ERROR_SUCCESS)
        {
            return {};
        }

        wchar_t buf[MAX_PATH] = {};
        DWORD cb = sizeof(buf);
        DWORD type = 0;
        LSTATUS rc = RegQueryValueExW(hKey,
                                      L"InstallFolder",
                                      nullptr,
                                      &type,
                                      reinterpret_cast<LPBYTE>(buf),
                                      &cb);
        RegCloseKey(hKey);

        if (rc != ERROR_SUCCESS || type != REG_SZ)
        {
            return {};
        }

        std::wstring result(buf);
        // Strip trailing backslash.
        while (!result.empty() && result.back() == L'\\')
        {
            result.pop_back();
        }
        return result;
    }

    std::wstring SidToString(void* psid)
    {
        LPWSTR str = nullptr;
        if (!ConvertSidToStringSidW(static_cast<PSID>(psid), &str))
        {
            return {};
        }
        std::wstring result(str);
        LocalFree(str);
        return result;
    }
}
