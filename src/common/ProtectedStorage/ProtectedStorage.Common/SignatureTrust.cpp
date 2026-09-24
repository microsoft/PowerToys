#include "SignatureTrust.h"
#include "Common.h"
#include <softpub.h>
#include <wintrust.h>
#include <cstring>
#include <span>
#include <utility>

#pragma comment(lib, "crypt32.lib")
#pragma comment(lib, "wintrust.lib")

namespace PowerToys::ProtectedStorage
{
    namespace
    {
        constexpr DWORD Encoding = X509_ASN_ENCODING | PKCS_7_ASN_ENCODING;
        constexpr size_t ContentLimit = 64 * 1024;
        constexpr size_t SignatureLimit = 1024 * 1024;
        constexpr DWORD DecodedLimit = 16 * 1024 * 1024;
        constexpr char SignatureTimestampOid[] = "1.2.840.113549.1.9.16.2.14";
        constexpr DWORD OfflineErrors = CERT_TRUST_REVOCATION_STATUS_UNKNOWN | CERT_TRUST_IS_OFFLINE_REVOCATION;
        constexpr DWORD MachineChainFlags = CERT_CHAIN_CACHE_ONLY_URL_RETRIEVAL |
                                            CERT_CHAIN_REVOCATION_CHECK_CACHE_ONLY |
                                            CERT_CHAIN_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT |
                                            CERT_CHAIN_DISABLE_AUTH_ROOT_AUTO_UPDATE |
                                            CERT_CHAIN_OPT_IN_WEAK_SIGNATURE;
        constexpr DWORD MicrosoftRootFlags = MICROSOFT_ROOT_CERT_CHAIN_POLICY_CHECK_APPLICATION_ROOT_FLAG |
                                             MICROSOFT_ROOT_CERT_CHAIN_POLICY_DISABLE_FLIGHT_ROOT_FLAG;
        static_assert((MicrosoftRootFlags & MICROSOFT_ROOT_CERT_CHAIN_POLICY_ENABLE_TEST_ROOT_FLAG) == 0);

        void CloseStore(HCERTSTORE value) { CertCloseStore(value, 0); }

        template<typename T, auto Free>
        struct Resource
        {
            T value = nullptr;
            Resource() = default;
            explicit Resource(T initial) : value(initial) {}
            ~Resource()
            {
                if (value)
                    (void)Free(value);
            }
            Resource(const Resource&) = delete;
            Resource& operator=(const Resource&) = delete;
            Resource(Resource&& other) noexcept : value(std::exchange(other.value, nullptr)) {}
            Resource& operator=(Resource&&) = delete;
        };
        using Store = Resource<HCERTSTORE, CloseStore>;
        using Certificate = Resource<PCCERT_CONTEXT, CertFreeCertificateContext>;
        using Message = Resource<HCRYPTMSG, CryptMsgClose>;
        using Chain = Resource<PCCERT_CHAIN_CONTEXT, CertFreeCertificateChain>;
        using Timestamp = Resource<PCRYPT_TIMESTAMP_CONTEXT, CryptMemFree>;

        bool OidIs(const char* actual, const char* expected)
        {
            return actual && std::strcmp(actual, expected) == 0;
        }

        const wchar_t* StrongHashName(const char* oid)
        {
            if (OidIs(oid, szOID_NIST_sha256))
                return BCRYPT_SHA256_ALGORITHM;
            if (OidIs(oid, szOID_NIST_sha384))
                return BCRYPT_SHA384_ALGORITHM;
            if (OidIs(oid, szOID_NIST_sha512))
                return BCRYPT_SHA512_ALGORITHM;
            return nullptr;
        }

        void CheckStrongSigner(PCCERT_CONTEXT certificate, const CRYPT_ALGORITHM_IDENTIFIER& algorithm, bool release)
        {
            const auto hash = StrongHashName(algorithm.pszObjId);
            Require(hash && (!release || OidIs(algorithm.pszObjId, szOID_NIST_sha256)),
                    release ? "Release CMS signer must use SHA-256" : "Timestamp signer must use SHA-256 or stronger");
            CERT_STRONG_SIGN_PARA strong{};
            strong.cbSize = sizeof(strong);
            strong.dwInfoChoice = CERT_STRONG_SIGN_OID_INFO_CHOICE;
            char strongPolicy[] = szOID_CERT_STRONG_SIGN_OS_CURRENT;
            strong.pszOID = strongPolicy;
            if (!CertIsStrongHashToSign(&strong, hash, certificate))
                Fail("Release signature uses a weak signing key or digest");
        }

        // This reader only checks the CMS envelope and its detached/encapsulated shape.
        // CryptoAPI, not this parser, decodes certificates and verifies signatures.
        class DerReader
        {
            std::span<const BYTE> remaining;

        public:
            explicit DerReader(std::span<const BYTE> bytes) : remaining(bytes) {}
            bool Empty() const { return remaining.empty(); }
            BYTE Tag() const
            {
                Require(!Empty(), "Truncated DER value");
                return remaining.front();
            }
            std::span<const BYTE> Take(BYTE tag)
            {
                Require(remaining.size() >= 2 && remaining[0] == tag, "Invalid CMS DER tag");
                size_t header = 2;
                size_t length = remaining[1];
                if (length & 0x80)
                {
                    const size_t count = length & 0x7f;
                    Require(count && count <= sizeof(size_t) && count <= remaining.size() - header,
                            "Invalid or indefinite CMS DER length");
                    Require(remaining[header] != 0, "Noncanonical CMS DER length");
                    length = 0;
                    for (size_t i = 0; i < count; ++i)
                        length = (length << 8) | remaining[header++];
                    Require(length >= 128, "Noncanonical short CMS DER length");
                }
                Require(length <= remaining.size() - header, "CMS DER length exceeds input");
                const auto result = remaining.subspan(header, length);
                remaining = remaining.subspan(header + length);
                return result;
            }
            void End() const { Require(Empty(), "Unexpected or trailing CMS DER data"); }
        };

        constexpr bool BytesEqual(std::span<const BYTE> left, std::span<const BYTE> right)
        {
            return left.size() == right.size() && std::equal(left.begin(), left.end(), right.begin());
        }

        void CheckCmsEnvelope(std::span<const BYTE> bytes, bool detached)
        {
            constexpr BYTE signedDataOid[]{ 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 1, 7, 2 };
            constexpr BYTE dataOid[]{ 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 1, 7, 1 };
            constexpr BYTE tstInfoOid[]{ 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 1, 9, 0x10, 1, 4 };
            DerReader outer(bytes);
            DerReader contentInfo(outer.Take(0x30));
            outer.End();
            Require(BytesEqual(contentInfo.Take(0x06), signedDataOid), "CMS content is not SignedData");
            DerReader explicitContent(contentInfo.Take(0xa0));
            contentInfo.End();
            DerReader signedData(explicitContent.Take(0x30));
            explicitContent.End();
            Require(!signedData.Take(0x02).empty(), "Missing CMS version");
            (void)signedData.Take(0x31);
            DerReader encapsulated(signedData.Take(0x30));
            Require(BytesEqual(encapsulated.Take(0x06), detached ? std::span<const BYTE>(dataOid) : std::span<const BYTE>(tstInfoOid)),
                    "Unexpected CMS inner content type");
            if (!detached)
            {
                DerReader content(encapsulated.Take(0xa0));
                Require(!content.Take(0x04).empty(), "Empty RFC 3161 TSTInfo");
                content.End();
            }
            encapsulated.End();
            if (!signedData.Empty() && signedData.Tag() == 0xa0)
                (void)signedData.Take(0xa0);
            if (!signedData.Empty() && signedData.Tag() == 0xa1)
                (void)signedData.Take(0xa1);
            (void)signedData.Take(0x31);
            signedData.End();
        }

        std::vector<BYTE> MessageParameter(HCRYPTMSG message, DWORD parameter, DWORD minimum = 1)
        {
            DWORD size = 0;
            if (!CryptMsgGetParam(message, parameter, 0, nullptr, &size))
                Fail("Cannot size CMS parameter");
            Require(size >= minimum && size <= DecodedLimit, "CMS decoded parameter exceeds bounds");
            std::vector<BYTE> buffer(size);
            if (!CryptMsgGetParam(message, parameter, 0, buffer.data(), &size))
                Fail("Cannot read CMS parameter");
            Require(size >= minimum && size <= buffer.size(), "Invalid CMS parameter size");
            return buffer;
        }

        DWORD MessageDword(HCRYPTMSG message, DWORD parameter)
        {
            DWORD value = 0;
            DWORD size = sizeof(value);
            if (!CryptMsgGetParam(message, parameter, 0, &value, &size))
                Fail("Cannot read CMS signer count or type");
            Require(size == sizeof(value), "Invalid CMS integer parameter");
            return value;
        }

        Message DecodeMessage(std::span<const BYTE> encoded, const std::vector<BYTE>* detachedContent)
        {
            Message result(CryptMsgOpenToDecode(Encoding, detachedContent ? CMSG_DETACHED_FLAG : 0, 0, 0, nullptr, nullptr));
            if (!result.value)
                Fail("Cannot open CMS decoder");
            if (!CryptMsgUpdate(result.value, encoded.data(), static_cast<DWORD>(encoded.size()), TRUE))
                Fail("Cannot decode CMS signature");
            Require(MessageDword(result.value, CMSG_TYPE_PARAM) == CMSG_SIGNED, "CMS must be SignedData");
            Require(MessageDword(result.value, CMSG_SIGNER_COUNT_PARAM) == 1, "CMS must contain exactly one signer");
            if (detachedContent &&
                !CryptMsgUpdate(result.value, detachedContent->data(), static_cast<DWORD>(detachedContent->size()), TRUE))
                Fail("Cannot bind CMS to exact detached content");
            return result;
        }

        Store MemoryStore()
        {
            Store result(CertOpenStore(CERT_STORE_PROV_MEMORY, 0, 0, CERT_STORE_CREATE_NEW_FLAG, nullptr));
            if (!result.value)
                Fail("Cannot create untrusted certificate collection");
            return result;
        }

        void AddCertificate(HCERTSTORE destination, PCCERT_CONTEXT certificate)
        {
            Require(certificate && certificate->pCertInfo, "Missing supporting signing certificate");
            if (!CertAddCertificateContextToStore(destination, certificate, CERT_STORE_ADD_USE_EXISTING, nullptr))
                Fail("Cannot collect embedded certificate");
        }

        void CopyCertificates(HCERTSTORE destination, HCERTSTORE source)
        {
            PCCERT_CONTEXT current = nullptr;
            while ((current = CertEnumCertificatesInStore(source, current)) != nullptr)
            {
                if (!CertAddCertificateContextToStore(destination, current, CERT_STORE_ADD_USE_EXISTING, nullptr))
                {
                    const DWORD error = GetLastError();
                    CertFreeCertificateContext(current);
                    Fail("Cannot collect CMS certificates", error);
                }
            }
            Require(GetLastError() == static_cast<DWORD>(CRYPT_E_NOT_FOUND), "Cannot enumerate CMS certificates");
        }

        Store MessageCertificates(HCRYPTMSG message)
        {
            Store embedded(CertOpenStore(CERT_STORE_PROV_MSG, Encoding, 0, CERT_STORE_READONLY_FLAG, message));
            if (!embedded.value)
                Fail("Cannot open CMS certificate collection");
            auto certificates = MemoryStore();
            // Embedded roots are merely candidates, never trust anchors. Do not import
            // message-supplied CRLs or CTLs into the machine chain's additional store.
            CopyCertificates(certificates.value, embedded.value);
            return certificates;
        }

        std::vector<BYTE> DecodeObject(LPCSTR type, const CRYPT_DATA_BLOB& encoded)
        {
            DWORD size = 0;
            if (!CryptDecodeObjectEx(Encoding, type, encoded.pbData, encoded.cbData, 0, nullptr, nullptr, &size))
                Fail("Cannot size certificate field");
            Require(size && size <= DecodedLimit, "Decoded certificate field exceeds bounds");
            std::vector<BYTE> result(size);
            if (!CryptDecodeObjectEx(Encoding, type, encoded.pbData, encoded.cbData, 0, nullptr, result.data(), &size))
                Fail("Cannot decode certificate field");
            return result;
        }

        void CheckMicrosoftOrganization(PCCERT_CONTEXT certificate)
        {
            auto decoded = DecodeObject(X509_NAME, certificate->pCertInfo->Subject);
            const auto* name = reinterpret_cast<const CERT_NAME_INFO*>(decoded.data());
            DWORD organizations = 0;
            for (DWORD rdn = 0; rdn < name->cRDN; ++rdn)
            {
                for (DWORD i = 0; i < name->rgRDN[rdn].cRDNAttr; ++i)
                {
                    const auto& attribute = name->rgRDN[rdn].rgRDNAttr[i];
                    if (!OidIs(attribute.pszObjId, szOID_ORGANIZATION_NAME))
                        continue;
                    ++organizations;
                    constexpr std::wstring_view expected = L"Microsoft Corporation";
                    auto value = attribute.Value;
                    const DWORD size = CertRDNValueToStrW(attribute.dwValueType, &value, nullptr, 0);
                    Require(size == expected.size() + 1, "Release signer organization is not Microsoft Corporation");
                    std::vector<wchar_t> text(size);
                    Require(CertRDNValueToStrW(attribute.dwValueType, &value, text.data(), size) == size &&
                                text.back() == L'\0' && std::equal(expected.begin(), expected.end(), text.begin()),
                            "Release signer organization is not Microsoft Corporation");
                }
            }
            Require(organizations == 1, "Release signer must have exactly one Microsoft Corporation organization");
        }

        void CheckExplicitEku(PCCERT_CONTEXT certificate, bool timestamp)
        {
            PCERT_EXTENSION extension = nullptr;
            for (DWORD i = 0; i < certificate->pCertInfo->cExtension; ++i)
            {
                auto& candidate = certificate->pCertInfo->rgExtension[i];
                if (OidIs(candidate.pszObjId, szOID_ENHANCED_KEY_USAGE))
                {
                    Require(!extension, "Ambiguous certificate EKU extensions");
                    extension = &candidate;
                }
            }
            Require(extension != nullptr, "Signing certificate must explicitly declare its EKU");
            const auto decoded = DecodeObject(X509_ENHANCED_KEY_USAGE, extension->Value);
            const auto* usage = reinterpret_cast<const CERT_ENHKEY_USAGE*>(decoded.data());
            const char* required = timestamp ? szOID_PKIX_KP_TIMESTAMP_SIGNING : szOID_PKIX_KP_CODE_SIGNING;
            DWORD matches = 0;
            for (DWORD i = 0; i < usage->cUsageIdentifier; ++i)
                matches += OidIs(usage->rgpszUsageIdentifier[i], required) ? 1 : 0;
            Require(matches == 1, timestamp ? "TSA certificate lacks timestamp EKU" : "Release signer lacks code-signing EKU");
            // RFC 3161 section 2.3 requires a critical, timestamp-only EKU.
            Require(!timestamp || (extension->fCritical && usage->cUsageIdentifier == 1),
                    "RFC 3161 TSA EKU must be critical and timestamp-only");
        }

        constexpr bool IsRevoked(DWORD error)
        {
            return error == static_cast<DWORD>(CERT_E_REVOKED) || error == static_cast<DWORD>(CRYPT_E_REVOKED);
        }

        constexpr bool IsUnavailableRevocation(DWORD error)
        {
            return error == static_cast<DWORD>(CRYPT_E_REVOCATION_OFFLINE) ||
                   error == static_cast<DWORD>(CRYPT_E_NO_REVOCATION_CHECK) ||
                   error == static_cast<DWORD>(CERT_E_REVOCATION_FAILURE);
        }

        void CheckRevocationResult(DWORD result, const char* purpose)
        {
            if (IsRevoked(result))
                Fail(std::string(purpose) + ": certificate is explicitly revoked", static_cast<DWORD>(CERT_E_REVOKED));
            if (result && !IsUnavailableRevocation(result))
                Fail(std::string(purpose) + ": revocation failed for a reason other than unavailable cached status", result);
        }

        void CheckTrustBits(DWORD errors, const char* purpose)
        {
            if (errors & CERT_TRUST_IS_REVOKED)
                Fail(std::string(purpose) + ": certificate is explicitly revoked", static_cast<DWORD>(CERT_E_REVOKED));
            if (errors & ~OfflineErrors)
                Fail(std::string(purpose) + ": certificate chain trust errors=" + std::to_string(errors & ~OfflineErrors),
                     static_cast<DWORD>(TRUST_E_SUBJECT_NOT_TRUSTED));
        }

        void InspectChain(PCCERT_CHAIN_CONTEXT chain, const char* purpose, bool revocationOnly = false)
        {
            Require(chain && chain->cChain && chain->rgpChain, "Missing certificate chain");
            bool unavailable = false;
            const auto inspect = [&](DWORD errors) {
                if (revocationOnly)
                {
                    if (errors & CERT_TRUST_IS_REVOKED)
                        Fail(std::string(purpose) + ": current cached status is revoked", static_cast<DWORD>(CERT_E_REVOKED));
                    if (errors & CERT_TRUST_IS_EXPLICIT_DISTRUST)
                        Fail(std::string(purpose) + ": certificate is explicitly distrusted", static_cast<DWORD>(TRUST_E_EXPLICIT_DISTRUST));
                }
                else
                {
                    CheckTrustBits(errors, purpose);
                }
                unavailable |= (errors & OfflineErrors) != 0;
            };
            inspect(chain->TrustStatus.dwErrorStatus);
            for (DWORD i = 0; i < chain->cChain; ++i)
            {
                const auto* simple = chain->rgpChain[i];
                Require(simple && simple->cElement && simple->rgpElement, "Empty certificate chain");
                inspect(simple->TrustStatus.dwErrorStatus);
                for (DWORD j = 0; j < simple->cElement; ++j)
                {
                    const auto* element = simple->rgpElement[j];
                    Require(element && element->pCertContext, "Empty certificate chain element");
                    inspect(element->TrustStatus.dwErrorStatus);
                    if (element->pRevocationInfo)
                        CheckRevocationResult(element->pRevocationInfo->dwRevocationResult, purpose);
                }
            }
            if (unavailable)
            {
                const std::string diagnostic = std::string("[ProtectedStorage][") + ReleaseTrustPolicy + "] " + purpose +
                    ": revocation status unavailable in local cache; accepted by offline consumer policy, NOT proof of non-revocation.\n";
                OutputDebugStringA(diagnostic.c_str());
            }
        }

        void CheckChainPolicy(PCCERT_CHAIN_CONTEXT chain, LPCSTR policy, DWORD flags, void* extra, const char* purpose)
        {
            CERT_CHAIN_POLICY_PARA parameters{};
            parameters.cbSize = sizeof(parameters);
            parameters.dwFlags = flags;
            parameters.pvExtraPolicyPara = extra;
            CERT_CHAIN_POLICY_STATUS status{};
            status.cbSize = sizeof(status);
            if (!CertVerifyCertificateChainPolicy(policy, chain, &parameters, &status))
                Fail(std::string(purpose) + ": cannot evaluate certificate chain policy");
            if (status.dwError)
                Fail(std::string(purpose) + ": certificate chain policy rejected signer", status.dwError);
        }

        Chain MachineChain(PCCERT_CONTEXT certificate, HCERTSTORE additional, FILETIME* time, bool timestamp)
        {
            CERT_CHAIN_PARA parameters{};
            parameters.cbSize = sizeof(parameters);
            char codeSigningUsage[] = szOID_PKIX_KP_CODE_SIGNING;
            char timestampUsage[] = szOID_PKIX_KP_TIMESTAMP_SIGNING;
            char* usage = timestamp ? timestampUsage : codeSigningUsage;
            parameters.RequestedUsage.dwType = USAGE_MATCH_TYPE_AND;
            parameters.RequestedUsage.Usage.cUsageIdentifier = 1;
            parameters.RequestedUsage.Usage.rgpszUsageIdentifier = &usage;
            Chain result;
            if (!CertGetCertificateChain(HCCE_LOCAL_MACHINE, certificate, time, additional, &parameters,
                                         MachineChainFlags, nullptr, &result.value))
                Fail(timestamp ? "Cannot build machine TSA chain" : "Cannot build machine code-signing chain");
            return result;
        }

        void VerifyMachineSigner(PCCERT_CONTEXT certificate, HCERTSTORE additional, FILETIME time, bool timestamp)
        {
            Require(certificate && certificate->pCertInfo, "Missing verified signing certificate");
            CheckExplicitEku(certificate, timestamp);
            if (!timestamp)
                CheckMicrosoftOrganization(certificate);
            const char* purpose = timestamp ? "RFC 3161 TSA" : "Microsoft production code signer";
            // pTime controls certificate validity, not the trusted-root/CTL store state
            // or CRL freshness. Do not use CERT_CHAIN_TIMESTAMP_TIME: that flag also
            // checks end-certificate expiration now and can defeat timestamp longevity.
            auto chain = MachineChain(certificate, additional, &time, timestamp);
            InspectChain(chain.value, purpose);
            constexpr DWORD policyFlags = CERT_CHAIN_POLICY_IGNORE_ALL_REV_UNKNOWN_FLAGS;
            CheckChainPolicy(chain.value, CERT_CHAIN_POLICY_BASE, policyFlags, nullptr, purpose);
            if (timestamp)
            {
                AUTHENTICODE_TS_EXTRA_CERT_CHAIN_POLICY_PARA extra{};
                extra.cbSize = sizeof(extra);
                CheckChainPolicy(chain.value, CERT_CHAIN_POLICY_AUTHENTICODE_TS, policyFlags, &extra, purpose);
            }
            else
            {
                AUTHENTICODE_EXTRA_CERT_CHAIN_POLICY_PARA extra{};
                extra.cbSize = sizeof(extra);
                CheckChainPolicy(chain.value, CERT_CHAIN_POLICY_AUTHENTICODE, policyFlags, &extra, purpose);
                // MICROSOFT_ROOT checks only root identity, not chain validity or EKU.
                CheckChainPolicy(chain.value, CERT_CHAIN_POLICY_MICROSOFT_ROOT, MicrosoftRootFlags, nullptr, purpose);
            }
            // A revocation effective after the signing time must not disappear merely
            // because the first chain was evaluated historically. This second chain
            // checks current revocation/distrust only; expiration now is not a failure.
            auto current = MachineChain(certificate, additional, nullptr, timestamp);
            InspectChain(current.value, purpose, true);
        }

        const CRYPT_ATTR_BLOB& TimestampAttribute(const CRYPT_ATTRIBUTES& attributes)
        {
            const CRYPT_ATTR_BLOB* token = nullptr;
            for (DWORD i = 0; i < attributes.cAttr; ++i)
            {
                const auto& attribute = attributes.rgAttr[i];
                Require(!OidIs(attribute.pszObjId, szOID_RSA_counterSign) &&
                            !OidIs(attribute.pszObjId, szOID_RFC3161v21_counterSign),
                        "Unsupported or ambiguous CMS countersignature");
                if (!OidIs(attribute.pszObjId, SignatureTimestampOid) && !OidIs(attribute.pszObjId, szOID_RFC3161_counterSign))
                    continue;
                Require(!token && attribute.cValue == 1 && attribute.rgValue,
                        "CMS must have exactly one unambiguous RFC 3161 timestamp");
                token = attribute.rgValue;
                Require(token->pbData && token->cbData && token->cbData <= SignatureLimit, "RFC 3161 timestamp exceeds bounds");
            }
            Require(token != nullptr, "CMS signature requires an RFC 3161 timestamp; signingTime is not trusted");
            return *token;
        }

        FILETIME VerifyTimestamp(const CMSG_CMS_SIGNER_INFO& signer, HCERTSTORE additional)
        {
            const auto& token = TimestampAttribute(signer.UnauthAttrs);
            // Both RFC 3161 Appendix A's signatureTimeStampToken and the Windows SDK's
            // szOID_RFC3161_counterSign carry a ContentInfo, NOT a bare SignerInfo or
            // TimeStampResp. Accept the Microsoft OID only with that exact CMS/TSTInfo shape.
            const std::span<const BYTE> encoded(token.pbData, token.cbData);
            CheckCmsEnvelope(encoded, false);
            auto message = DecodeMessage(encoded, nullptr);
            const auto tokenSignerBytes = MessageParameter(message.value, CMSG_CMS_SIGNER_INFO_PARAM, sizeof(CMSG_CMS_SIGNER_INFO));
            const auto* tokenSigner = reinterpret_cast<const CMSG_CMS_SIGNER_INFO*>(tokenSignerBytes.data());
            Require(signer.EncryptedHash.pbData && signer.EncryptedHash.cbData, "CMS signer has no signature value");
            Timestamp timestamp;
            Certificate tsa;
            Store embedded;
            if (!CryptVerifyTimeStampSignature(token.pbData, token.cbData, signer.EncryptedHash.pbData,
                                               signer.EncryptedHash.cbData, additional, &timestamp.value, &tsa.value, &embedded.value))
                Fail("RFC 3161 signature or signature-value message imprint is invalid");
            Require(timestamp.value && timestamp.value->pTimeStamp && tsa.value, "Incomplete verified RFC 3161 timestamp");
            const auto& info = *timestamp.value->pTimeStamp;
            Require(info.dwVersion == TIMESTAMP_VERSION && info.pszTSAPolicyId && *info.pszTSAPolicyId,
                    "Invalid RFC 3161 version or TSA policy");
            Require(StrongHashName(info.HashAlgorithm.pszObjId) != nullptr, "RFC 3161 message imprint must use SHA-256 or stronger");
            FILETIME now{};
            GetSystemTimeAsFileTime(&now);
            Require((info.ftTime.dwHighDateTime || info.ftTime.dwLowDateTime) && CompareFileTime(&info.ftTime, &now) <= 0,
                    "RFC 3161 timestamp is empty or in the future");
            for (DWORD i = 0; i < info.cExtension; ++i)
                Require(!info.rgExtension[i].fCritical, "Unsupported critical RFC 3161 extension");
            CheckStrongSigner(tsa.value, tokenSigner->HashAlgorithm, false);
            auto certificates = MemoryStore();
            CopyCertificates(certificates.value, additional);
            if (embedded.value)
                CopyCertificates(certificates.value, embedded.value);
            VerifyMachineSigner(tsa.value, certificates.value, info.ftTime, true);
            return info.ftTime;
        }

        struct WinTrustState
        {
            GUID action = WINTRUST_ACTION_GENERIC_VERIFY_V2;
            WINTRUST_DATA data{};
            WinTrustState() { data.cbStruct = sizeof(data); }
            ~WinTrustState()
            {
                data.dwStateAction = WTD_STATEACTION_CLOSE;
                (void)WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &action, &data);
            }
            WinTrustState(const WinTrustState&) = delete;
            WinTrustState& operator=(const WinTrustState&) = delete;
        };
    }

    void VerifyDetachedReleaseSignature(const std::vector<BYTE>& content, const std::vector<BYTE>& signature)
    {
        Require(!content.empty() && content.size() <= ContentLimit, "Detached release content exceeds 64 KiB or is empty");
        Require(!signature.empty() && signature.size() <= SignatureLimit, "Detached release signature exceeds 1 MiB or is empty");
        CheckCmsEnvelope(signature, true);
        auto message = DecodeMessage(signature, &content);
        const auto signerBytes = MessageParameter(message.value, CMSG_CMS_SIGNER_INFO_PARAM, sizeof(CMSG_CMS_SIGNER_INFO));
        const auto* signer = reinterpret_cast<const CMSG_CMS_SIGNER_INFO*>(signerBytes.data());
        auto certificates = MessageCertificates(message.value);
        Certificate certificate(CertFindCertificateInStore(certificates.value, Encoding, 0, CERT_FIND_CERT_ID, &signer->SignerId, nullptr));
        if (!certificate.value)
            Fail("CMS must embed the actual signing certificate");
        CheckStrongSigner(certificate.value, signer->HashAlgorithm, true);
        CMSG_CTRL_VERIFY_SIGNATURE_EX_PARA verify{};
        verify.cbSize = sizeof(verify);
        verify.dwSignerIndex = 0;
        verify.dwSignerType = CMSG_VERIFY_SIGNER_PUBKEY;
        verify.pvSigner = &certificate.value->pCertInfo->SubjectPublicKeyInfo;
        if (!CryptMsgControl(message.value, 0, CMSG_CTRL_VERIFY_SIGNATURE_EX, &verify))
            Fail("Detached release signature does not authenticate the exact content");
        const FILETIME timestamp = VerifyTimestamp(*signer, certificates.value);
        VerifyMachineSigner(certificate.value, certificates.value, timestamp, false);
    }

    void VerifyAuthenticodeReleaseSignature(HANDLE heldFile, const std::wstring& path)
    {
        Require(heldFile && heldFile != INVALID_HANDLE_VALUE && GetFileType(heldFile) == FILE_TYPE_DISK,
                "Authenticode requires a held disk-file handle");
        Require(!path.empty() && path.find(L'\0') == std::wstring::npos, "Invalid Authenticode diagnostic path");
        WINTRUST_FILE_INFO file{};
        file.cbStruct = sizeof(file);
        file.pcwszFilePath = path.c_str();
        file.hFile = heldFile;
        CERT_STRONG_SIGN_PARA strong{};
        strong.cbSize = sizeof(strong);
        strong.dwInfoChoice = CERT_STRONG_SIGN_OID_INFO_CHOICE;
        char strongPolicy[] = szOID_CERT_STRONG_SIGN_OS_CURRENT;
        strong.pszOID = strongPolicy;
        WINTRUST_SIGNATURE_SETTINGS settings{};
        settings.cbStruct = sizeof(settings);
        settings.dwFlags = WSS_VERIFY_SPECIFIC;
        settings.dwIndex = 0;
        settings.pCryptoPolicy = &strong;
        WinTrustState trust;
        trust.data.dwUIChoice = WTD_UI_NONE;
        trust.data.dwUnionChoice = WTD_CHOICE_FILE;
        trust.data.pFile = &file;
        trust.data.dwStateAction = WTD_STATEACTION_VERIFY;
        // WinVerifyTrust authenticates the held object and its timestamp. All revocation
        // decisions are made below on machine chains, with one explicit offline policy,
        // rather than retrying a failed WinVerifyTrust result with weaker trust settings.
        trust.data.fdwRevocationChecks = WTD_REVOKE_NONE;
        trust.data.dwProvFlags = WTD_CACHE_ONLY_URL_RETRIEVAL | WTD_REVOCATION_CHECK_NONE | WTD_DISABLE_MD2_MD4;
        trust.data.pSignatureSettings = &settings;
        const LONG result = WinVerifyTrust(static_cast<HWND>(INVALID_HANDLE_VALUE), &trust.action, &trust.data);
        if (result != ERROR_SUCCESS)
            Fail("Held-file Authenticode verification failed", static_cast<DWORD>(result));
        auto* provider = WTHelperProvDataFromStateData(trust.data.hWVTStateData);
        Require(provider != nullptr, "Authenticode provider returned no verified state");
        const auto* signer = WTHelperGetProvSignerFromChain(provider, 0, FALSE, 0);
        Require(signer && signer->dwError == ERROR_SUCCESS && signer->csCertChain &&
                    signer->pasCertChain && signer->pasCertChain[0].pCert && signer->psSigner,
                "Authenticode provider returned no verified signing certificate");
        auto certificates = MemoryStore();
        for (DWORD i = 0; i < signer->csCertChain; ++i)
            AddCertificate(certificates.value, signer->pasCertChain[i].pCert);
        CheckStrongSigner(signer->pasCertChain[0].pCert, signer->psSigner->HashAlgorithm, true);
        FILETIME verificationTime = signer->sftVerifyAsOf;
        Require(signer->csCounterSigners == 1, "Release Authenticode requires exactly one verified timestamp signer");
        if (signer->csCounterSigners)
        {
            const auto* tsa = signer->pasCounterSigners;
            Require(tsa && tsa->dwError == ERROR_SUCCESS && tsa->csCertChain && tsa->pasCertChain &&
                        tsa->pasCertChain[0].pCert && tsa->psSigner,
                    "Authenticode timestamp signer was not verified");
            for (DWORD i = 0; i < tsa->csCertChain; ++i)
                AddCertificate(certificates.value, tsa->pasCertChain[i].pCert);
            CheckStrongSigner(tsa->pasCertChain[0].pCert, tsa->psSigner->HashAlgorithm, false);
            bool rfc3161 = false;
            for (DWORD i = 0; i < signer->psSigner->UnauthAttrs.cAttr; ++i)
            {
                const auto* oid = signer->psSigner->UnauthAttrs.rgAttr[i].pszObjId;
                rfc3161 |= OidIs(oid, SignatureTimestampOid) || OidIs(oid, szOID_RFC3161_counterSign);
            }
            if (rfc3161)
            {
                CMSG_CMS_SIGNER_INFO binding{};
                binding.EncryptedHash = signer->psSigner->EncryptedHash;
                binding.UnauthAttrs = signer->psSigner->UnauthAttrs;
                // Also inspect the RFC 3161 imprint algorithm; the TSA's own CMS
                // digest algorithm alone does not establish the imprint's strength.
                verificationTime = VerifyTimestamp(binding, certificates.value);
            }
            else
            {
                VerifyMachineSigner(tsa->pasCertChain[0].pCert, certificates.value, verificationTime, true);
            }
        }
        VerifyMachineSigner(signer->pasCertChain[0].pCert, certificates.value, verificationTime, false);
    }
}
