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

namespace PTSettingsSvc
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

    std::wstring GetSettingsRoot()
    {
        return GetProgramDataFolder() + L"\\Microsoft\\PowerToys\\Settings";
    }

    std::wstring GetServiceBinRoot()
    {
        return GetProgramDataFolder() + L"\\Microsoft\\PowerToys\\SettingsSvcBin";
    }

    std::wstring GetUserFolder(const std::wstring& userSidString)
    {
        return GetSettingsRoot() + L"\\" + userSidString;
    }

    std::wstring GetUserNamespaceFolder(const std::wstring& userSidString,
                                        const std::wstring& namespaceId)
    {
        return GetUserFolder(userSidString) + L"\\" + namespaceId;
    }

    std::wstring GetUserFilePath(const std::wstring& userSidString,
                                 const std::wstring& namespaceId,
                                 const std::wstring& fileName)
    {
        return GetUserNamespaceFolder(userSidString, namespaceId) + L"\\" + fileName;
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
