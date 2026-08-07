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

        // Enables a privilege (e.g. SeRestore/SeTakeOwnership) on the current
        // process token so the register path can set the store owner to SYSTEM.
        // The elevated registrar (SYSTEM CA, or elevated admin provisioner) holds
        // these privileges; they are merely disabled by default.
        void EnablePrivilege(const wchar_t* name)
        {
            HANDLE token = nullptr;
            if (!OpenProcessToken(GetCurrentProcess(),
                                  TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &token))
            {
                return;
            }
            LUID luid{};
            if (LookupPrivilegeValueW(nullptr, name, &luid))
            {
                TOKEN_PRIVILEGES tp{};
                tp.PrivilegeCount = 1;
                tp.Privileges[0].Luid = luid;
                tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
                AdjustTokenPrivileges(token, FALSE, &tp, sizeof(tp), nullptr, nullptr);
            }
            CloseHandle(token);
        }

        // Applies the PROTECTED per-user DACL and sets owner = SYSTEM.
        //   serviceAccountName = the virtual account, e.g.
        //   L"NT SERVICE\\PTSettingsSvc_<SID>" (Full Control writer).
        HRESULT ApplyProtectiveDacl(const std::wstring& target,
                                    const std::wstring& userSidString,
                                    const std::wstring& serviceAccountName)
        {
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

            PSID systemSid = nullptr;
            if (!ConvertStringSidToSidW(L"S-1-5-18", &systemSid))
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }
            std::unique_ptr<void, LocalFreeDeleter> systemGuard(systemSid);

            // The per-user folder DACL is:
            //   svc-vaccount:F, admin:F, SYSTEM:F, <specific user>:RX
            // Owner = SYSTEM so the low-privilege virtual account cannot rewrite
            // the DACL.  PROTECTED below blocks inheritance from the store root
            // (its blanket AuthUsers:RX does NOT carry through here — that's how
            // user A can't read user B's data).  The virtual account is named
            // (TRUSTEE_IS_NAME) because it exists only after CreateService.
            std::wstring svcAccount = serviceAccountName;
            EXPLICIT_ACCESS_W ea[4] = {};

            ea[0].grfAccessPermissions = GENERIC_ALL;
            ea[0].grfAccessMode = SET_ACCESS;
            ea[0].grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
            ea[0].Trustee.TrusteeForm = TRUSTEE_IS_NAME;
            ea[0].Trustee.TrusteeType = TRUSTEE_IS_USER;
            ea[0].Trustee.ptstrName = svcAccount.data();

            ea[1].grfAccessPermissions = GENERIC_ALL;
            ea[1].grfAccessMode = SET_ACCESS;
            ea[1].grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
            ea[1].Trustee.TrusteeForm = TRUSTEE_IS_SID;
            ea[1].Trustee.TrusteeType = TRUSTEE_IS_WELL_KNOWN_GROUP;
            ea[1].Trustee.ptstrName = static_cast<LPWSTR>(adminSid);

            ea[2].grfAccessPermissions = GENERIC_ALL;
            ea[2].grfAccessMode = SET_ACCESS;
            ea[2].grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
            ea[2].Trustee.TrusteeForm = TRUSTEE_IS_SID;
            ea[2].Trustee.TrusteeType = TRUSTEE_IS_USER;
            ea[2].Trustee.ptstrName = static_cast<LPWSTR>(systemSid);

            ea[3].grfAccessPermissions = GENERIC_READ | GENERIC_EXECUTE;
            ea[3].grfAccessMode = SET_ACCESS;
            ea[3].grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
            ea[3].Trustee.TrusteeForm = TRUSTEE_IS_SID;
            ea[3].Trustee.TrusteeType = TRUSTEE_IS_USER;
            ea[3].Trustee.ptstrName = static_cast<LPWSTR>(userSid);

            PACL acl = nullptr;
            DWORD rc = SetEntriesInAclW(ARRAYSIZE(ea), ea, nullptr, &acl);
            if (rc != ERROR_SUCCESS)
            {
                return HRESULT_FROM_WIN32(rc);
            }
            std::unique_ptr<void, LocalFreeDeleter> aclGuard(acl);

            EnablePrivilege(SE_RESTORE_NAME);
            EnablePrivilege(SE_TAKE_OWNERSHIP_NAME);

            std::vector<wchar_t> mutableName(target.begin(), target.end());
            mutableName.push_back(L'\0');
            rc = SetNamedSecurityInfoW(mutableName.data(),
                                       SE_FILE_OBJECT,
                                       OWNER_SECURITY_INFORMATION |
                                           DACL_SECURITY_INFORMATION |
                                           PROTECTED_DACL_SECURITY_INFORMATION,
                                       systemSid, nullptr, acl, nullptr);
            return rc == ERROR_SUCCESS ? S_OK : HRESULT_FROM_WIN32(rc);
        }
    }

    HRESULT EnsureStoreRoot(const std::wstring& root)
    {
        if (!CreateDirectoryW(root.c_str(), nullptr))
        {
            DWORD err = GetLastError();
            if (err != ERROR_ALREADY_EXISTS)
            {
                return HRESULT_FROM_WIN32(err);
            }
        }

        PSID adminSid = nullptr;
        if (!ConvertStringSidToSidW(L"S-1-5-32-544", &adminSid))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        std::unique_ptr<void, LocalFreeDeleter> adminGuard(adminSid);

        PSID systemSid = nullptr;
        if (!ConvertStringSidToSidW(L"S-1-5-18", &systemSid))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        std::unique_ptr<void, LocalFreeDeleter> systemGuard(systemSid);

        PSID authUsersSid = nullptr;
        if (!ConvertStringSidToSidW(L"S-1-5-11", &authUsersSid)) // Authenticated Users
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        std::unique_ptr<void, LocalFreeDeleter> authUsersGuard(authUsersSid);

        // Root: SYSTEM/Admins Full, Authenticated Users RX (traverse only).  Not
        // protected — each <sid> node below protects itself; the blanket RX here
        // lets every user reach their own node but the protected child DACL
        // stops A reading B.
        EXPLICIT_ACCESS_W ea[3] = {};
        ea[0].grfAccessPermissions = GENERIC_ALL;
        ea[0].grfAccessMode = SET_ACCESS;
        ea[0].grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
        ea[0].Trustee.TrusteeForm = TRUSTEE_IS_SID;
        ea[0].Trustee.TrusteeType = TRUSTEE_IS_USER;
        ea[0].Trustee.ptstrName = static_cast<LPWSTR>(systemSid);

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
        ea[2].Trustee.TrusteeType = TRUSTEE_IS_WELL_KNOWN_GROUP;
        ea[2].Trustee.ptstrName = static_cast<LPWSTR>(authUsersSid);

        PACL acl = nullptr;
        DWORD rc = SetEntriesInAclW(ARRAYSIZE(ea), ea, nullptr, &acl);
        if (rc != ERROR_SUCCESS)
        {
            return HRESULT_FROM_WIN32(rc);
        }
        std::unique_ptr<void, LocalFreeDeleter> aclGuard(acl);

        std::vector<wchar_t> mutableName(root.begin(), root.end());
        mutableName.push_back(L'\0');
        rc = SetNamedSecurityInfoW(mutableName.data(),
                                   SE_FILE_OBJECT,
                                   DACL_SECURITY_INFORMATION,
                                   nullptr, nullptr, acl, nullptr);
        return rc == ERROR_SUCCESS ? S_OK : HRESULT_FROM_WIN32(rc);
    }

    HRESULT HardenStagingDirAdminOnly(const std::wstring& dir)
    {
        if (!CreateDirectoryW(dir.c_str(), nullptr))
        {
            DWORD err = GetLastError();
            if (err != ERROR_ALREADY_EXISTS)
            {
                return HRESULT_FROM_WIN32(err);
            }
        }

        PSID adminSid = nullptr;
        if (!ConvertStringSidToSidW(L"S-1-5-32-544", &adminSid)) // BUILTIN\Administrators
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        std::unique_ptr<void, LocalFreeDeleter> adminGuard(adminSid);

        PSID systemSid = nullptr;
        if (!ConvertStringSidToSidW(L"S-1-5-18", &systemSid))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        std::unique_ptr<void, LocalFreeDeleter> systemGuard(systemSid);

        // SYSTEM Full + Administrators Full ONLY.  No Authenticated-Users ACE and
        // — combined with PROTECTED below — no inherited %ProgramData% ACEs, so a
        // non-admin who pre-created this dir keeps nothing.  Owner is reset to
        // SYSTEM so a non-admin creator's CREATOR-OWNER rights are reclaimed.
        // The virtual account / Users RX ACEs are added later by
        // ProtectServiceBinDir, once the service (hence the account) exists.
        EXPLICIT_ACCESS_W ea[2] = {};
        ea[0].grfAccessPermissions = GENERIC_ALL;
        ea[0].grfAccessMode = SET_ACCESS;
        ea[0].grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
        ea[0].Trustee.TrusteeForm = TRUSTEE_IS_SID;
        ea[0].Trustee.TrusteeType = TRUSTEE_IS_USER;
        ea[0].Trustee.ptstrName = static_cast<LPWSTR>(systemSid);

        ea[1].grfAccessPermissions = GENERIC_ALL;
        ea[1].grfAccessMode = SET_ACCESS;
        ea[1].grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
        ea[1].Trustee.TrusteeForm = TRUSTEE_IS_SID;
        ea[1].Trustee.TrusteeType = TRUSTEE_IS_WELL_KNOWN_GROUP;
        ea[1].Trustee.ptstrName = static_cast<LPWSTR>(adminSid);

        PACL acl = nullptr;
        DWORD rc = SetEntriesInAclW(ARRAYSIZE(ea), ea, nullptr, &acl);
        if (rc != ERROR_SUCCESS)
        {
            return HRESULT_FROM_WIN32(rc);
        }
        std::unique_ptr<void, LocalFreeDeleter> aclGuard(acl);

        EnablePrivilege(SE_RESTORE_NAME);
        EnablePrivilege(SE_TAKE_OWNERSHIP_NAME);

        std::vector<wchar_t> mutableName(dir.begin(), dir.end());
        mutableName.push_back(L'\0');
        rc = SetNamedSecurityInfoW(mutableName.data(),
                                   SE_FILE_OBJECT,
                                   OWNER_SECURITY_INFORMATION |
                                       DACL_SECURITY_INFORMATION |
                                       PROTECTED_DACL_SECURITY_INFORMATION,
                                   systemSid, nullptr, acl, nullptr);
        return rc == ERROR_SUCCESS ? S_OK : HRESULT_FROM_WIN32(rc);
    }

    HRESULT EnsureUserFolder(const std::wstring& folder,
                             const std::wstring& userSidString,
                             const std::wstring& serviceAccountName)
    {
        if (!CreateDirectoryW(folder.c_str(), nullptr))
        {
            DWORD err = GetLastError();
            if (err != ERROR_ALREADY_EXISTS)
            {
                return HRESULT_FROM_WIN32(err);
            }
        }
        return ApplyProtectiveDacl(folder, userSidString, serviceAccountName);
    }

    HRESULT ProvisionStore(const std::wstring& root,
                           const std::wstring& userFolder,
                           const std::wstring& userSidString,
                           const std::wstring& serviceAccountName)
    {
        HRESULT hr = EnsureStoreRoot(root);
        if (FAILED(hr))
        {
            return hr;
        }
        return EnsureUserFolder(userFolder, userSidString, serviceAccountName);
    }

    HRESULT ProtectServiceBinDir(const std::wstring& binDir,
                                 const std::wstring& serviceAccountName)
    {
        PSID adminSid = nullptr;
        if (!ConvertStringSidToSidW(L"S-1-5-32-544", &adminSid))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        std::unique_ptr<void, LocalFreeDeleter> adminGuard(adminSid);

        PSID systemSid = nullptr;
        if (!ConvertStringSidToSidW(L"S-1-5-18", &systemSid))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        std::unique_ptr<void, LocalFreeDeleter> systemGuard(systemSid);

        PSID usersSid = nullptr;
        if (!ConvertStringSidToSidW(L"S-1-5-32-545", &usersSid)) // BUILTIN\Users
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        std::unique_ptr<void, LocalFreeDeleter> usersGuard(usersSid);

        std::wstring svcAccount = serviceAccountName;
        EXPLICIT_ACCESS_W ea[4] = {};

        ea[0].grfAccessPermissions = GENERIC_ALL;
        ea[0].grfAccessMode = SET_ACCESS;
        ea[0].grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
        ea[0].Trustee.TrusteeForm = TRUSTEE_IS_SID;
        ea[0].Trustee.TrusteeType = TRUSTEE_IS_USER;
        ea[0].Trustee.ptstrName = static_cast<LPWSTR>(systemSid);

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
        ea[2].Trustee.TrusteeType = TRUSTEE_IS_WELL_KNOWN_GROUP;
        ea[2].Trustee.ptstrName = static_cast<LPWSTR>(usersSid);

        ea[3].grfAccessPermissions = GENERIC_READ | GENERIC_EXECUTE;
        ea[3].grfAccessMode = SET_ACCESS;
        ea[3].grfInheritance = SUB_CONTAINERS_AND_OBJECTS_INHERIT;
        ea[3].Trustee.TrusteeForm = TRUSTEE_IS_NAME;
        ea[3].Trustee.TrusteeType = TRUSTEE_IS_USER;
        ea[3].Trustee.ptstrName = svcAccount.data();

        PACL acl = nullptr;
        DWORD rc = SetEntriesInAclW(ARRAYSIZE(ea), ea, nullptr, &acl);
        if (rc != ERROR_SUCCESS)
        {
            return HRESULT_FROM_WIN32(rc);
        }
        std::unique_ptr<void, LocalFreeDeleter> aclGuard(acl);

        EnablePrivilege(SE_RESTORE_NAME);
        EnablePrivilege(SE_TAKE_OWNERSHIP_NAME);

        std::vector<wchar_t> mutableName(binDir.begin(), binDir.end());
        mutableName.push_back(L'\0');
        rc = SetNamedSecurityInfoW(mutableName.data(),
                                   SE_FILE_OBJECT,
                                   OWNER_SECURITY_INFORMATION |
                                       DACL_SECURITY_INFORMATION |
                                       PROTECTED_DACL_SECURITY_INFORMATION,
                                   systemSid, nullptr, acl, nullptr);
        return rc == ERROR_SUCCESS ? S_OK : HRESULT_FROM_WIN32(rc);
    }

    HRESULT EnsureDirectory(const std::wstring& dir)
    {
        if (!CreateDirectoryW(dir.c_str(), nullptr))
        {
            DWORD err = GetLastError();
            if (err != ERROR_ALREADY_EXISTS)
            {
                return HRESULT_FROM_WIN32(err);
            }
        }
        return S_OK;
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
