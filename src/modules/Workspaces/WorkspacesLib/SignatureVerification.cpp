// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "SignatureVerification.h"
#include "PackageVerification.h"

#include <imagehlp.h>
#include <mscat.h>
#include <softpub.h>
#include <wincrypt.h>

#include <filesystem>
#include <mutex>
#include <vector>

#pragma comment(lib, "wintrust.lib")
#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "imagehlp.lib")

namespace SignatureVerification
{
    namespace
    {
        Result Failure(LONG error)
        {
            return { Classify(error), error };
        }

        LONG LastError()
        {
            const DWORD error = GetLastError();
            return error == ERROR_SUCCESS ? E_FAIL : HRESULT_FROM_WIN32(error);
        }

        LONG MachineChainStatus(const CRYPT_PROVIDER_SGNR& signer, bool timestamp)
        {
            if (!signer.csCertChain || !signer.pasCertChain)
            {
                return TRUST_E_FAIL;
            }

            HCERTSTORE store = CertOpenStore(CERT_STORE_PROV_MEMORY, 0, 0, 0, nullptr);
            if (!store)
            {
                return LastError();
            }
            auto closeStore = wil::scope_exit([&] { CertCloseStore(store, 0); });

            for (DWORD i = 0; i < signer.csCertChain; ++i)
            {
                if (!CertAddCertificateContextToStore(store, signer.pasCertChain[i].pCert, CERT_STORE_ADD_ALWAYS, nullptr))
                {
                    return LastError();
                }
            }

            char signingUsage[] = szOID_PKIX_KP_CODE_SIGNING;
            char timestampUsage[] = szOID_PKIX_KP_TIMESTAMP_SIGNING;
            LPSTR usage = timestamp ? timestampUsage : signingUsage;
            CERT_CHAIN_PARA parameters{};
            parameters.cbSize = sizeof(parameters);
            parameters.RequestedUsage.dwType = USAGE_MATCH_TYPE_AND;
            parameters.RequestedUsage.Usage.cUsageIdentifier = 1;
            parameters.RequestedUsage.Usage.rgpszUsageIdentifier = &usage;

            PCCERT_CHAIN_CONTEXT chain = nullptr;
            const DWORD flags = CERT_CHAIN_CACHE_ONLY_URL_RETRIEVAL |
                                CERT_CHAIN_REVOCATION_CHECK_CACHE_ONLY |
                                CERT_CHAIN_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT;
            // WinVerifyTrust supplies the authenticated signing time, not the leaf's current expiry.
            FILETIME verifyAsOf = signer.sftVerifyAsOf;
            if (!CertGetCertificateChain(HCCE_LOCAL_MACHINE, signer.pasCertChain[0].pCert, &verifyAsOf, store, &parameters, flags, nullptr, &chain))
            {
                return LastError();
            }
            auto closeChain = wil::scope_exit([&] { CertFreeCertificateChain(chain); });

            const DWORD errors = chain->TrustStatus.dwErrorStatus;
            if (errors & CERT_TRUST_IS_REVOKED)
            {
                return CERT_E_REVOKED;
            }
            if (errors & CERT_TRUST_IS_EXPLICIT_DISTRUST)
            {
                return TRUST_E_EXPLICIT_DISTRUST;
            }
            if (errors & CERT_TRUST_IS_UNTRUSTED_ROOT)
            {
                return CERT_E_UNTRUSTEDROOT;
            }
            if (errors & CERT_TRUST_IS_PARTIAL_CHAIN)
            {
                return CERT_E_CHAINING;
            }
            if (errors & (CERT_TRUST_REVOCATION_STATUS_UNKNOWN | CERT_TRUST_IS_OFFLINE_REVOCATION))
            {
                return CRYPT_E_REVOCATION_OFFLINE;
            }

            CERT_CHAIN_POLICY_PARA policy{};
            policy.cbSize = sizeof(policy);
            CERT_CHAIN_POLICY_STATUS status{};
            status.cbSize = sizeof(status);
            if (!CertVerifyCertificateChainPolicy(timestamp ? CERT_CHAIN_POLICY_AUTHENTICODE_TS : CERT_CHAIN_POLICY_AUTHENTICODE,
                                                  chain,
                                                  &policy,
                                                  &status))
            {
                return LastError();
            }
            if (status.dwError != ERROR_SUCCESS)
            {
                return static_cast<LONG>(status.dwError);
            }
            return errors == 0 ? ERROR_SUCCESS : TRUST_E_FAIL;
        }

        Result VerifySignature(WINTRUST_FILE_INFO* file, WINTRUST_CATALOG_INFO* catalog, DWORD index, DWORD* secondaryCount)
        {
            GUID action = WINTRUST_ACTION_GENERIC_VERIFY_V2;
            WINTRUST_SIGNATURE_SETTINGS signatures{};
            signatures.cbStruct = sizeof(signatures);
            signatures.dwIndex = index;
            signatures.dwFlags = WSS_VERIFY_SPECIFIC | (secondaryCount ? WSS_GET_SECONDARY_SIG_COUNT : 0);

            WINTRUST_DATA data{};
            data.cbStruct = sizeof(data);
            data.dwUIChoice = WTD_UI_NONE;
            data.dwStateAction = WTD_STATEACTION_VERIFY;
            data.dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL | WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT | WTD_DISABLE_MD2_MD4;
            data.dwUnionChoice = catalog ? WTD_CHOICE_CATALOG : WTD_CHOICE_FILE;
            if (catalog)
            {
                data.pCatalog = catalog;
            }
            else
            {
                data.pFile = file;
                data.pSignatureSettings = &signatures;
            }

            const auto noWindow = static_cast<HWND>(INVALID_HANDLE_VALUE);
            const LONG error = WinVerifyTrust(noWindow, &action, &data);
            auto closeTrust = wil::scope_exit([&] {
                data.dwStateAction = WTD_STATEACTION_CLOSE;
                WinVerifyTrust(noWindow, &action, &data);
            });
            if (secondaryCount)
            {
                *secondaryCount = signatures.cSecondarySigs;
            }

            Result result = Failure(error);
            result.source = catalog ? L"catalog" : L"embedded";
            if (error != ERROR_SUCCESS)
            {
                return result;
            }

            const auto provider = WTHelperProvDataFromStateData(data.hWVTStateData);
            const auto signer = provider ? WTHelperGetProvSignerFromChain(provider, 0, FALSE, 0) : nullptr;
            if (!signer)
            {
                return Failure(TRUST_E_FAIL);
            }

            LONG machineStatus = MachineChainStatus(*signer, false);
            for (DWORD i = 0; machineStatus == ERROR_SUCCESS && i < signer->csCounterSigners; ++i)
            {
                machineStatus = MachineChainStatus(signer->pasCounterSigners[i], true);
            }
            if (machineStatus != ERROR_SUCCESS)
            {
                result.status = Classify(machineStatus);
                result.error = machineStatus;
                return result;
            }

            const auto certificate = signer->pasCertChain[0].pCert;
            const DWORD length = CertGetNameStringW(certificate, CERT_NAME_SIMPLE_DISPLAY_TYPE, 0, nullptr, nullptr, 0);
            if (length <= 1)
            {
                return Failure(TRUST_E_FAIL);
            }
            std::wstring publisher(length, L'\0');
            if (CertGetNameStringW(certificate, CERT_NAME_SIMPLE_DISPLAY_TYPE, 0, nullptr, publisher.data(), length) != length)
            {
                return Failure(TRUST_E_FAIL);
            }
            publisher.resize(length - 1);
            result.publisher = std::move(publisher);
            return result;
        }

        int FailurePriority(const Result& result)
        {
            if (result.error == CERT_E_REVOKED || result.error == CRYPT_E_REVOKED || result.error == TRUST_E_EXPLICIT_DISTRUST)
            {
                return 5;
            }
            switch (result.status)
            {
            case Status::InvalidSignature:
                return 4;
            case Status::CertificateUntrusted:
                return 3;
            case Status::UnableToVerify:
                return result.error == TRUST_E_NOSIGNATURE ? 0 : 2;
            default:
                return 1;
            }
        }

        void KeepFailure(Result& current, Result candidate)
        {
            if (FailurePriority(candidate) > FailurePriority(current))
            {
                current = std::move(candidate);
            }
        }
    }

    bool Result::IsVerified() const noexcept
    {
        return status == Status::Verified;
    }

    Status Classify(LONG error, bool confirmedUnsigned) noexcept
    {
        switch (error)
        {
        case ERROR_SUCCESS:
            return Status::Verified;
        case TRUST_E_NOSIGNATURE:
            return confirmedUnsigned ? Status::Unsigned : Status::UnableToVerify;
        case TRUST_E_BAD_DIGEST:
        case NTE_BAD_SIGNATURE:
            return Status::InvalidSignature;
        case CERT_E_UNTRUSTEDROOT:
        case CERT_E_CHAINING:
        case CERT_E_EXPIRED:
        case CERT_E_WRONG_USAGE:
        case CERT_E_REVOKED:
        case CRYPT_E_REVOKED:
        case TRUST_E_EXPLICIT_DISTRUST:
            return Status::CertificateUntrusted;
        default:
            return Status::UnableToVerify;
        }
    }

    const wchar_t* StatusName(Status status) noexcept
    {
        switch (status)
        {
        case Status::Verified:
            return L"verified";
        case Status::Unsigned:
            return L"unsigned";
        case Status::InvalidSignature:
            return L"invalid-signature";
        case Status::CertificateUntrusted:
            return L"certificate-untrusted";
        default:
            return L"verification-unavailable";
        }
    }

    std::wstring Result::Reason() const
    {
        if (!detailReason.empty())
        {
            return detailReason;
        }
        switch (error)
        {
        case CERT_E_REVOKED:
        case CRYPT_E_REVOKED:
            return L"revoked";
        case TRUST_E_EXPLICIT_DISTRUST:
            return L"explicit-distrust";
        case CERT_E_EXPIRED:
            return L"expired";
        case CRYPT_E_REVOCATION_OFFLINE:
        case CRYPT_E_NO_REVOCATION_CHECK:
        case CERT_E_REVOCATION_FAILURE:
            return L"revocation-unavailable";
        case TRUST_E_SUBJECT_FORM_UNKNOWN:
            return L"unresolved-target";
        default:
            return StatusName(status);
        }
    }

    bool IsCurrent(const LaunchTarget& target, const std::function<bool()>& isCanceled)
    {
        return PackageVerification::IsCurrent(target, isCanceled);
    }

    LaunchTarget Verify(const std::wstring& path, const std::function<bool()>& isCanceled)
    {
        const auto canceled = [&] { return isCanceled && isCanceled(); };
        if (canceled())
        {
            LaunchTarget target;
            target.path = path;
            target.result = Failure(HRESULT_FROM_WIN32(ERROR_CANCELLED));
            return target;
        }
        if (PackageVerification::ParseTarget(path))
        {
            return PackageVerification::Verify(path, isCanceled);
        }
        LaunchTarget executable;
        executable.path = path;
        if (path.empty() || path.find(L'\0') != std::wstring::npos ||
            !std::filesystem::path(path).is_absolute() || _wcsicmp(std::filesystem::path(path).extension().c_str(), L".exe") != 0)
        {
            return executable;
        }

        executable.file.reset(CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
        if (!executable.file)
        {
            executable.result = Failure(LastError());
            return executable;
        }

        const DWORD length = GetFinalPathNameByHandleW(executable.file.get(), nullptr, 0, FILE_NAME_NORMALIZED);
        if (!length)
        {
            executable.result = Failure(LastError());
            return executable;
        }
        std::wstring resolved(length, L'\0');
        const DWORD written = GetFinalPathNameByHandleW(executable.file.get(), resolved.data(), length, FILE_NAME_NORMALIZED);
        if (!written || written >= length)
        {
            executable.result = Failure(written ? HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER) : LastError());
            return executable;
        }
        resolved.resize(written);
        if (resolved.starts_with(L"\\\\?\\UNC\\"))
        {
            resolved = L"\\\\" + resolved.substr(8);
        }
        else if (resolved.starts_with(L"\\\\?\\"))
        {
            resolved.erase(0, 4);
        }
        executable.path = std::move(resolved);

        WINTRUST_FILE_INFO file{};
        file.cbStruct = sizeof(file);
        file.pcwszFilePath = executable.path.c_str();
        file.hFile = executable.file.get();
        DWORD secondaryCount = 0;
        executable.result = VerifySignature(&file, nullptr, 0, &secondaryCount);
        for (DWORD i = 1; !executable.result.IsVerified() && i != 0 && i <= secondaryCount; ++i)
        {
            if (canceled())
            {
                executable.result = Failure(HRESULT_FROM_WIN32(ERROR_CANCELLED));
                return executable;
            }
            auto result = VerifySignature(&file, nullptr, i, nullptr);
            if (result.IsVerified())
            {
                executable.result = std::move(result);
                return executable;
            }
            KeepFailure(executable.result, std::move(result));
        }
        if (executable.result.IsVerified())
        {
            return executable;
        }

        bool catalogFound = false;
        bool catalogSearchComplete = true;
        for (const auto algorithm : { L"SHA256", L"SHA1" })
        {
            if (canceled())
            {
                executable.result = Failure(HRESULT_FROM_WIN32(ERROR_CANCELLED));
                return executable;
            }
            HCATADMIN admin = nullptr;
            if (!CryptCATAdminAcquireContext2(&admin, nullptr, algorithm, nullptr, 0))
            {
                catalogSearchComplete = false;
                KeepFailure(executable.result, Failure(LastError()));
                continue;
            }
            HCATINFO catalog = nullptr;
            auto closeCatalog = wil::scope_exit([&] {
                if (catalog)
                {
                    CryptCATAdminReleaseCatalogContext(admin, catalog, 0);
                }
                CryptCATAdminReleaseContext(admin, 0);
            });

            DWORD hashSize = 0;
            if (!CryptCATAdminCalcHashFromFileHandle2(admin, executable.file.get(), &hashSize, nullptr, 0))
            {
                catalogSearchComplete = false;
                KeepFailure(executable.result, Failure(LastError()));
                continue;
            }
            std::vector<BYTE> hash(hashSize);
            if (!CryptCATAdminCalcHashFromFileHandle2(admin, executable.file.get(), &hashSize, hash.data(), 0))
            {
                catalogSearchComplete = false;
                KeepFailure(executable.result, Failure(LastError()));
                continue;
            }
            std::wstring tag;
            constexpr wchar_t digits[] = L"0123456789ABCDEF";
            for (BYTE byte : hash)
            {
                tag.push_back(digits[byte >> 4]);
                tag.push_back(digits[byte & 15]);
            }

            for (;;)
            {
                if (canceled())
                {
                    executable.result = Failure(HRESULT_FROM_WIN32(ERROR_CANCELLED));
                    return executable;
                }
                SetLastError(ERROR_SUCCESS);
                catalog = CryptCATAdminEnumCatalogFromHash(admin, hash.data(), hashSize, 0, catalog ? &catalog : nullptr);
                if (!catalog)
                {
                    const DWORD error = GetLastError();
                    if (error != ERROR_SUCCESS && error != ERROR_NOT_FOUND)
                    {
                        catalogSearchComplete = false;
                        KeepFailure(executable.result, Failure(HRESULT_FROM_WIN32(error)));
                    }
                    break;
                }
                catalogFound = true;
                CATALOG_INFO information{};
                information.cbStruct = sizeof(information);
                if (!CryptCATCatalogInfoFromContext(catalog, &information, 0))
                {
                    catalogSearchComplete = false;
                    KeepFailure(executable.result, Failure(LastError()));
                    continue;
                }
                WINTRUST_CATALOG_INFO member{};
                member.cbStruct = sizeof(member);
                member.pcwszCatalogFilePath = information.wszCatalogFile;
                member.pcwszMemberTag = tag.c_str();
                member.pcwszMemberFilePath = executable.path.c_str();
                member.hMemberFile = executable.file.get();
                member.pbCalculatedFileHash = hash.data();
                member.cbCalculatedFileHash = hashSize;
                member.hCatAdmin = admin;
                auto result = VerifySignature(nullptr, &member, 0, nullptr);
                if (result.IsVerified())
                {
                    executable.result = std::move(result);
                    return executable;
                }
                KeepFailure(executable.result, std::move(result));
            }
        }

        if (executable.result.error == TRUST_E_NOSIGNATURE && catalogSearchComplete && !catalogFound)
        {
            // ImageHlp is single-threaded. A malformed certificate table is not proof of no signature.
            static std::mutex imageMutex;
            std::lock_guard lock(imageMutex);
            DWORD certificates = 0;
            if (!ImageEnumerateCertificates(executable.file.get(), CERT_SECTION_TYPE_ANY, &certificates, nullptr, 0))
            {
                executable.result = Failure(LastError());
            }
            else if (certificates == 0)
            {
                executable.result.status = Status::Unsigned;
                executable.result.source.clear();
            }
        }
        return executable;
    }
}
