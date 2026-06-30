// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "CallerAuth.h"
#include "Bindings.h"
#include "Paths.h"
#include "CallerVerify.h"

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

        // The caller binary often lives under %LocalAppData% (per-user install),
        // which is ACL'd to the user only.  The service account cannot read it,
        // so canonicalization and the signature/version checks MUST run while we
        // are still impersonating the client (which can read its own image).
        std::wstring canonical;
        bool sigMicrosoft = false;
        unsigned long long callerVersion = 0;
        if (gotImage)
        {
            canonical = CanonicalizePath(exePath);
            sigMicrosoft = VerifyMicrosoftSignature(canonical);
            callerVersion = GetBinaryVersion(canonical);
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

        outIdentity.imagePath = canonical;

        // 4) Caller-image trust anchor (UNIFIED — Design §7, updated 2026-06-30).
        //    EVERY caller, per-machine and per-user alike, must be Microsoft-
        //    signed AND its file version must equal the service's own version.
        //
        //    Why the former per-machine "path-only" branch was dropped:
        //      * Path-trust accepted any image under the admin-only install
        //        folder regardless of version, so a different admin-installed
        //        editor version would pass — inconsistent with the version-match
        //        requirement.
        //      * The signature is verified by this LocalSystem service against
        //        the MACHINE trust store (CallerVerify.cpp), so it is NOT
        //        forgeable by a non-admin user-store root (this defeats the §13
        //        per-user TrustedPeople objection that argued path > signature).
        //      * Version-pinning supplies the "freshness" that path-trust gave;
        //        binary immutability is already guaranteed by deployment
        //        (WindowsApps for the service, %ProgramFiles% for per-machine
        //        callers), so it need not be re-proven during authentication.
        //
        //    sigMicrosoft and callerVersion were captured above under
        //    impersonation so a user-profile image is readable.
        const unsigned long long serviceVersion = GetServiceOwnVersion();
        bool sigOk = sigMicrosoft;
#ifdef _DEBUG
        // DEV-ONLY (compiled out of Release): local/smoke-test builds are not
        // Microsoft-signed, so allow skipping ONLY the signature predicate when
        // PT_DEV_SKIP_SIGCHECK is set.  The version-match check still applies,
        // so the unified anchor's logic is exercised with unsigned dev binaries.
        // MUST NOT ship — Release always requires a real Microsoft signature.
        if (!sigOk)
        {
            wchar_t dev[8] = {};
            if (GetEnvironmentVariableW(L"PT_DEV_SKIP_SIGCHECK", dev, ARRAYSIZE(dev)) > 0)
            {
                sigOk = true;
            }
        }
#endif
        const bool accepted =
            serviceVersion != 0 &&
            sigOk &&
            callerVersion == serviceVersion;

        if (!accepted)
        {
            return E_ACCESSDENIED;
        }

        // 5) Caller binding lookup (basename allow-list + namespace selection).
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

