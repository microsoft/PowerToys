// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "CallerAuth.h"
#include "Bindings.h"
#include "Paths.h"

#include <windows.h>
#include <sddl.h>
#include <pathcch.h>
#include <vector>
#include <algorithm>

#pragma comment(lib, "Advapi32.lib")
#pragma comment(lib, "Pathcch.lib")

namespace PTSettingsSvc
{
    namespace
    {
        HRESULT RejectionForToken(HANDLE token, std::wstring& outSidString)
        {
            DWORD size = 0;
            GetTokenInformation(token, TokenUser, nullptr, 0, &size);
            if (size == 0)
            {
                return E_FAIL;
            }

            std::vector<BYTE> buf(size);
            if (!GetTokenInformation(token, TokenUser, buf.data(), size, &size))
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }

            PSID sid = reinterpret_cast<TOKEN_USER*>(buf.data())->User.Sid;

            // Reject well-known synthetic principals — we want a real
            // interactive user so the data folder is scoped to a human.
            const WELL_KNOWN_SID_TYPE rejected[] = {
                WinLocalSystemSid,
                WinLocalServiceSid,
                WinNetworkServiceSid,
                WinAnonymousSid,
                WinNullSid,
            };
            for (auto wk : rejected)
            {
                if (IsWellKnownSid(sid, wk))
                {
                    return E_ACCESSDENIED;
                }
            }

            outSidString = SidToString(sid);
            if (outSidString.empty())
            {
                return E_FAIL;
            }
            return S_OK;
        }

        std::wstring CanonicalizePath(const std::wstring& path)
        {
            // Open with backup-semantics so we can canonicalize even
            // executables that the loader has already mapped.
            HANDLE h = CreateFileW(path.c_str(),
                                   READ_CONTROL,
                                   FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                                   nullptr,
                                   OPEN_EXISTING,
                                   FILE_FLAG_BACKUP_SEMANTICS,
                                   nullptr);
            if (h == INVALID_HANDLE_VALUE)
            {
                return path;
            }
            wchar_t buf[1024] = {};
            DWORD len = GetFinalPathNameByHandleW(h, buf, ARRAYSIZE(buf), FILE_NAME_NORMALIZED);
            CloseHandle(h);
            if (len == 0 || len >= ARRAYSIZE(buf))
            {
                return path;
            }
            std::wstring result(buf);
            if (result.compare(0, 4, L"\\\\?\\") == 0)
            {
                result.erase(0, 4);
            }
            return result;
        }

        bool IsUnderDir(const std::wstring& file, const std::wstring& dir)
        {
            if (dir.empty())
            {
                return false;
            }
            std::wstring d = dir;
            if (d.back() != L'\\')
            {
                d.push_back(L'\\');
            }
            if (file.size() < d.size())
            {
                return false;
            }
            return _wcsnicmp(file.c_str(), d.c_str(), d.size()) == 0;
        }

        std::wstring BaseName(const std::wstring& path)
        {
            auto pos = path.find_last_of(L"\\/");
            return pos == std::wstring::npos ? path : path.substr(pos + 1);
        }
    }

    HRESULT AuthenticateCaller(HANDLE pipeHandle, CallerIdentity& outIdentity)
    {
        outIdentity = {};

        // 1) Capture client pid up front (cheap, doesn't need impersonation).
        ULONG pid = 0;
        if (!GetNamedPipeClientProcessId(pipeHandle, &pid))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        outIdentity.processId = pid;

        // 2) Impersonate the client.  We need the caller's token to (a) read
        //    its SID and (b) open a handle to its own process.  The service
        //    runs as NT SERVICE\<vacct>, which is NOT a member of
        //    Authenticated Users and so cannot satisfy the default process
        //    DACL when calling OpenProcess across user boundaries.  Doing
        //    the OpenProcess while impersonating means the DACL check is
        //    against the user's own token, which naturally grants access
        //    to its own processes.
        if (!ImpersonateNamedPipeClient(pipeHandle))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        HANDLE clientToken = nullptr;
        BOOL gotToken = OpenThreadToken(GetCurrentThread(),
                                        TOKEN_QUERY,
                                        TRUE,
                                        &clientToken);
        DWORD tokenErr = gotToken ? ERROR_SUCCESS : GetLastError();

        if (!gotToken)
        {
            RevertToSelf();
            return HRESULT_FROM_WIN32(tokenErr);
        }

        HRESULT hr = RejectionForToken(clientToken, outIdentity.userSidString);
        CloseHandle(clientToken);
        if (FAILED(hr))
        {
            RevertToSelf();
            return hr;
        }

        // 3) While still impersonating: open the client process and read its
        //    image path.  Hold the handle for the rest of validation so the
        //    PID can't be reused under us.
        HANDLE hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
        DWORD openErr = hProc ? ERROR_SUCCESS : GetLastError();

        wchar_t exePath[MAX_PATH * 2] = {};
        DWORD cch = ARRAYSIZE(exePath);
        BOOL gotImage = FALSE;
        DWORD imageErr = ERROR_SUCCESS;
        if (hProc)
        {
            gotImage = QueryFullProcessImageNameW(hProc, 0, exePath, &cch);
            imageErr = gotImage ? ERROR_SUCCESS : GetLastError();
        }

        // Revert before we touch any service-side resources (file IO etc).
        RevertToSelf();

        if (!hProc)
        {
            return HRESULT_FROM_WIN32(openErr);
        }
        CloseHandle(hProc);

        if (!gotImage)
        {
            return HRESULT_FROM_WIN32(imageErr);
        }

        std::wstring canonical = CanonicalizePath(exePath);
        outIdentity.imagePath = canonical;

        // 4) Path-under-InstallFolder check.
        std::wstring installFolder = GetPowerToysInstallFolder();
        // Prototype dev override — lets the smoke test demonstrate the
        // happy path without requiring a real MSI install + HKLM write.
        // Production builds rely on the MSI-written
        // HKLM\SOFTWARE\Classes\PowerToys\InstallFolder value.  This
        // override should be removed (or #ifdef _DEBUG'd) before merge.
        if (installFolder.empty())
        {
            wchar_t dev[MAX_PATH] = {};
            if (GetEnvironmentVariableW(L"PT_DEV_INSTALL_FOLDER", dev, ARRAYSIZE(dev)) > 0)
            {
                installFolder = dev;
            }
        }
        if (installFolder.empty() || !IsUnderDir(canonical, installFolder))
        {
            return E_ACCESSDENIED;
        }

        // 5) Install-folder DACL-hardness check.  Rejects custom MSI install
        //    paths under a user-writable parent (where same-user malware
        //    could otherwise drop an allow-listed exe name and pass the
        //    path+name check).  See Design-v6-Final.md §8.
        if (!IsFolderAdminOnlyWritable(installFolder))
        {
            return E_ACCESSDENIED;
        }

        // 6) Caller binding lookup (basename allow-list + namespace selection).
        std::wstring basename = BaseName(canonical);
        const CallerBinding* binding = FindBindingByExeBasename(basename);
        if (!binding)
        {
            return E_ACCESSDENIED;
        }

        // Defensive: the table should always carry a well-formed namespace id;
        // verify before we hand it to the storage layer to use as a directory
        // name.  Failure here is a build-time misconfiguration of Bindings.cpp.
        if (!IsValidNamespaceId(binding->namespaceId))
        {
            return HRESULT_FROM_WIN32(ERROR_NOT_FOUND);
        }

        outIdentity.binding = binding;
        return S_OK;
    }
}
