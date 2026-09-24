// Standalone test executable: compile this translation unit only, linking crypt32,
// wintrust and ncrypt. Including the implementation exercises private decisions
// without adding test switches or alternate trust anchors to the production API.
#define CMSG_SIGNER_ENCODE_INFO_HAS_CMS_FIELDS
#include "..\ProtectedStorage.Common\SignatureTrust.cpp"
#include <iostream>
#include <ncrypt.h>
#include <random>

#pragma comment(lib, "ncrypt.lib")

namespace PowerToys::ProtectedStorage
{
    [[noreturn]] void Fail(const std::string& message, DWORD code)
    {
        throw Error(message, code);
    }

    void Require(bool condition, const std::string& message)
    {
        if (!condition)
            Fail(message, ERROR_INVALID_DATA);
    }
}

using namespace PowerToys::ProtectedStorage;

namespace
{
    unsigned passed = 0;

    void Expect(bool condition, const char* message)
    {
        if (!condition)
            throw std::runtime_error(message);
        ++passed;
    }

    template<typename F>
    void Reject(F action, std::string_view message = {})
    {
        try
        {
            action();
        }
        catch (const Error& error)
        {
            if (!message.empty() && std::string_view(error.what()).find(message) == std::string_view::npos)
                throw std::runtime_error(std::string("Expected '") + std::string(message) + "', got '" + error.what() +
                                         "' (error=" + std::to_string(error.code) + ")");
            ++passed;
            return;
        }
        throw std::runtime_error("Expected rejection");
    }

    std::vector<BYTE> Encode(LPCSTR type, const void* data)
    {
        DWORD size = 0;
        if (!CryptEncodeObjectEx(Encoding, type, data, 0, nullptr, nullptr, &size))
            Fail("Fixture encoding size");
        std::vector<BYTE> encoded(size);
        if (!CryptEncodeObjectEx(Encoding, type, data, 0, nullptr, encoded.data(), &size))
            Fail("Fixture encoding");
        return encoded;
    }

    std::vector<BYTE> Name(const wchar_t* name)
    {
        DWORD size = 0;
        if (!CertStrToNameW(Encoding, name, CERT_X500_NAME_STR, nullptr, nullptr, &size, nullptr))
            Fail("Fixture subject size");
        std::vector<BYTE> encoded(size);
        if (!CertStrToNameW(Encoding, name, CERT_X500_NAME_STR, nullptr, encoded.data(), &size, nullptr))
            Fail("Fixture subject");
        return encoded;
    }

    struct EphemeralSigner
    {
        NCRYPT_PROV_HANDLE provider = 0;
        NCRYPT_KEY_HANDLE key = 0;
        Certificate certificate;

        explicit EphemeralSigner(bool tsa, bool expired = false)
        {
            Require(NCryptOpenStorageProvider(&provider, MS_KEY_STORAGE_PROVIDER, 0) == ERROR_SUCCESS,
                    "Fixture ephemeral provider");
            // A null key name creates an ephemeral key, not a persisted key/container.
            Require(NCryptCreatePersistedKey(provider, &key, NCRYPT_RSA_ALGORITHM, nullptr, 0, 0) == ERROR_SUCCESS,
                    "Fixture ephemeral key");
            DWORD bits = 2048;
            Require(NCryptSetProperty(key, NCRYPT_LENGTH_PROPERTY, reinterpret_cast<BYTE*>(&bits), sizeof(bits), 0) == ERROR_SUCCESS &&
                        NCryptFinalizeKey(key, 0) == ERROR_SUCCESS,
                    "Fixture key finalization");
            auto name = Name(L"CN=Untrusted Test Signer,O=Microsoft Corporation");
            CERT_NAME_BLOB subject{ static_cast<DWORD>(name.size()), name.data() };
            std::string usageOid = tsa ? szOID_PKIX_KP_TIMESTAMP_SIGNING : szOID_PKIX_KP_CODE_SIGNING;
            char* oid = usageOid.data();
            CERT_ENHKEY_USAGE usage{ 1, &oid };
            auto encodedUsage = Encode(X509_ENHANCED_KEY_USAGE, &usage);
            char ekuOid[] = szOID_ENHANCED_KEY_USAGE;
            CERT_EXTENSION eku{ ekuOid, TRUE,
                                { static_cast<DWORD>(encodedUsage.size()), encodedUsage.data() } };
            char signatureOid[] = szOID_RSA_SHA256RSA;
            CRYPT_ALGORITHM_IDENTIFIER algorithm{ signatureOid, {} };
            DWORD publicSize = 0;
            if (!CryptExportPublicKeyInfo(key, CERT_NCRYPT_KEY_SPEC, Encoding, nullptr, &publicSize))
                Fail("Fixture public key size");
            std::vector<BYTE> publicKey(publicSize);
            auto* publicInfo = reinterpret_cast<CERT_PUBLIC_KEY_INFO*>(publicKey.data());
            if (!CryptExportPublicKeyInfo(key, CERT_NCRYPT_KEY_SPEC, Encoding, publicInfo, &publicSize))
                Fail("Fixture public key");
            BYTE serial = 1;
            CERT_INFO info{};
            info.dwVersion = CERT_V3;
            info.SerialNumber = { 1, &serial };
            info.SignatureAlgorithm = algorithm;
            info.Subject = subject;
            info.Issuer = subject;
            info.SubjectPublicKeyInfo = *publicInfo;
            info.cExtension = 1;
            info.rgExtension = &eku;
            GetSystemTimeAsFileTime(&info.NotBefore);
            ULARGE_INTEGER expiry{};
            expiry.LowPart = info.NotBefore.dwLowDateTime;
            expiry.HighPart = info.NotBefore.dwHighDateTime;
            if (expired)
            {
                expiry.QuadPart -= 2 * 86400ULL * 10000000;
                info.NotBefore = { expiry.LowPart, expiry.HighPart };
            }
            expiry.QuadPart += 86400ULL * 10000000;
            info.NotAfter = { expiry.LowPart, expiry.HighPart };
            DWORD certificateSize = 0;
            if (!CryptSignAndEncodeCertificate(key, CERT_NCRYPT_KEY_SPEC, X509_ASN_ENCODING, X509_CERT_TO_BE_SIGNED, &info,
                                               &algorithm, nullptr, nullptr, &certificateSize))
                Fail("Fixture signed certificate size");
            std::vector<BYTE> encoded(certificateSize);
            if (!CryptSignAndEncodeCertificate(key, CERT_NCRYPT_KEY_SPEC, X509_ASN_ENCODING, X509_CERT_TO_BE_SIGNED, &info,
                                               &algorithm, nullptr, encoded.data(), &certificateSize))
                Fail("Fixture signed certificate");
            certificate.value = CertCreateCertificateContext(Encoding, encoded.data(), certificateSize);
            if (!certificate.value)
                Fail("Fixture ephemeral certificate");
        }

        ~EphemeralSigner()
        {
            if (key)
                NCryptFreeObject(key);
            if (provider)
                NCryptFreeObject(provider);
        }

        EphemeralSigner(const EphemeralSigner&) = delete;
        EphemeralSigner& operator=(const EphemeralSigner&) = delete;

        std::vector<BYTE> Sign(const std::vector<BYTE>& content, bool detached = true, DWORD count = 1,
                               const char* hash = szOID_NIST_sha256, const char* inner = szOID_PKCS_7_DATA,
                               const CRYPT_ATTRIBUTES* unsignedAttributes = nullptr)
        {
            std::string hashOid = hash;
            std::string innerOid = inner;
            CMSG_SIGNER_ENCODE_INFO signer{};
            signer.cbSize = sizeof(signer);
            signer.pCertInfo = certificate.value->pCertInfo;
            signer.hCryptProv = key;
            signer.dwKeySpec = CERT_NCRYPT_KEY_SPEC;
            signer.HashAlgorithm.pszObjId = hashOid.data();
            if (unsignedAttributes)
            {
                signer.cUnauthAttr = unsignedAttributes->cAttr;
                signer.rgUnauthAttr = unsignedAttributes->rgAttr;
            }
            std::vector<CMSG_SIGNER_ENCODE_INFO> signers(count, signer);
            CERT_BLOB embedded{ certificate.value->cbCertEncoded, certificate.value->pbCertEncoded };
            CMSG_SIGNED_ENCODE_INFO parameters{};
            parameters.cbSize = sizeof(parameters);
            parameters.cSigners = count;
            parameters.rgSigners = signers.data();
            parameters.cCertEncoded = 1;
            parameters.rgCertEncoded = &embedded;
            Message message(CryptMsgOpenToEncode(Encoding, CMSG_CMS_ENCAPSULATED_CONTENT_FLAG | CMSG_AUTHENTICATED_ATTRIBUTES_FLAG |
                                                              (detached ? CMSG_DETACHED_FLAG : 0), CMSG_SIGNED,
                                                &parameters, OidIs(inner, szOID_PKCS_7_DATA) ? nullptr : innerOid.data(), nullptr));
            if (!message.value)
                Fail("Fixture CMS encoder");
            if (!CryptMsgUpdate(message.value, content.data(), static_cast<DWORD>(content.size()), TRUE))
                Fail("Fixture CMS signature");
            return MessageParameter(message.value, CMSG_CONTENT_PARAM);
        }
    };

    void PolicyTests()
    {
        Expect(std::string_view(ReleaseTrustPolicy) == "microsoft-production-v1", "One production trust policy");
        Expect((MachineChainFlags & CERT_CHAIN_REVOCATION_CHECK_CACHE_ONLY) != 0, "Offline revocation retrieval");
        Expect((MachineChainFlags & CERT_CHAIN_CACHE_ONLY_URL_RETRIEVAL) != 0, "Offline chain retrieval");
        Expect((MachineChainFlags & CERT_CHAIN_ENABLE_PEER_TRUST) == 0, "No peer trust");
        Expect((MicrosoftRootFlags & MICROSOFT_ROOT_CERT_CHAIN_POLICY_CHECK_APPLICATION_ROOT_FLAG) != 0, "Application root required");
        Expect((MicrosoftRootFlags & MICROSOFT_ROOT_CERT_CHAIN_POLICY_ENABLE_TEST_ROOT_FLAG) == 0, "Test roots disabled");
        CheckTrustBits(0, "test");
        CheckTrustBits(OfflineErrors, "test");
        for (unsigned bit = 0; bit < 32; ++bit)
        {
            const DWORD flag = DWORD{ 1 } << bit;
            if ((flag & OfflineErrors) == 0)
                Reject([&] { CheckTrustBits(OfflineErrors | flag, "test"); });
        }
        for (const DWORD result : { static_cast<DWORD>(CERT_E_REVOKED), static_cast<DWORD>(CRYPT_E_REVOKED),
                                    static_cast<DWORD>(NTE_BAD_ALGID), static_cast<DWORD>(CERT_E_UNTRUSTEDROOT) })
            Reject([&] { CheckRevocationResult(result, "test"); });
        CheckRevocationResult(static_cast<DWORD>(CRYPT_E_REVOCATION_OFFLINE), "test");
        CheckRevocationResult(static_cast<DWORD>(CRYPT_E_NO_REVOCATION_CHECK), "test");
        Expect(!StrongHashName(szOID_OIWSEC_sha1) && !StrongHashName(szOID_RSA_MD5), "Weak timestamp hashes rejected");
        Expect(StrongHashName(szOID_NIST_sha256) && StrongHashName(szOID_NIST_sha384) && StrongHashName(szOID_NIST_sha512),
               "SHA-2 timestamp hashes accepted");
        CERT_CONTEXT certificate{};
        CERT_CHAIN_ELEMENT element{};
        element.pCertContext = &certificate;
        element.TrustStatus.dwErrorStatus = CERT_TRUST_IS_REVOKED;
        PCERT_CHAIN_ELEMENT elements[]{ &element };
        CERT_SIMPLE_CHAIN simple{};
        simple.cElement = 1;
        simple.rgpElement = elements;
        PCERT_SIMPLE_CHAIN chains[]{ &simple };
        CERT_CHAIN_CONTEXT chain{};
        chain.cChain = 1;
        chain.rgpChain = chains;
        chain.TrustStatus.dwErrorStatus = OfflineErrors;
        Reject([&] { InspectChain(&chain, "test"); }, "revoked");
        Reject([&] { InspectChain(&chain, "test", true); }, "revoked");
        element.TrustStatus.dwErrorStatus = CERT_TRUST_IS_EXPLICIT_DISTRUST;
        Reject([&] { InspectChain(&chain, "test", true); }, "distrusted");
        element.TrustStatus.dwErrorStatus = CERT_TRUST_IS_NOT_TIME_VALID;
        Reject([&] { InspectChain(&chain, "test"); }, "trust errors");
        InspectChain(&chain, "test", true);
    }

    void AttributeTests()
    {
        BYTE byte = 0;
        CRYPT_ATTR_BLOB value{ 1, &byte };
        std::string standardOid = SignatureTimestampOid;
        char microsoftOid[] = szOID_RFC3161_counterSign;
        char signingTimeOid[] = szOID_RSA_signingTime;
        char counterSignOid[] = szOID_RSA_counterSign;
        CRYPT_ATTRIBUTE attribute{ standardOid.data(), 1, &value };
        CRYPT_ATTRIBUTES attributes{ 1, &attribute };
        Expect(&TimestampAttribute(attributes) == &value, "Standard timestamp attribute");
        attribute.pszObjId = microsoftOid;
        Expect(&TimestampAttribute(attributes) == &value, "Microsoft RFC 3161 attribute");
        attribute.pszObjId = signingTimeOid;
        Reject([&] { TimestampAttribute(attributes); }, "requires an RFC 3161");
        attribute.pszObjId = counterSignOid;
        Reject([&] { TimestampAttribute(attributes); }, "Unsupported or ambiguous");
        attribute.pszObjId = standardOid.data();
        attribute.cValue = 2;
        Reject([&] { TimestampAttribute(attributes); }, "unambiguous");
        attribute.cValue = 0;
        Reject([&] { TimestampAttribute(attributes); }, "unambiguous");
        attribute.cValue = 1;
        CRYPT_ATTRIBUTE duplicate[]{ attribute, attribute };
        duplicate[1].pszObjId = microsoftOid;
        CRYPT_ATTRIBUTES duplicates{ 2, duplicate };
        Reject([&] { TimestampAttribute(duplicates); }, "unambiguous");
        value.cbData = static_cast<DWORD>(SignatureLimit + 1);
        Reject([&] { TimestampAttribute(attributes); }, "exceeds bounds");
        attributes.cAttr = 0;
        Reject([&] { TimestampAttribute(attributes); }, "requires an RFC 3161");
    }

    void SubjectAndEkuTests()
    {
        CERT_INFO info{};
        CERT_CONTEXT certificate{};
        certificate.pCertInfo = &info;
        for (const auto* text : { L"CN=Microsoft Corporation", L"O=Not Microsoft", L"O=Microsoft Corporation Extra",
                                  L"O=Microsoft Corporation,O=Microsoft Corporation", L"O=Microsoft Corporation,O=Other" })
        {
            auto subject = Name(text);
            info.Subject = { static_cast<DWORD>(subject.size()), subject.data() };
            Reject([&] { CheckMicrosoftOrganization(&certificate); });
        }
        auto subject = Name(L"CN=Anything,O=Microsoft Corporation");
        info.Subject = { static_cast<DWORD>(subject.size()), subject.data() };
        CheckMicrosoftOrganization(&certificate);
        Reject([&] { CheckExplicitEku(&certificate, false); }, "explicitly");
        char timestampOid[] = szOID_PKIX_KP_TIMESTAMP_SIGNING;
        char* oid = timestampOid;
        CERT_ENHKEY_USAGE usage{ 1, &oid };
        auto encoded = Encode(X509_ENHANCED_KEY_USAGE, &usage);
        char ekuOid[] = szOID_ENHANCED_KEY_USAGE;
        CERT_EXTENSION extension{ ekuOid, TRUE,
                                  { static_cast<DWORD>(encoded.size()), encoded.data() } };
        info.cExtension = 1;
        info.rgExtension = &extension;
        CheckExplicitEku(&certificate, true);
        Reject([&] { CheckExplicitEku(&certificate, false); }, "code-signing");
        extension.fCritical = FALSE;
        Reject([&] { CheckExplicitEku(&certificate, true); }, "critical");
        extension.fCritical = TRUE;
        CERT_EXTENSION duplicate[]{ extension, extension };
        info.cExtension = 2;
        info.rgExtension = duplicate;
        Reject([&] { CheckExplicitEku(&certificate, true); }, "Ambiguous");
    }

    void CmsTests()
    {
        const std::vector<BYTE> content{ 'e', 'x', 'a', 'c', 't' };
        Reject([&] { VerifyDetachedReleaseSignature({}, content); }, "content");
        Reject([&] { VerifyDetachedReleaseSignature(std::vector<BYTE>(ContentLimit + 1), content); }, "64 KiB");
        Reject([&] { VerifyDetachedReleaseSignature(content, std::vector<BYTE>(SignatureLimit + 1)); }, "1 MiB");
        Reject([&] { VerifyDetachedReleaseSignature(content, {}); }, "signature");
        for (const std::vector<BYTE>& invalid : { std::vector<BYTE>{ 0x30, 0x80, 0, 0 },
                                                 std::vector<BYTE>{ 0x30, 0x81, 1, 0 },
                                                 std::vector<BYTE>{ 0x30, 0x82, 0, 0x80 },
                                                 std::vector<BYTE>{ 0x30, 0xff },
                                                 std::vector<BYTE>{ 0x30, 0x88, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff } })
            Reject([&] { CheckCmsEnvelope(invalid, true); });
        EphemeralSigner signer(false);
        const auto signature = signer.Sign(content);
        CheckCmsEnvelope(signature, true);
        Reject([&] { VerifyDetachedReleaseSignature(content, signature); }, "requires an RFC 3161");
        auto modified = content;
        modified[0] ^= 1;
        Reject([&] { VerifyDetachedReleaseSignature(modified, signature); }, "exact content");
        Reject([&] { VerifyDetachedReleaseSignature(content, signer.Sign(content, false)); }, "trailing");
        Reject([&] { VerifyDetachedReleaseSignature(content, signer.Sign(content, true, 2)); }, "exactly one signer");
        Reject([&] { VerifyDetachedReleaseSignature(content, signer.Sign(content, true, 1, szOID_OIWSEC_sha1)); }, "SHA-256");
        auto trailing = signature;
        trailing.push_back(0);
        Reject([&] { VerifyDetachedReleaseSignature(content, trailing); }, "trailing");
        for (size_t size = 0; size < signature.size(); ++size)
            Reject([&] { CheckCmsEnvelope(std::span(signature).first(size), true); });
        std::mt19937 random(12345);
        for (unsigned i = 0; i < 5000; ++i)
        {
            auto mutated = signature;
            mutated[random() % mutated.size()] ^= static_cast<BYTE>(1 + random() % 255);
            Reject([&] { VerifyDetachedReleaseSignature(content, mutated); });
        }
        auto store = MemoryStore();
        AddCertificate(store.value, signer.certificate.value);
        FILETIME now{};
        GetSystemTimeAsFileTime(&now);
        Reject([&] { VerifyMachineSigner(signer.certificate.value, store.value, now, false); }, "chain trust errors");
        auto chain = MachineChain(signer.certificate.value, store.value, &now, false);
        Reject([&] { CheckChainPolicy(chain.value, CERT_CHAIN_POLICY_MICROSOFT_ROOT, MicrosoftRootFlags, nullptr, "test"); });
        EphemeralSigner expired(false, true);
        FILETIME signingTime = expired.certificate.value->pCertInfo->NotBefore;
        auto historical = MachineChain(expired.certificate.value, store.value, &signingTime, false);
        auto current = MachineChain(expired.certificate.value, store.value, nullptr, false);
        Expect((historical.value->TrustStatus.dwErrorStatus & CERT_TRUST_IS_NOT_TIME_VALID) == 0,
               "Code-signing chain evaluates expiration at authenticated historical time");
        Expect((current.value->TrustStatus.dwErrorStatus & CERT_TRUST_IS_NOT_TIME_VALID) != 0,
               "Expired fixture really is expired now");
    }

    void TimestampTests()
    {
        EphemeralSigner release(false);
        EphemeralSigner tsa(true);
        const std::vector<BYTE> content{ 't', 'i', 'm', 'e' };
        auto signature = release.Sign(content);
        auto decoded = DecodeMessage(signature, &content);
        auto signerBytes = MessageParameter(decoded.value, CMSG_CMS_SIGNER_INFO_PARAM, sizeof(CMSG_CMS_SIGNER_INFO));
        auto* signer = reinterpret_cast<CMSG_CMS_SIGNER_INFO*>(signerBytes.data());
        auto certificates = MessageCertificates(decoded.value);
        BYTE serial = 1;
        char policyOid[] = "1.2.3.4.5";
        char sha256Oid[] = szOID_NIST_sha256;
        char sha1Oid[] = szOID_OIWSEC_sha1;
        char microsoftTimestampOid[] = szOID_RFC3161_counterSign;
        std::string standardTimestampOid = SignatureTimestampOid;
        CRYPT_TIMESTAMP_INFO info{};
        info.dwVersion = TIMESTAMP_VERSION;
        info.pszTSAPolicyId = policyOid;
        info.HashAlgorithm.pszObjId = sha256Oid;
        std::array<BYTE, 32> imprint{};
        DWORD size = static_cast<DWORD>(imprint.size());
        if (!CryptHashCertificate2(BCRYPT_SHA256_ALGORITHM, 0, nullptr, signer->EncryptedHash.pbData,
                                  signer->EncryptedHash.cbData, imprint.data(), &size))
            Fail("Fixture timestamp imprint");
        info.HashedMessage = { size, imprint.data() };
        info.SerialNumber = { 1, &serial };
        GetSystemTimeAsFileTime(&info.ftTime);
        auto token = tsa.Sign(Encode(TIMESTAMP_INFO, &info), false, 1, szOID_NIST_sha256, szOID_TIMESTAMP_TOKEN);
        CheckCmsEnvelope(token, false);
        Timestamp verified;
        Certificate actualTsa;
        Store embedded;
        Expect(CryptVerifyTimeStampSignature(token.data(), static_cast<DWORD>(token.size()), signer->EncryptedHash.pbData,
                                            signer->EncryptedHash.cbData, certificates.value, &verified.value, &actualTsa.value,
                                            &embedded.value) != FALSE,
               "RFC 3161 fixture crypto verifies over the actual CMS signature value");
        CRYPT_ATTR_BLOB value{ static_cast<DWORD>(token.size()), token.data() };
        CRYPT_ATTRIBUTE attribute{ standardTimestampOid.data(), 1, &value };
        signer->UnauthAttrs = { 1, &attribute };
        Reject([&] { VerifyTimestamp(*signer, certificates.value); }, "chain trust errors");
        const CRYPT_ATTRIBUTES unsignedAttributes{ 1, &attribute };
        auto timestamped = release.Sign(content, true, 1, szOID_NIST_sha256, szOID_PKCS_7_DATA, &unsignedAttributes);
        Reject([&] { VerifyDetachedReleaseSignature(content, timestamped); }, "chain trust errors");
        attribute.pszObjId = microsoftTimestampOid;
        Reject([&] { VerifyTimestamp(*signer, certificates.value); }, "chain trust errors");
        signer->EncryptedHash.pbData[0] ^= 1;
        Reject([&] { VerifyTimestamp(*signer, certificates.value); }, "message imprint is invalid");
        signer->EncryptedHash.pbData[0] ^= 1;
        info.HashAlgorithm.pszObjId = sha1Oid;
        std::array<BYTE, 20> weakImprint{};
        size = static_cast<DWORD>(weakImprint.size());
        if (!CryptHashCertificate2(BCRYPT_SHA1_ALGORITHM, 0, nullptr, signer->EncryptedHash.pbData,
                                  signer->EncryptedHash.cbData, weakImprint.data(), &size))
            Fail("Fixture weak timestamp imprint");
        info.HashedMessage = { size, weakImprint.data() };
        auto weakToken = tsa.Sign(Encode(TIMESTAMP_INFO, &info), false, 1, szOID_NIST_sha256, szOID_TIMESTAMP_TOKEN);
        value = { static_cast<DWORD>(weakToken.size()), weakToken.data() };
        Reject([&] { VerifyTimestamp(*signer, certificates.value); }, "message imprint must use SHA-256");
        auto multiple = tsa.Sign(Encode(TIMESTAMP_INFO, &info), false, 2, szOID_NIST_sha256, szOID_TIMESTAMP_TOKEN);
        value = { static_cast<DWORD>(multiple.size()), multiple.data() };
        Reject([&] { VerifyTimestamp(*signer, certificates.value); }, "exactly one signer");
    }
}

int wmain(int argc, wchar_t** argv)
{
    try
    {
        PolicyTests();
        AttributeTests();
        SubjectAndEkuTests();
        for (int i = 1; i < argc; ++i)
        {
            Handle file(CreateFileW(argv[i], GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
            if (!file)
                Fail("Cannot open supplied Authenticode fixture");
            VerifyAuthenticodeReleaseSignature(file.get(), argv[i]);
            ++passed;
            std::array<wchar_t, 32768> executable{};
            const DWORD count = GetModuleFileNameW(nullptr, executable.data(), static_cast<DWORD>(executable.size()));
            Expect(count && count < executable.size(), "Resolve unsigned test executable");
            Handle unsignedFile(CreateFileW(executable.data(), GENERIC_READ, FILE_SHARE_READ, nullptr,
                                            OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
            if (!unsignedFile)
                Fail("Cannot open unsigned held-handle fixture");
            Reject([&] { VerifyAuthenticodeReleaseSignature(unsignedFile.get(), argv[i]); }, "Authenticode verification failed");
            VerifyAuthenticodeReleaseSignature(file.get(), executable.data());
            ++passed;
        }
        CmsTests();
        TimestampTests();
        std::cout << "Signature trust checks passed: " << passed << '\n';
        return 0;
    }
    catch (const Error& error)
    {
        std::cerr << error.what() << " (error=" << std::hex << error.code << ")\n";
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << '\n';
    }
    return 1;
}
