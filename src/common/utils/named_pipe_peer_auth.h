#pragma once

#include <Windows.h>
#include <softpub.h>
#include <wincrypt.h>
#include <wintrust.h>

#include <cwctype>
#include <string>
#include <vector>

#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "wintrust.lib")

namespace named_pipe_peer_auth
{
    enum class Peer
    {
        Client,
        Server,
    };

    enum class Validation
    {
        PowerToysPeer,
        TrustedSignedProcess,
    };

    struct Policy
    {
        std::wstring expectedProcessName;
        std::wstring trustedDirectory;
        std::wstring referenceBinaryPath;
        Validation validation = Validation::PowerToysPeer;
    };

    namespace details
    {
        inline std::wstring to_lower(std::wstring value)
        {
            for (auto& character : value)
            {
                character = static_cast<wchar_t>(towlower(character));
            }
            return value;
        }

        inline std::wstring canonicalize_path(const std::wstring& path)
        {
            const HANDLE handle = CreateFileW(
                path.c_str(),
                0,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                nullptr,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                nullptr);
            if (handle == INVALID_HANDLE_VALUE)
            {
                return {};
            }

            std::vector<wchar_t> buffer(32768);
            const DWORD length = GetFinalPathNameByHandleW(
                handle,
                buffer.data(),
                static_cast<DWORD>(buffer.size()),
                FILE_NAME_NORMALIZED);
            CloseHandle(handle);

            if (length == 0 || length >= buffer.size())
            {
                return {};
            }

            std::wstring result(buffer.data(), length);
            if (result.rfind(L"\\\\?\\", 0) == 0)
            {
                result.erase(0, 4);
            }
            return result;
        }

        inline std::wstring basename(const std::wstring& path)
        {
            const auto separator = path.find_last_of(L"\\/");
            return separator == std::wstring::npos ? path : path.substr(separator + 1);
        }

        inline bool path_is_under_directory(const std::wstring& canonicalPath, const std::wstring& directory)
        {
            auto canonicalDirectory = to_lower(canonicalize_path(directory));
            if (canonicalPath.empty() || canonicalDirectory.empty())
            {
                return false;
            }

            if (canonicalDirectory.back() != L'\\')
            {
                canonicalDirectory.push_back(L'\\');
            }

            const auto normalizedPath = to_lower(canonicalPath);
            return normalizedPath.size() > canonicalDirectory.size() &&
                   normalizedPath.compare(0, canonicalDirectory.size(), canonicalDirectory) == 0;
        }

        inline bool has_expected_name(const std::wstring& canonicalPath, const std::wstring& expectedName)
        {
            return !expectedName.empty() &&
                   to_lower(basename(canonicalPath)) == to_lower(expectedName);
        }

        inline bool has_intact_authenticode_signature(const std::wstring& path)
        {
            WINTRUST_FILE_INFO fileInfo{};
            fileInfo.cbStruct = sizeof(fileInfo);
            fileInfo.pcwszFilePath = path.c_str();

            WINTRUST_DATA trustData{};
            trustData.cbStruct = sizeof(trustData);
            trustData.dwUIChoice = WTD_UI_NONE;
            trustData.fdwRevocationChecks = WTD_REVOKE_NONE;
            trustData.dwUnionChoice = WTD_CHOICE_FILE;
            trustData.pFile = &fileInfo;
            trustData.dwStateAction = WTD_STATEACTION_VERIFY;
            trustData.dwProvFlags = WTD_SAFER_FLAG | WTD_CACHE_ONLY_URL_RETRIEVAL;

            GUID action = WINTRUST_ACTION_GENERIC_VERIFY_V2;
            const LONG status = WinVerifyTrust(
                static_cast<HWND>(INVALID_HANDLE_VALUE),
                &action,
                &trustData);

            trustData.dwStateAction = WTD_STATEACTION_CLOSE;
            WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &action, &trustData);
            return status == ERROR_SUCCESS;
        }

        class SignerCertificate
        {
        public:
            SignerCertificate() = default;
            SignerCertificate(const SignerCertificate&) = delete;
            SignerCertificate& operator=(const SignerCertificate&) = delete;

            ~SignerCertificate()
            {
                if (certificate)
                {
                    CertFreeCertificateContext(certificate);
                }
                if (message)
                {
                    CryptMsgClose(message);
                }
                if (store)
                {
                    CertCloseStore(store, 0);
                }
            }

            bool load(const std::wstring& path)
            {
                if (!has_intact_authenticode_signature(path) ||
                    !CryptQueryObject(
                        CERT_QUERY_OBJECT_FILE,
                        path.c_str(),
                        CERT_QUERY_CONTENT_FLAG_PKCS7_SIGNED_EMBED,
                        CERT_QUERY_FORMAT_FLAG_BINARY,
                        0,
                        nullptr,
                        nullptr,
                        nullptr,
                        &store,
                        &message,
                        nullptr))
                {
                    return false;
                }

                DWORD signerSize = 0;
                if (!CryptMsgGetParam(message, CMSG_SIGNER_INFO_PARAM, 0, nullptr, &signerSize) ||
                    signerSize == 0)
                {
                    return false;
                }

                std::vector<BYTE> signerBuffer(signerSize);
                if (!CryptMsgGetParam(
                        message,
                        CMSG_SIGNER_INFO_PARAM,
                        0,
                        signerBuffer.data(),
                        &signerSize))
                {
                    return false;
                }

                const auto* signer = reinterpret_cast<const CMSG_SIGNER_INFO*>(signerBuffer.data());
                CERT_INFO certificateInfo{};
                certificateInfo.Issuer = signer->Issuer;
                certificateInfo.SerialNumber = signer->SerialNumber;
                certificate = CertGetSubjectCertificateFromStore(
                    store,
                    X509_ASN_ENCODING | PKCS_7_ASN_ENCODING,
                    &certificateInfo);
                return certificate != nullptr;
            }

            PCCERT_CONTEXT get() const
            {
                return certificate;
            }

            HCERTSTORE additional_store() const
            {
                return store;
            }

        private:
            HCERTSTORE store = nullptr;
            HCRYPTMSG message = nullptr;
            PCCERT_CONTEXT certificate = nullptr;
        };

        inline bool chains_to_machine_root(PCCERT_CONTEXT certificate, HCERTSTORE additionalStore)
        {
            char codeSigningOid[] = szOID_PKIX_KP_CODE_SIGNING;
            LPSTR usages[] = { codeSigningOid };

            CERT_CHAIN_PARA chainParameters{};
            chainParameters.cbSize = sizeof(chainParameters);
            chainParameters.RequestedUsage.dwType = USAGE_MATCH_TYPE_AND;
            chainParameters.RequestedUsage.Usage.cUsageIdentifier = ARRAYSIZE(usages);
            chainParameters.RequestedUsage.Usage.rgpszUsageIdentifier = usages;

            PCCERT_CHAIN_CONTEXT chain = nullptr;
            if (!CertGetCertificateChain(
                    HCCE_LOCAL_MACHINE,
                    certificate,
                    nullptr,
                    additionalStore,
                    &chainParameters,
                    CERT_CHAIN_CACHE_ONLY_URL_RETRIEVAL | CERT_CHAIN_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT,
                    nullptr,
                    &chain))
            {
                return false;
            }

            const DWORD ignoredErrors =
                CERT_TRUST_REVOCATION_STATUS_UNKNOWN |
                CERT_TRUST_IS_OFFLINE_REVOCATION;
            bool valid = (chain->TrustStatus.dwErrorStatus & ~ignoredErrors) == 0;
            if (valid)
            {
                CERT_CHAIN_POLICY_PARA policyParameters{};
                policyParameters.cbSize = sizeof(policyParameters);
                CERT_CHAIN_POLICY_STATUS policyStatus{};
                policyStatus.cbSize = sizeof(policyStatus);
                valid =
                    CertVerifyCertificateChainPolicy(
                        CERT_CHAIN_POLICY_AUTHENTICODE,
                        chain,
                        &policyParameters,
                        &policyStatus) &&
                    policyStatus.dwError == 0;
            }

            CertFreeCertificateChain(chain);
            return valid;
        }

        inline bool has_matching_signer(
            const std::wstring& referenceBinaryPath,
            const std::wstring& peerPath)
        {
            SignerCertificate referenceSigner;
            SignerCertificate peerSigner;
            if (!referenceSigner.load(referenceBinaryPath) ||
                !peerSigner.load(peerPath) ||
                !chains_to_machine_root(referenceSigner.get(), referenceSigner.additional_store()) ||
                !chains_to_machine_root(peerSigner.get(), peerSigner.additional_store()))
            {
                return false;
            }

            return CertCompareCertificate(
                       X509_ASN_ENCODING | PKCS_7_ASN_ENCODING,
                       referenceSigner.get()->pCertInfo,
                       peerSigner.get()->pCertInfo) == TRUE;
        }

        inline bool has_trusted_signature(const std::wstring& path)
        {
            SignerCertificate signer;
            return signer.load(path) &&
                   chains_to_machine_root(signer.get(), signer.additional_store());
        }

        inline std::wstring get_process_path(DWORD processId)
        {
            const HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
            if (!process)
            {
                return {};
            }

            std::vector<wchar_t> buffer(32768);
            DWORD length = static_cast<DWORD>(buffer.size());
            const bool queried =
                QueryFullProcessImageNameW(process, 0, buffer.data(), &length) == TRUE;
            CloseHandle(process);
            return queried ? canonicalize_path(std::wstring(buffer.data(), length)) : std::wstring{};
        }

    }

    inline bool authenticate(HANDLE pipe, Peer peer, const Policy& policy)
    {
        ULONG processId = 0;
        const BOOL foundProcess =
            peer == Peer::Client ?
                GetNamedPipeClientProcessId(pipe, &processId) :
                GetNamedPipeServerProcessId(pipe, &processId);
        if (!foundProcess)
        {
            return false;
        }

        const auto peerPath = details::get_process_path(processId);
        if (peerPath.empty())
        {
            return false;
        }

        if (policy.validation == Validation::TrustedSignedProcess)
        {
#ifdef _DEBUG
            return true;
#else
            return details::has_trusted_signature(peerPath);
#endif
        }

        if (!details::has_expected_name(peerPath, policy.expectedProcessName) ||
            !details::path_is_under_directory(peerPath, policy.trustedDirectory) ||
            policy.referenceBinaryPath.empty())
        {
            return false;
        }

#ifdef _DEBUG
        return true;
#else
        return details::has_matching_signer(policy.referenceBinaryPath, peerPath);
#endif
    }
}
