// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "FileGuard.h"

#include <windows.h>
#include <sddl.h>
#include <aclapi.h>
#include <pathcch.h>
#include <memory>
#include <vector>

#pragma comment(lib, "Advapi32.lib")
#pragma comment(lib, "Pathcch.lib")

namespace PTSettingsSvc
{
    namespace
    {
        struct LocalFreeDeleter
        {
            void operator()(void* p) const noexcept { if (p) LocalFree(p); }
        };

        HRESULT GetServiceSid(PSID& outSid)
        {
            // "NT SERVICE\PTSettingsSvc" virtual account.  Resolved via
            // LookupAccountName since we have a name, not a SID.
            wchar_t name[] = L"NT SERVICE\\PTSettingsSvc";
            DWORD sidLen = 0;
            DWORD domLen = 0;
            SID_NAME_USE use{};
            LookupAccountNameW(nullptr, name, nullptr, &sidLen, nullptr, &domLen, &use);
            if (sidLen == 0)
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            outSid = static_cast<PSID>(LocalAlloc(LMEM_FIXED, sidLen));
            if (!outSid)
            {
                return E_OUTOFMEMORY;
            }
            std::vector<wchar_t> domain(static_cast<size_t>(domLen) + 1);
            if (!LookupAccountNameW(nullptr, name, outSid, &sidLen, domain.data(), &domLen, &use))
            {
                LocalFree(outSid);
                outSid = nullptr;
                return HRESULT_FROM_WIN32(GetLastError());
            }
            return S_OK;
        }

        HRESULT ApplyProtectiveDacl(const std::wstring& target,
                                    const std::wstring& userSidString)
        {
            PSID serviceSid = nullptr;
            HRESULT hr = GetServiceSid(serviceSid);
            if (FAILED(hr))
            {
                return hr;
            }
            std::unique_ptr<void, LocalFreeDeleter> serviceSidGuard(serviceSid);

            PSID userSid = nullptr;
            if (!ConvertStringSidToSidW(userSidString.c_str(), &userSid))
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            std::unique_ptr<void, LocalFreeDeleter> userSidGuard(userSid);

            PSID adminSid = nullptr;
            if (!ConvertStringSidToSidW(L"S-1-5-32-544", &adminSid)) // BUILTIN\Administrators
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            std::unique_ptr<void, LocalFreeDeleter> adminSidGuard(adminSid);

            // Per Design-v6-Final.md §9 the per-user folder DACL is:
            //   svc:F, admin:F, <specific user>:RX
            // Everyone else implicitly denied because we PROTECT the DACL
            // below (no inheritance from <storeRoot>\, so the blanket
            // AuthUsers:RX granted at the store root does NOT carry through
            // here — that's how user A can't read user B's data).  Applied at
            // the per-user <sid> node, it inherits down to the namespace folder
            // and the file.
            EXPLICIT_ACCESS_W ea[3] = {};

            ea[0].grfAccessPermissions = GENERIC_ALL;
            ea[0].grfAccessMode = SET_ACCESS;
            ea[0].grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
            ea[0].Trustee.TrusteeForm = TRUSTEE_IS_SID;
            ea[0].Trustee.TrusteeType = TRUSTEE_IS_USER;
            ea[0].Trustee.ptstrName = static_cast<LPWSTR>(serviceSid);

            ea[1].grfAccessPermissions = GENERIC_ALL;
            ea[1].grfAccessMode = SET_ACCESS;
            ea[1].grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
            ea[1].Trustee.TrusteeForm = TRUSTEE_IS_SID;
            ea[1].Trustee.TrusteeType = TRUSTEE_IS_WELL_KNOWN_GROUP;
            ea[1].Trustee.ptstrName = static_cast<LPWSTR>(adminSid);

            ea[2].grfAccessPermissions = GENERIC_READ | GENERIC_EXECUTE;
            ea[2].grfAccessMode = SET_ACCESS;
            ea[2].grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
            ea[2].Trustee.TrusteeForm = TRUSTEE_IS_SID;
            ea[2].Trustee.TrusteeType = TRUSTEE_IS_USER;
            ea[2].Trustee.ptstrName = static_cast<LPWSTR>(userSid);

            PACL acl = nullptr;
            DWORD rc = SetEntriesInAclW(ARRAYSIZE(ea), ea, nullptr, &acl);
            if (rc != ERROR_SUCCESS)
            {
                return HRESULT_FROM_WIN32(rc);
            }
            std::unique_ptr<void, LocalFreeDeleter> aclGuard(acl);

            // PROTECTED_DACL_SECURITY_INFORMATION blocks inheritance from
            // <root>\<namespace>\.  SetNamedSecurityInfoW takes a non-const
            // LPWSTR by historical signature; copy into a local mutable buffer.
            std::vector<wchar_t> mutableName(target.begin(), target.end());
            mutableName.push_back(L'\0');
            rc = SetNamedSecurityInfoW(mutableName.data(),
                                       SE_FILE_OBJECT,
                                       DACL_SECURITY_INFORMATION | PROTECTED_DACL_SECURITY_INFORMATION,
                                       nullptr, nullptr, acl, nullptr);
            return rc == ERROR_SUCCESS ? S_OK : HRESULT_FROM_WIN32(rc);
        }
    }

    HRESULT EnsureUserFolder(const std::wstring& folder,
                             const std::wstring& userSidString)
    {
        if (!CreateDirectoryW(folder.c_str(), nullptr))
        {
            DWORD err = GetLastError();
            if (err != ERROR_ALREADY_EXISTS)
            {
                return HRESULT_FROM_WIN32(err);
            }
        }
        return ApplyProtectiveDacl(folder, userSidString);
    }

    HRESULT WriteFileAtomically(const std::wstring& targetFile,
                                const std::vector<BYTE>& bytes)
    {
        std::wstring tmp = targetFile + L".tmp";

        HANDLE h = CreateFileW(tmp.c_str(),
                               GENERIC_WRITE,
                               0,
                               nullptr,
                               CREATE_ALWAYS,
                               FILE_ATTRIBUTE_NORMAL,
                               nullptr);
        if (h == INVALID_HANDLE_VALUE)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        DWORD written = 0;
        BOOL ok = WriteFile(h,
                            bytes.data(),
                            static_cast<DWORD>(bytes.size()),
                            &written,
                            nullptr);
        DWORD writeErr = ok ? ERROR_SUCCESS : GetLastError();
        FlushFileBuffers(h);
        CloseHandle(h);

        if (!ok || written != bytes.size())
        {
            DeleteFileW(tmp.c_str());
            return HRESULT_FROM_WIN32(writeErr ? writeErr : ERROR_WRITE_FAULT);
        }

        if (!ReplaceFileW(targetFile.c_str(),
                          tmp.c_str(),
                          nullptr,
                          REPLACEFILE_WRITE_THROUGH | REPLACEFILE_IGNORE_MERGE_ERRORS,
                          nullptr,
                          nullptr))
        {
            DWORD err = GetLastError();
            if (err == ERROR_FILE_NOT_FOUND)
            {
                // No existing file — MoveFile is sufficient.
                if (!MoveFileExW(tmp.c_str(),
                                 targetFile.c_str(),
                                 MOVEFILE_WRITE_THROUGH))
                {
                    DWORD mvErr = GetLastError();
                    DeleteFileW(tmp.c_str());
                    return HRESULT_FROM_WIN32(mvErr);
                }
            }
            else
            {
                DeleteFileW(tmp.c_str());
                return HRESULT_FROM_WIN32(err);
            }
        }

        return S_OK;
    }

    HRESULT ReadFileFully(const std::wstring& path,
                          uint32_t maxBytes,
                          std::vector<BYTE>& outBytes)
    {
        outBytes.clear();

        HANDLE h = CreateFileW(path.c_str(),
                               GENERIC_READ,
                               FILE_SHARE_READ,
                               nullptr,
                               OPEN_EXISTING,
                               FILE_ATTRIBUTE_NORMAL,
                               nullptr);
        if (h == INVALID_HANDLE_VALUE)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        LARGE_INTEGER size{};
        if (!GetFileSizeEx(h, &size))
        {
            DWORD err = GetLastError();
            CloseHandle(h);
            return HRESULT_FROM_WIN32(err);
        }
        if (size.QuadPart > static_cast<LONGLONG>(maxBytes))
        {
            CloseHandle(h);
            return HRESULT_FROM_WIN32(ERROR_FILE_TOO_LARGE);
        }

        outBytes.resize(static_cast<size_t>(size.QuadPart));
        DWORD read = 0;
        BOOL ok = ReadFile(h,
                           outBytes.data(),
                           static_cast<DWORD>(outBytes.size()),
                           &read,
                           nullptr);
        DWORD err = ok ? ERROR_SUCCESS : GetLastError();
        CloseHandle(h);

        if (!ok || read != outBytes.size())
        {
            outBytes.clear();
            return HRESULT_FROM_WIN32(err ? err : ERROR_READ_FAULT);
        }
        return S_OK;
    }
}
