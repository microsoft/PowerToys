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
        std::wstring g_serviceOwnerSid;
    }

    void SetServiceOwnerSid(const std::wstring& sidString)
    {
        g_serviceOwnerSid = sidString;
    }

    std::wstring GetServiceOwnerSid()
    {
        return g_serviceOwnerSid;
    }

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

        // 2b) Owner-SID binding.  This per-user
        //     service instance serves exactly ONE user — the SID it was
        //     registered for.  Reject any caller whose token user SID differs,
        //     so a caller that reached us via a forged pipe name (running as a
        //     different user) is denied here even if the pipe DACL let it
        //     connect.  Empty owner SID => enforcement disabled (dev/standalone).
        const std::wstring ownerSid = GetServiceOwnerSid();
        if (!ownerSid.empty() &&
            _wcsicmp(outIdentity.userSidString.c_str(), ownerSid.c_str()) != 0)
        {
            RevertToSelf();
            return E_ACCESSDENIED;
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
        // so canonicalization, the (attacker-writable, non-security) version
        // read, and OPENING the image handle run while we are still impersonating
        // the client.  The Authenticode TRUST decision, however, must NOT run
        // under impersonation: WinVerifyTrust would build the chain against the
        // caller's CurrentUser\Root, so a non-admin could trust a self-signed
        // root and forge a "Microsoft Corporation" signer, defeating this anchor.
        // We therefore only OPEN the handle here and verify AFTER RevertToSelf().
        std::wstring canonical;
        bool sigMicrosoft = false;
        unsigned long long callerVersion = 0;
        HANDLE hImage = INVALID_HANDLE_VALUE;
        if (gotImage)
        {
            canonical = CanonicalizePath(exePath);
            hImage = CreateFileW(canonical.c_str(),
                                 GENERIC_READ,
                                 FILE_SHARE_READ | FILE_SHARE_DELETE,
                                 nullptr,
                                 OPEN_EXISTING,
                                 FILE_ATTRIBUTE_NORMAL,
                                 nullptr);
            callerVersion = GetBinaryVersion(canonical);
        }

        // Revert before we touch any service-side resources (file IO etc) AND
        // before the signature trust decision, so WinVerifyTrust consults the
        // service's own (SYSTEM) machine trust store rather than the caller's.
        RevertToSelf();

        // Now in the service's own context: verify the signature through the
        // handle opened under impersonation.  SYSTEM cannot re-open a per-user
        // image by path, but it can read through the already-open handle, while
        // the chain is built against the machine trust store.
        if (hImage != INVALID_HANDLE_VALUE)
        {
            sigMicrosoft = VerifyMicrosoftSignature(hImage, canonical);
            CloseHandle(hImage);
        }

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

        // 4) Caller-image trust anchor.
        //    EVERY caller must be Microsoft-signed AND its version must EXACTLY
        //    equal the service's own version.
        //
        //    Why EXACT equality (not a floor + max-delta policy):
        //      * The service is per-user (PTSettingsSvc_<SID>),
        //        so a user's caller and service are 1:1 and upgrade together —
        //        the only legitimate caller version IS the service's own
        //        version.  The multi-user / multi-version tension that forced
        //        the earlier floor+delta magic numbers no longer exists,
        //        so exact-match is false-positive-free and removes a tunable
        //        threshold from a security boundary.
        //      * Exact-match is the anti-DOWNGRADE control: signature alone would
        //        accept a planted old-but-still-Microsoft-signed caller (a real
        //        threat since per-user callers live in user-writable
        //        %LocalAppData%); requiring caller == service rejects it.
        //      * The signature is verified by the service against the MACHINE
        //        trust store (CallerVerify.cpp), so it is NOT forgeable by a
        //        non-admin user-store root (defeats the objection).
        //
        //    sigMicrosoft and callerVersion were captured above under
        //    impersonation so a user-profile image is readable.
        const unsigned long long serviceVersion = GetServiceOwnVersion();
        bool sigOk = sigMicrosoft;
#ifdef _DEBUG
        // DEV-ONLY, conditional compilation: this block exists ONLY in Debug
        // builds and is physically absent from Release, so there is no bypass to
        // abuse in shipped binaries.  Local/smoke-test builds are not
        // Microsoft-signed, so a Debug build accepts an unsigned caller — but
        // the exact version-match below STILL applies (a locally-built caller
        // and service share the same version, so it passes).  Production is
        // always Release + ESRP-signed, where a real Microsoft signature is
        // mandatory.
        sigOk = true;
#endif
        const bool accepted =
            sigOk &&
            serviceVersion != 0 &&
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

