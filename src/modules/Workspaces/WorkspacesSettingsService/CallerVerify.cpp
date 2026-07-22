// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#include "CallerVerify.h"

#include <windows.h>
#include <wintrust.h>
#include <softpub.h>
#include <wincrypt.h>

#include <vector>

#pragma comment(lib, "wintrust.lib")
#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "version.lib")

namespace PTSettingsSvc
{
    namespace
    {
        // Verifies the embedded Authenticode signature of the image behind
        // `hFile` AND that the signer leaf subject is "Microsoft Corporation".
        //
        // Runs WinVerifyTrust in the CURRENT thread's security context — the
        // caller guarantees that is the service's own (SYSTEM) context, so the
        // chain is built against the MACHINE trust store and a user-poisoned
        // CurrentUser\Root cannot make a forged signer validate.  Reading the
        // image is done through `hFile` (opened earlier while impersonating,
        // since a per-user image is user-only readable); the signer is pulled
        // from the verified provider data, so the file is not re-read by path.
        bool ImageIsMicrosoftSigned(HANDLE hFile, const std::wstring& pathForDisplay)
        {
            WINTRUST_FILE_INFO fileInfo = {};
            fileInfo.cbStruct = sizeof(fileInfo);
            fileInfo.pcwszFilePath = pathForDisplay.c_str();
            fileInfo.hFile = hFile; // read via this handle, not by re-opening the path

            GUID action = WINTRUST_ACTION_GENERIC_VERIFY_V2;

            WINTRUST_DATA wd = {};
            wd.cbStruct = sizeof(wd);
            wd.dwUIChoice = WTD_UI_NONE;
            // Prototype: skip network revocation on the hot path.  Production
            // should use WTD_REVOKE_WHOLECHAIN with a cached/offline policy.
            wd.fdwRevocationChecks = WTD_REVOKE_NONE;
            wd.dwUnionChoice = WTD_CHOICE_FILE;
            wd.pFile = &fileInfo;
            wd.dwStateAction = WTD_STATEACTION_VERIFY;
            wd.dwProvFlags = WTD_SAFER_FLAG;

            HWND noWindow = static_cast<HWND>(INVALID_HANDLE_VALUE);
            LONG status = WinVerifyTrust(noWindow, &action, &wd);

            bool isMicrosoft = false;
            if (status == ERROR_SUCCESS)
            {
                // Chain is valid AND chains to a trusted root (machine store,
                // because we run as SYSTEM).  Confirm the signer leaf subject is
                // Microsoft, read from the verified provider data — no second
                // pass over the file, and no attacker-influenced trust store.
                CRYPT_PROVIDER_DATA* prov = WTHelperProvDataFromStateData(wd.hWVTStateData);
                if (prov)
                {
                    CRYPT_PROVIDER_SGNR* signer =
                        WTHelperGetProvSignerFromChain(prov, 0, FALSE, 0);
                    if (signer && signer->csCertChain > 0)
                    {
                        CRYPT_PROVIDER_CERT* leaf = WTHelperGetProvCertFromChain(signer, 0);
                        if (leaf && leaf->pCert)
                        {
                            wchar_t name[256] = {};
                            DWORD n = CertGetNameStringW(leaf->pCert,
                                                        CERT_NAME_SIMPLE_DISPLAY_TYPE,
                                                        0,
                                                        nullptr,
                                                        name,
                                                        ARRAYSIZE(name));
                            if (n > 1)
                            {
                                isMicrosoft = (wcsstr(name, L"Microsoft Corporation") != nullptr);
                            }
                        }
                    }
                }
            }

            wd.dwStateAction = WTD_STATEACTION_CLOSE;
            WinVerifyTrust(noWindow, &action, &wd);

            return isMicrosoft;
        }
    }

    bool VerifyMicrosoftSignature(HANDLE hImage, const std::wstring& pathForDisplay)
    {
        if (hImage == nullptr || hImage == INVALID_HANDLE_VALUE)
        {
            return false;
        }
        return ImageIsMicrosoftSigned(hImage, pathForDisplay);
    }

    unsigned long long GetBinaryVersion(const std::wstring& path)
    {
        if (path.empty())
        {
            return 0;
        }

        DWORD ignored = 0;
        DWORD size = GetFileVersionInfoSizeW(path.c_str(), &ignored);
        if (size == 0)
        {
            return 0;
        }

        std::vector<BYTE> buf(size);
        if (!GetFileVersionInfoW(path.c_str(), 0, size, buf.data()))
        {
            return 0;
        }

        VS_FIXEDFILEINFO* ffi = nullptr;
        UINT ffiLen = 0;
        if (!VerQueryValueW(buf.data(), L"\\", reinterpret_cast<LPVOID*>(&ffi), &ffiLen) ||
            ffi == nullptr || ffiLen == 0)
        {
            return 0;
        }

        return (static_cast<unsigned long long>(ffi->dwFileVersionMS) << 32) |
               static_cast<unsigned long long>(ffi->dwFileVersionLS);
    }

    unsigned long long GetServiceOwnVersion()
    {
        wchar_t self[MAX_PATH * 2] = {};
        DWORD n = GetModuleFileNameW(nullptr, self, ARRAYSIZE(self));
        if (n == 0 || n >= ARRAYSIZE(self))
        {
            return 0;
        }
        return GetBinaryVersion(self);
    }
}
