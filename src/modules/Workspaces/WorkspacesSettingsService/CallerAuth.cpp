// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "CallerAuth.h"
#include "Paths.h"

#include <windows.h>
#include <sddl.h>
#include <pathcch.h>
#include <vector>
#include <algorithm>

#pragma comment(lib, "Advapi32.lib")
#pragma comment(lib, "Pathcch.lib")

namespace WorkspacesSvc
{
    namespace
    {
        // Allow-list of PowerToys binaries that may issue writes.  Everything
        // not on this list is rejected even if it lives under the PT install
        // folder.  Kept narrow on purpose.
        const wchar_t* const kAllowedCallerExeNames[] = {
            L"PowerToys.WorkspacesEditor.exe",
            L"PowerToys.WorkspacesSnapshotTool.exe",
            L"PowerToys.exe",            // runner — needed for the one-shot
                                         // MigrateFromLegacy call at startup.
        };

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
            // GetFinalPathNameByHandle prefixes with \\?\ — strip it for nicer
            // comparison, but only when it's the literal prefix.
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
            // Case-insensitive prefix match — Win32 paths are case-insensitive.
            return _wcsnicmp(file.c_str(), d.c_str(), d.size()) == 0;
        }

        std::wstring BaseName(const std::wstring& path)
        {
            auto pos = path.find_last_of(L"\\/");
            return pos == std::wstring::npos ? path : path.substr(pos + 1);
        }

        bool IsAllowedExeName(const std::wstring& name)
        {
            for (auto allowed : kAllowedCallerExeNames)
            {
                if (_wcsicmp(name.c_str(), allowed) == 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    HRESULT AuthenticateCaller(HANDLE pipeHandle, CallerIdentity& outIdentity)
    {
        outIdentity = {};

        // 1) Impersonate the client to get a token we can introspect, then
        //    immediately revert.  We deliberately don't do any privileged
        //    work while impersonating.
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
        RevertToSelf();

        if (!gotToken)
        {
            return HRESULT_FROM_WIN32(tokenErr);
        }

        HRESULT hr = RejectionForToken(clientToken, outIdentity.userSidString);
        CloseHandle(clientToken);
        if (FAILED(hr))
        {
            return hr;
        }

        // 2) Resolve the client's image path via PID.  Hold a process handle
        //    across the validation to keep the PID stable and mitigate the
        //    classic PID-reuse TOCTOU window.
        ULONG pid = 0;
        if (!GetNamedPipeClientProcessId(pipeHandle, &pid))
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        outIdentity.processId = pid;

        HANDLE hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
        if (!hProc)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        wchar_t exePath[MAX_PATH * 2] = {};
        DWORD cch = ARRAYSIZE(exePath);
        BOOL gotImage = QueryFullProcessImageNameW(hProc, 0, exePath, &cch);
        DWORD imageErr = gotImage ? ERROR_SUCCESS : GetLastError();
        CloseHandle(hProc);

        if (!gotImage)
        {
            return HRESULT_FROM_WIN32(imageErr);
        }

        std::wstring canonical = CanonicalizePath(exePath);
        outIdentity.imagePath = canonical;

        // 3) Path + allow-list check.
        std::wstring installFolder = GetPowerToysInstallFolder();
        // Prototype dev override — lets the smoke test demonstrate the
        // happy path without requiring an MSI install + HKLM write.
        // Removed before production: real builds rely on the MSI-written
        // HKLM\SOFTWARE\Classes\PowerToys\InstallFolder value.
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
        if (!IsAllowedExeName(BaseName(canonical)))
        {
            return E_ACCESSDENIED;
        }

        return S_OK;
    }
}
