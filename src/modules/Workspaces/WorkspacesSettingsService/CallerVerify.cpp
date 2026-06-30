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
        // WinVerifyTrust with no UI; confirms the embedded signature is valid
        // and chains to a trusted root.  Runs in the service's own security
        // context, so it consults the machine trust stores, not the caller's.
        bool EmbeddedSignatureChainsToTrustedRoot(const std::wstring& path)
        {
            WINTRUST_FILE_INFO fileInfo = {};
            fileInfo.cbStruct = sizeof(fileInfo);
            fileInfo.pcwszFilePath = path.c_str();

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

            wd.dwStateAction = WTD_STATEACTION_CLOSE;
            WinVerifyTrust(noWindow, &action, &wd);

            return status == ERROR_SUCCESS;
        }

        // Extracts the signer leaf certificate's simple display name and checks
        // it is "Microsoft Corporation".  Production should pin the exact cert
        // (public key / thumbprint) rather than the subject string.
        bool SignerSubjectIsMicrosoft(const std::wstring& path)
        {
            HCERTSTORE store = nullptr;
            HCRYPTMSG msg = nullptr;
            DWORD encoding = 0;
            DWORD contentType = 0;
            DWORD formatType = 0;

            if (!CryptQueryObject(CERT_QUERY_OBJECT_FILE,
                                  path.c_str(),
                                  CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED,
                                  CERT_QUERY_FORMAT_FLAG_BINARY,
                                  0,
                                  &encoding,
                                  &contentType,
                                  &formatType,
                                  &store,
                                  &msg,
                                  nullptr))
            {
                return false;
            }

            bool isMicrosoft = false;

            DWORD signerInfoSize = 0;
            if (CryptMsgGetParam(msg, CMSG_SIGNER_INFO_PARAM, 0, nullptr, &signerInfoSize) &&
                signerInfoSize > 0)
            {
                std::vector<BYTE> signerInfoBuf(signerInfoSize);
                if (CryptMsgGetParam(msg, CMSG_SIGNER_INFO_PARAM, 0, signerInfoBuf.data(), &signerInfoSize))
                {
                    auto signerInfo = reinterpret_cast<CMSG_SIGNER_INFO*>(signerInfoBuf.data());

                    CERT_INFO certInfo = {};
                    certInfo.Issuer = signerInfo->Issuer;
                    certInfo.SerialNumber = signerInfo->SerialNumber;

                    PCCERT_CONTEXT cert = CertFindCertificateInStore(
                        store,
                        X509_ASN_ENCODING | PKCS_7_ASN_ENCODING,
                        0,
                        CERT_FIND_SUBJECT_CERT,
                        &certInfo,
                        nullptr);

                    if (cert)
                    {
                        wchar_t name[256] = {};
                        DWORD n = CertGetNameStringW(cert,
                                                     CERT_NAME_SIMPLE_DISPLAY_TYPE,
                                                     0,
                                                     nullptr,
                                                     name,
                                                     ARRAYSIZE(name));
                        if (n > 1)
                        {
                            isMicrosoft = (wcsstr(name, L"Microsoft Corporation") != nullptr);
                        }
                        CertFreeCertificateContext(cert);
                    }
                }
            }

            if (msg)
            {
                CryptMsgClose(msg);
            }
            if (store)
            {
                CertCloseStore(store, 0);
            }
            return isMicrosoft;
        }
    }

    bool VerifyMicrosoftSignature(const std::wstring& path)
    {
        if (path.empty())
        {
            return false;
        }
        return EmbeddedSignatureChainsToTrustedRoot(path) && SignerSubjectIsMicrosoft(path);
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

    bool IsCallerVersionAcceptable(unsigned long long callerVersion,
                                   unsigned long long serviceVersion)
    {
        if (callerVersion == 0 || serviceVersion == 0)
        {
            return false;
        }

        // 1) Absolute floor — the anti-downgrade boundary.
        if (callerVersion < kMinSupportedCallerVersion)
        {
            return false;
        }

        // 2) Bounded staleness on the MINOR-release field (bits 32..47).  Compare
        //    the absolute distance so a caller may trail OR (transiently, mid-
        //    upgrade) lead the service by at most kMaxMinorVersionDelta releases.
        const unsigned long long callerMinor = (callerVersion >> 32) & 0xFFFFull;
        const unsigned long long serviceMinor = (serviceVersion >> 32) & 0xFFFFull;
        const unsigned long long minorDelta =
            (serviceMinor > callerMinor) ? (serviceMinor - callerMinor)
                                         : (callerMinor - serviceMinor);
        if (minorDelta > kMaxMinorVersionDelta)
        {
            return false;
        }

        return true;
    }
}
