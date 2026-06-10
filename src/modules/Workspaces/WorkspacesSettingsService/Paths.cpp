// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "Paths.h"

#include <windows.h>
#include <sddl.h>
#include <shlobj.h>
#include <pathcch.h>
#include <aclapi.h>
#include <memory>

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

    namespace
    {
        bool IsAdminClassPrincipal(PSID sid)
        {
            // Build a small set of well-known principals that are allowed to
            // write to an install folder we still consider hardened.
            const WELL_KNOWN_SID_TYPE wellKnown[] = {
                WinLocalSystemSid,
                WinBuiltinAdministratorsSid,
            };
            for (auto wk : wellKnown)
            {
                BYTE buf[SECURITY_MAX_SID_SIZE];
                DWORD cb = sizeof(buf);
                if (CreateWellKnownSid(wk, nullptr, buf, &cb) &&
                    EqualSid(sid, reinterpret_cast<PSID>(buf)))
                {
                    return true;
                }
            }

            // NT SERVICE\TrustedInstaller — no WELL_KNOWN_SID_TYPE constant,
            // but the SID is stable.
            PSID tiSid = nullptr;
            if (ConvertStringSidToSidW(
                    L"S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464",
                    &tiSid))
            {
                bool match = EqualSid(sid, tiSid) != 0;
                LocalFree(tiSid);
                if (match) return true;
            }
            return false;
        }
    }

    bool IsFolderAdminOnlyWritable(const std::wstring& folder)
    {
        if (folder.empty())
        {
            return false;
        }

        // Rights that let an attacker influence what's inside the folder
        // (drop a fake exe, swap an existing one, change the DACL itself).
        constexpr DWORD kDangerousRights =
            FILE_ADD_FILE | FILE_ADD_SUBDIRECTORY |
            FILE_WRITE_DATA | FILE_APPEND_DATA |
            FILE_DELETE_CHILD | DELETE |
            WRITE_DAC | WRITE_OWNER |
            GENERIC_WRITE | GENERIC_ALL;

        PACL dacl = nullptr;
        PSECURITY_DESCRIPTOR sd = nullptr;
        DWORD rc = GetNamedSecurityInfoW(
            folder.c_str(),
            SE_FILE_OBJECT,
            DACL_SECURITY_INFORMATION,
            nullptr,
            nullptr,
            &dacl,
            nullptr,
            &sd);

        if (rc != ERROR_SUCCESS)
        {
            return false;
        }

        // NULL DACL means "allow everyone everything" — definitely not safe.
        if (!dacl)
        {
            if (sd) LocalFree(sd);
            return false;
        }

        bool safe = true;
        for (WORD i = 0; safe && i < dacl->AceCount; ++i)
        {
            PACE_HEADER hdr = nullptr;
            if (!GetAce(dacl, i, reinterpret_cast<LPVOID*>(&hdr)))
            {
                continue;
            }

            // Only positive ACEs matter.  ACCESS_DENIED only narrows
            // permissions further.
            if (hdr->AceType != ACCESS_ALLOWED_ACE_TYPE &&
                hdr->AceType != ACCESS_ALLOWED_OBJECT_ACE_TYPE)
            {
                continue;
            }

            ACCESS_ALLOWED_ACE* ace = reinterpret_cast<ACCESS_ALLOWED_ACE*>(hdr);
            if ((ace->Mask & kDangerousRights) == 0)
            {
                continue;
            }

            PSID sid = reinterpret_cast<PSID>(&ace->SidStart);

            // CREATOR OWNER / CREATOR GROUP only apply when something is
            // created; they don't grant the current trustee anything by
            // themselves, so they're benign here.
            BYTE creatorOwner[SECURITY_MAX_SID_SIZE];
            DWORD cb = sizeof(creatorOwner);
            if (CreateWellKnownSid(WinCreatorOwnerSid, nullptr, creatorOwner, &cb) &&
                EqualSid(sid, reinterpret_cast<PSID>(creatorOwner)))
            {
                continue;
            }

            if (!IsAdminClassPrincipal(sid))
            {
                safe = false;
            }
        }

        LocalFree(sd);
        return safe;
    }
}
