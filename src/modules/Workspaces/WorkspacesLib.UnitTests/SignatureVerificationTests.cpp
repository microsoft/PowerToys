// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include <WorkspacesLib/SignatureVerification.h>
#include <WorkspacesLib/PendingLaunchApproval.h>
#include <WorkspacesLib/LauncherUiMessage.h>
#include <WorkspacesLauncher/pch.h>
#include <WorkspacesLauncher/AppLauncher.h>
#include <WorkspacesLauncher/RegistryUtils.h>
#include <mscat.h>
#include <ncrypt.h>
#include <softpub.h>
#include <wincrypt.h>

#include <algorithm>
#include <array>
#include <filesystem>
#include <fstream>
#include <functional>
#include <future>

#pragma comment(lib, "ncrypt.lib")

namespace WorkspacesLibUnitTests::TrustApi
{
    using Microsoft::VisualStudio::CppUnitTestFramework::Assert;

    struct Fixture;
    thread_local Fixture* active{};

    struct Fixture
    {
        wil::unique_cert_context certificate;
        wil::unique_hcertstore roots;
        wil::unique_any<HCERTCHAINENGINE, decltype(&::CertFreeCertificateChainEngine), ::CertFreeCertificateChainEngine> engine;
        wil::unique_cert_chain_context chain;
        CERT_CHAIN_CONTEXT chainView{};
        CRYPT_PROVIDER_DATA provider{};
        CRYPT_PROVIDER_CERT providerCertificate{};
        CRYPT_PROVIDER_SGNR signer{};
        CRYPT_PROVIDER_SGNR timestamp{};
        std::vector<LONG> embedded{ ERROR_SUCCESS };
        std::vector<LONG> catalogs;
        std::vector<std::wstring> algorithms;
        std::wstring catalogAlgorithm{ L"SHA256" };
        std::wstring finalPath;
        DWORD signerErrors{};
        DWORD timestampErrors{};
        bool missingProvider{};
        bool missingSigner{};
        bool chainFailure{};
        bool catalogFailure{};
        bool checkingTimestamp{};
        size_t catalogIndex{};
        unsigned verified{};
        unsigned closed{};
        unsigned chainCalls{};
        unsigned policyCalls{};
        unsigned adminsOpened{};
        unsigned adminsClosed{};
        unsigned catalogsOpened{};
        unsigned catalogsClosed{};

        static constexpr FILETIME ShiftDays(FILETIME time, int days)
        {
            const auto ticks = static_cast<LONGLONG>((static_cast<ULONGLONG>(time.dwHighDateTime) << 32) | time.dwLowDateTime);
            const auto shifted = static_cast<ULONGLONG>(ticks + days * 864000000000LL);
            return { static_cast<DWORD>(shifted), static_cast<DWORD>(shifted >> 32) };
        }

        explicit Fixture(bool trustedRoot = true, int validityOffsetDays = 0)
        {
            Assert::IsNull(active);
            wil::unique_ncrypt_prov keyProvider;
            wil::unique_ncrypt_key key;
            Assert::AreEqual(static_cast<SECURITY_STATUS>(ERROR_SUCCESS), NCryptOpenStorageProvider(keyProvider.put(), MS_KEY_STORAGE_PROVIDER, 0));
            Assert::AreEqual(static_cast<SECURITY_STATUS>(ERROR_SUCCESS), NCryptCreatePersistedKey(keyProvider.get(), key.put(), NCRYPT_RSA_ALGORITHM, nullptr, 0, 0));
            Assert::AreEqual(static_cast<SECURITY_STATUS>(ERROR_SUCCESS), NCryptFinalizeKey(key.get(), NCRYPT_SILENT_FLAG));

            DWORD nameSize{};
            constexpr auto name = L"CN=Workspaces signature unit test";
            Assert::IsTrue(CertStrToNameW(X509_ASN_ENCODING, name, CERT_X500_NAME_STR, nullptr, nullptr, &nameSize, nullptr) != FALSE);
            std::vector<BYTE> encodedName(nameSize);
            Assert::IsTrue(CertStrToNameW(X509_ASN_ENCODING, name, CERT_X500_NAME_STR, nullptr, encodedName.data(), &nameSize, nullptr) != FALSE);
            CERT_NAME_BLOB subject{ nameSize, encodedName.data() };
            char algorithm[] = szOID_RSA_SHA256RSA;
            CRYPT_ALGORITHM_IDENTIFIER signatureAlgorithm{ algorithm, {} };
            GetSystemTimeAsFileTime(&signer.sftVerifyAsOf);
            const auto from = ShiftDays(signer.sftVerifyAsOf, validityOffsetDays - 1);
            const auto until = ShiftDays(signer.sftVerifyAsOf, validityOffsetDays + 1);
            SYSTEMTIME start{}, end{};
            Assert::IsTrue(FileTimeToSystemTime(&from, &start) != FALSE);
            Assert::IsTrue(FileTimeToSystemTime(&until, &end) != FALSE);
            certificate.reset(CertCreateSelfSignCertificate(key.get(), &subject, CERT_CREATE_SELFSIGN_NO_KEY_INFO, nullptr, &signatureAlgorithm, &start, &end, nullptr));
            Assert::IsTrue(static_cast<bool>(certificate));

            roots.reset(CertOpenStore(CERT_STORE_PROV_MEMORY, 0, 0, 0, nullptr));
            Assert::IsTrue(static_cast<bool>(roots));
            if (trustedRoot)
            {
                Assert::IsTrue(CertAddCertificateContextToStore(roots.get(), certificate.get(), CERT_STORE_ADD_ALWAYS, nullptr) != FALSE);
            }
            CERT_CHAIN_ENGINE_CONFIG configuration{};
            configuration.cbSize = sizeof(configuration);
            configuration.hExclusiveRoot = roots.get();
            Assert::IsTrue(CertCreateCertificateChainEngine(&configuration, engine.put()) != FALSE);

            providerCertificate.cbStruct = sizeof(providerCertificate);
            providerCertificate.pCert = certificate.get();
            signer.cbStruct = sizeof(signer);
            signer.csCertChain = 1;
            signer.pasCertChain = &providerCertificate;
            timestamp = signer;
            active = this;
        }

        ~Fixture()
        {
            active = nullptr;
        }

        SignatureVerification::LaunchTarget Verify(const std::wstring& path)
        {
            auto result = SignatureVerification::Verify(path);
            Assert::AreEqual(verified, closed, L"Every WinVerifyTrust state must be closed, including failures.");
            Assert::IsFalse(static_cast<bool>(chain), L"Machine/timestamp chains must be released.");
            Assert::AreEqual(adminsOpened, adminsClosed);
            Assert::AreEqual(catalogsOpened, catalogsClosed);
            return result;
        }
    };

    DWORD WINAPI FinalPath(HANDLE file, LPWSTR buffer, DWORD size, DWORD flags)
    {
        if (!active || active->finalPath.empty())
        {
            return ::GetFinalPathNameByHandleW(file, buffer, size, flags);
        }
        Assert::AreEqual(static_cast<DWORD>(FILE_NAME_NORMALIZED | VOLUME_NAME_DOS), flags);
        const auto& path = active->finalPath;
        if (size <= path.size())
        {
            return static_cast<DWORD>(path.size() + 1);
        }
        std::copy(path.c_str(), path.c_str() + path.size() + 1, buffer);
        return static_cast<DWORD>(path.size());
    }

    LONG WINAPI VerifyTrust(HWND window, GUID* action, LPVOID information)
    {
        if (!active)
        {
            return ::WinVerifyTrust(window, action, information);
        }
        auto& data = *static_cast<WINTRUST_DATA*>(information);
        GUID expectedAction = WINTRUST_ACTION_GENERIC_VERIFY_V2;
        Assert::IsTrue(*action == expectedAction);
        Assert::AreEqual(static_cast<DWORD>(WTD_UI_NONE), data.dwUIChoice);
        Assert::AreEqual(static_cast<DWORD>(WTD_CACHE_ONLY_URL_RETRIEVAL | WTD_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT | WTD_DISABLE_MD2_MD4), data.dwProvFlags);
        if (data.dwStateAction == WTD_STATEACTION_CLOSE)
        {
            Assert::IsTrue(data.hWVTStateData == active);
            ++active->closed;
            data.hWVTStateData = nullptr;
            return ERROR_SUCCESS;
        }
        Assert::AreEqual(static_cast<DWORD>(WTD_STATEACTION_VERIFY), data.dwStateAction);
        data.hWVTStateData = active;
        ++active->verified;
        if (data.dwUnionChoice == WTD_CHOICE_FILE)
        {
            Assert::IsNotNull(data.pFile);
            Assert::IsTrue(data.pFile->hFile != INVALID_HANDLE_VALUE);
            Assert::IsNotNull(data.pSignatureSettings);
            auto& signatures = *data.pSignatureSettings;
            Assert::IsTrue((signatures.dwFlags & WSS_VERIFY_SPECIFIC) != 0);
            Assert::IsTrue(signatures.dwIndex < active->embedded.size());
            if (signatures.dwIndex == 0)
            {
                Assert::IsTrue((signatures.dwFlags & WSS_GET_SECONDARY_SIG_COUNT) != 0);
                signatures.cSecondarySigs = static_cast<DWORD>(active->embedded.size() - 1);
            }
            return active->embedded[signatures.dwIndex];
        }
        Assert::AreEqual(static_cast<DWORD>(WTD_CHOICE_CATALOG), data.dwUnionChoice);
        Assert::IsNotNull(data.pCatalog);
        Assert::IsTrue(data.pCatalog->hCatAdmin == active);
        Assert::AreEqual(L"C:\\WorkspacesTest\\signature.cat", data.pCatalog->pcwszCatalogFilePath);
        Assert::AreEqual(L"01AB00FF", data.pCatalog->pcwszMemberTag);
        Assert::AreEqual(static_cast<DWORD>(4), data.pCatalog->cbCalculatedFileHash);
        Assert::IsTrue(data.pCatalog->hMemberFile != INVALID_HANDLE_VALUE);
        return active->catalogs.at(active->catalogIndex);
    }

    CRYPT_PROVIDER_DATA* WINAPI Provider(HANDLE state)
    {
        if (!active)
        {
            return ::WTHelperProvDataFromStateData(state);
        }
        Assert::IsTrue(state == active);
        return active->missingProvider ? nullptr : &active->provider;
    }

    CRYPT_PROVIDER_SGNR* WINAPI Signer(CRYPT_PROVIDER_DATA* provider, DWORD index, BOOL counterSigner, DWORD counterIndex)
    {
        if (!active)
        {
            return ::WTHelperGetProvSignerFromChain(provider, index, counterSigner, counterIndex);
        }
        Assert::IsTrue(provider == &active->provider);
        Assert::AreEqual(static_cast<DWORD>(0), index);
        Assert::IsFalse(counterSigner != FALSE);
        return active->missingSigner ? nullptr : &active->signer;
    }

    BOOL WINAPI CertificateChain(HCERTCHAINENGINE engine, PCCERT_CONTEXT certificate, LPFILETIME time, HCERTSTORE store,
                                PCERT_CHAIN_PARA parameters, DWORD flags, LPVOID reserved, PCCERT_CHAIN_CONTEXT* chain)
    {
        if (!active)
        {
            return ::CertGetCertificateChain(engine, certificate, time, store, parameters, flags, reserved, chain);
        }
        Assert::IsTrue(engine == HCCE_LOCAL_MACHINE);
        Assert::AreEqual(static_cast<DWORD>(CERT_CHAIN_CACHE_ONLY_URL_RETRIEVAL | CERT_CHAIN_REVOCATION_CHECK_CACHE_ONLY | CERT_CHAIN_REVOCATION_CHECK_CHAIN_EXCLUDE_ROOT), flags);
        Assert::AreEqual(static_cast<DWORD>(USAGE_MATCH_TYPE_AND), parameters->RequestedUsage.dwType);
        Assert::AreEqual(static_cast<DWORD>(1), parameters->RequestedUsage.Usage.cUsageIdentifier);
        const auto usage = parameters->RequestedUsage.Usage.rgpszUsageIdentifier[0];
        active->checkingTimestamp = strcmp(usage, szOID_PKIX_KP_TIMESTAMP_SIGNING) == 0;
        Assert::AreEqual(active->checkingTimestamp ? szOID_PKIX_KP_TIMESTAMP_SIGNING : szOID_PKIX_KP_CODE_SIGNING, usage);
        const auto expectedTime = active->checkingTimestamp ? active->timestamp.sftVerifyAsOf : active->signer.sftVerifyAsOf;
        Assert::AreEqual(expectedTime.dwLowDateTime, time->dwLowDateTime);
        Assert::AreEqual(expectedTime.dwHighDateTime, time->dwHighDateTime);
        Assert::IsFalse(static_cast<bool>(active->chain));
        ++active->chainCalls;
        if (active->chainFailure)
        {
            SetLastError(ERROR_INVALID_DATA);
            return FALSE;
        }
        // Real Windows chain/policy evaluation with a private memory-only root, never machine trust.
        if (!::CertGetCertificateChain(active->engine.get(), certificate, time, store, parameters, flags, reserved, active->chain.put()))
        {
            return FALSE;
        }
        active->chainView = *active->chain.get();
        active->chainView.TrustStatus.dwErrorStatus |= active->checkingTimestamp ? active->timestampErrors : active->signerErrors;
        *chain = &active->chainView;
        return TRUE;
    }

    void WINAPI FreeChain(PCCERT_CHAIN_CONTEXT chain)
    {
        if (active && chain == &active->chainView)
        {
            active->chain.reset();
            return;
        }
        ::CertFreeCertificateChain(chain);
    }

    BOOL WINAPI ChainPolicy(LPCSTR policy, PCCERT_CHAIN_CONTEXT chain, PCERT_CHAIN_POLICY_PARA parameters, PCERT_CHAIN_POLICY_STATUS status)
    {
        if (active)
        {
            Assert::IsTrue(chain == &active->chainView);
            Assert::IsTrue(policy == (active->checkingTimestamp ? CERT_CHAIN_POLICY_AUTHENTICODE_TS : CERT_CHAIN_POLICY_AUTHENTICODE));
            Assert::AreEqual(static_cast<DWORD>(0), parameters->dwFlags);
            ++active->policyCalls;
        }
        return ::CertVerifyCertificateChainPolicy(policy, chain, parameters, status);
    }

    BOOL WINAPI AcquireCatalog(HCATADMIN* admin, const GUID* subsystem, PCWSTR algorithm, PCCERT_STRONG_SIGN_PARA policy, DWORD flags)
    {
        if (!active)
        {
            return ::CryptCATAdminAcquireContext2(admin, subsystem, algorithm, policy, flags);
        }
        active->algorithms.emplace_back(algorithm);
        if (active->catalogFailure)
        {
            SetLastError(ERROR_ACCESS_DENIED);
            return FALSE;
        }
        *admin = active;
        ++active->adminsOpened;
        return TRUE;
    }

    BOOL WINAPI ReleaseCatalogAdmin(HCATADMIN admin, DWORD flags)
    {
        if (!active)
        {
            return ::CryptCATAdminReleaseContext(admin, flags);
        }
        Assert::IsTrue(admin == active);
        ++active->adminsClosed;
        return TRUE;
    }

    BOOL WINAPI CatalogHash(HCATADMIN admin, HANDLE file, DWORD* size, BYTE* hash, DWORD flags)
    {
        if (!active)
        {
            return ::CryptCATAdminCalcHashFromFileHandle2(admin, file, size, hash, flags);
        }
        Assert::IsTrue(admin == active);
        BY_HANDLE_FILE_INFORMATION information{};
        Assert::IsTrue(GetFileInformationByHandle(file, &information) != FALSE);
        constexpr std::array<BYTE, 4> bytes{ 0x01, 0xAB, 0x00, 0xFF };
        if (hash)
        {
            Assert::AreEqual(static_cast<DWORD>(bytes.size()), *size);
            std::copy(bytes.begin(), bytes.end(), hash);
        }
        *size = static_cast<DWORD>(bytes.size());
        return TRUE;
    }

    HCATINFO WINAPI EnumerateCatalog(HCATADMIN admin, BYTE* hash, DWORD size, DWORD flags, HCATINFO* previous)
    {
        if (!active)
        {
            return ::CryptCATAdminEnumCatalogFromHash(admin, hash, size, flags, previous);
        }
        Assert::IsTrue(admin == active);
        Assert::AreEqual(static_cast<DWORD>(4), size);
        if (previous)
        {
            Assert::IsTrue(*previous == &active->catalogIndex);
            ++active->catalogsClosed;
            ++active->catalogIndex;
        }
        else
        {
            active->catalogIndex = 0;
        }
        if (active->algorithms.back() != active->catalogAlgorithm || active->catalogIndex >= active->catalogs.size())
        {
            SetLastError(ERROR_NOT_FOUND);
            return nullptr;
        }
        ++active->catalogsOpened;
        return &active->catalogIndex;
    }

    BOOL WINAPI CatalogInformation(HCATINFO catalog, CATALOG_INFO* information, DWORD flags)
    {
        if (!active)
        {
            return ::CryptCATCatalogInfoFromContext(catalog, information, flags);
        }
        Assert::IsTrue(catalog == &active->catalogIndex);
        wcscpy_s(information->wszCatalogFile, L"C:\\WorkspacesTest\\signature.cat");
        return TRUE;
    }

    BOOL WINAPI ReleaseCatalog(HCATADMIN admin, HCATINFO catalog, DWORD flags)
    {
        if (!active)
        {
            return ::CryptCATAdminReleaseCatalogContext(admin, catalog, flags);
        }
        Assert::IsTrue(admin == active);
        Assert::IsTrue(catalog == &active->catalogIndex);
        ++active->catalogsClosed;
        return TRUE;
    }
}

// Link the production verifier from this test translation unit; inactive seams use real Windows APIs.
#define GetFinalPathNameByHandleW ::WorkspacesLibUnitTests::TrustApi::FinalPath
#define WinVerifyTrust ::WorkspacesLibUnitTests::TrustApi::VerifyTrust
#define WTHelperProvDataFromStateData ::WorkspacesLibUnitTests::TrustApi::Provider
#define WTHelperGetProvSignerFromChain ::WorkspacesLibUnitTests::TrustApi::Signer
#define CertGetCertificateChain ::WorkspacesLibUnitTests::TrustApi::CertificateChain
#define CertFreeCertificateChain ::WorkspacesLibUnitTests::TrustApi::FreeChain
#define CertVerifyCertificateChainPolicy ::WorkspacesLibUnitTests::TrustApi::ChainPolicy
#define CryptCATAdminAcquireContext2 ::WorkspacesLibUnitTests::TrustApi::AcquireCatalog
#define CryptCATAdminReleaseContext ::WorkspacesLibUnitTests::TrustApi::ReleaseCatalogAdmin
#define CryptCATAdminCalcHashFromFileHandle2 ::WorkspacesLibUnitTests::TrustApi::CatalogHash
#define CryptCATAdminEnumCatalogFromHash ::WorkspacesLibUnitTests::TrustApi::EnumerateCatalog
#define CryptCATCatalogInfoFromContext ::WorkspacesLibUnitTests::TrustApi::CatalogInformation
#define CryptCATAdminReleaseCatalogContext ::WorkspacesLibUnitTests::TrustApi::ReleaseCatalog
#include <WorkspacesLib/SignatureVerification.cpp>
#undef GetFinalPathNameByHandleW
#undef WinVerifyTrust
#undef WTHelperProvDataFromStateData
#undef WTHelperGetProvSignerFromChain
#undef CertGetCertificateChain
#undef CertFreeCertificateChain
#undef CertVerifyCertificateChainPolicy
#undef CryptCATAdminAcquireContext2
#undef CryptCATAdminReleaseContext
#undef CryptCATAdminCalcHashFromFileHandle2
#undef CryptCATAdminEnumCatalogFromHash
#undef CryptCATCatalogInfoFromContext
#undef CryptCATAdminReleaseCatalogContext

namespace WorkspacesLibUnitTests
{
    namespace
    {
        thread_local std::function<BOOL(SHELLEXECUTEINFO*)> shellExecute;
    }

    BOOL WINAPI FakeShellExecute(SHELLEXECUTEINFO* information)
    {
        if (!shellExecute)
        {
            SetLastError(ERROR_ACCESS_DENIED);
            Microsoft::VisualStudio::CppUnitTestFramework::Assert::Fail(L"Unexpected ShellExecuteEx call; tests must not launch applications.");
        }
        return shellExecute(information);
    }
}

// Test-only interception: compile the real launcher once, without calling the Windows shell.
#pragma push_macro("ShellExecuteEx")
#undef ShellExecuteEx
#define ShellExecuteEx ::WorkspacesLibUnitTests::FakeShellExecute
#include <WorkspacesLauncher/AppLauncher.cpp>
#pragma pop_macro("ShellExecuteEx")

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using SignatureVerification::Status;

namespace WorkspacesLibUnitTests
{
    namespace
    {
        struct TemporaryExecutable
        {
            std::wstring path;
            std::wstring directory;

            explicit TemporaryExecutable(const std::wstring& fileName = {})
            {
                wchar_t temporary[MAX_PATH]{};
                Assert::IsTrue(GetTempFileNameW(std::filesystem::temp_directory_path().c_str(), L"wsg", 0, temporary) != 0);
                path = std::wstring(temporary) + L".exe";
                if (!fileName.empty())
                {
                    directory = std::wstring(temporary) + L".dir";
                    Assert::IsTrue(CreateDirectoryW(directory.c_str(), nullptr) != FALSE);
                    path = (std::filesystem::path(directory) / fileName).wstring();
                }
                Assert::IsTrue(MoveFileW(temporary, path.c_str()) != FALSE);

                std::array<char, 1024> image{};
                IMAGE_DOS_HEADER dos{};
                dos.e_magic = IMAGE_DOS_SIGNATURE;
                dos.e_lfanew = sizeof(dos);
                IMAGE_NT_HEADERS64 nt{};
                nt.Signature = IMAGE_NT_SIGNATURE;
                nt.FileHeader.Machine = IMAGE_FILE_MACHINE_AMD64;
                nt.FileHeader.NumberOfSections = 1;
                nt.FileHeader.SizeOfOptionalHeader = sizeof(nt.OptionalHeader);
                nt.FileHeader.Characteristics = IMAGE_FILE_EXECUTABLE_IMAGE | IMAGE_FILE_LARGE_ADDRESS_AWARE;
                nt.OptionalHeader.Magic = IMAGE_NT_OPTIONAL_HDR64_MAGIC;
                nt.OptionalHeader.AddressOfEntryPoint = 0x1000;
                nt.OptionalHeader.BaseOfCode = 0x1000;
                nt.OptionalHeader.ImageBase = 0x140000000;
                nt.OptionalHeader.SectionAlignment = 0x1000;
                nt.OptionalHeader.FileAlignment = 0x200;
                nt.OptionalHeader.MajorOperatingSystemVersion = 6;
                nt.OptionalHeader.MajorSubsystemVersion = 6;
                nt.OptionalHeader.SizeOfImage = 0x2000;
                nt.OptionalHeader.SizeOfHeaders = 0x200;
                nt.OptionalHeader.Subsystem = IMAGE_SUBSYSTEM_WINDOWS_CUI;
                nt.OptionalHeader.NumberOfRvaAndSizes = IMAGE_NUMBEROF_DIRECTORY_ENTRIES;
                IMAGE_SECTION_HEADER section{};
                memcpy(section.Name, ".text", 5);
                section.Misc.VirtualSize = 3;
                section.VirtualAddress = 0x1000;
                section.SizeOfRawData = 0x200;
                section.PointerToRawData = 0x200;
                section.Characteristics = IMAGE_SCN_CNT_CODE | IMAGE_SCN_MEM_EXECUTE | IMAGE_SCN_MEM_READ;
                memcpy(image.data(), &dos, sizeof(dos));
                memcpy(image.data() + sizeof(dos), &nt, sizeof(nt));
                memcpy(image.data() + sizeof(dos) + sizeof(nt), &section, sizeof(section));
                image[0x200] = 0x31;
                image[0x201] = static_cast<char>(0xC0);
                image[0x202] = static_cast<char>(0xC3);
                std::ofstream output(std::filesystem::path(path), std::ios::binary | std::ios::trunc);
                output.write(image.data(), image.size());
                Assert::IsTrue(output.good());
            }

            ~TemporaryExecutable()
            {
                if (!DeleteFileW(path.c_str()) && GetLastError() != ERROR_FILE_NOT_FOUND)
                {
                    Microsoft::VisualStudio::CppUnitTestFramework::Logger::WriteMessage(L"Could not remove signature test fixture.");
                }
                if (!directory.empty() && !RemoveDirectoryW(directory.c_str()))
                {
                    Microsoft::VisualStudio::CppUnitTestFramework::Logger::WriteMessage(L"Could not remove signature test fixture directory.");
                }
            }
        };
    }

    TEST_CLASS (SignatureVerificationTests)
    {
    public:
        TEST_METHOD (OnlyZeroIsSuccessful)
        {
            Assert::IsTrue(SignatureVerification::Classify(ERROR_SUCCESS) == Status::Verified);
            Assert::IsTrue(SignatureVerification::Classify(S_FALSE) == Status::UnableToVerify);
        }

        TEST_METHOD (NoSignatureRequiresCompletedSearch)
        {
            Assert::IsTrue(SignatureVerification::Classify(TRUST_E_NOSIGNATURE) == Status::UnableToVerify);
            Assert::IsTrue(SignatureVerification::Classify(TRUST_E_NOSIGNATURE, true) == Status::Unsigned);
        }

        TEST_METHOD (ContentMismatchIsNotACertificateError)
        {
            Assert::IsTrue(SignatureVerification::Classify(TRUST_E_BAD_DIGEST) == Status::InvalidSignature);
            Assert::IsTrue(SignatureVerification::Classify(NTE_BAD_SIGNATURE) == Status::InvalidSignature);
        }

        TEST_METHOD (CertificateFailuresAreGrouped)
        {
            for (const LONG error : { CERT_E_UNTRUSTEDROOT, CERT_E_CHAINING, CERT_E_EXPIRED, CERT_E_WRONG_USAGE, CERT_E_REVOKED, CRYPT_E_REVOKED, TRUST_E_EXPLICIT_DISTRUST })
            {
                Assert::IsTrue(SignatureVerification::Classify(error) == Status::CertificateUntrusted);
            }
        }

        TEST_METHOD (OfflineAndGenericErrorsAreInconclusive)
        {
            for (const LONG error : { CRYPT_E_REVOCATION_OFFLINE, CERT_E_REVOCATION_FAILURE, CRYPT_E_NO_REVOCATION_CHECK, E_FAIL, TRUST_E_SUBJECT_FORM_UNKNOWN, HRESULT_FROM_WIN32(ERROR_ACCESS_DENIED) })
            {
                Assert::IsTrue(SignatureVerification::Classify(error) == Status::UnableToVerify);
            }
        }

        TEST_METHOD (RevocationAndExpiryKeepSpecificReasons)
        {
            const SignatureVerification::Result revoked{ Status::CertificateUntrusted, CERT_E_REVOKED };
            const SignatureVerification::Result expired{ Status::CertificateUntrusted, CERT_E_EXPIRED };
            const SignatureVerification::Result offline{ Status::UnableToVerify, CRYPT_E_REVOCATION_OFFLINE };
            Assert::AreEqual(std::wstring(L"revoked"), revoked.Reason());
            Assert::AreEqual(std::wstring(L"expired"), expired.Reason());
            Assert::AreEqual(std::wstring(L"revocation-unavailable"), offline.Reason());
        }

        TEST_METHOD (IndirectAndRelativeTargetsAreNotVerified)
        {
            for (const auto path : { L"shell:AppsFolder\\Example.App", L"https://example.invalid", L"application.exe", L"C:\\test.lnk" })
            {
                const auto result = SignatureVerification::Verify(path);
                Assert::IsFalse(result.result.IsVerified());
                Assert::AreEqual(std::wstring(L"unresolved-target"), result.result.Reason());
            }
        }

        TEST_METHOD (UnsignedPeIsConfirmedAfterCatalogLookup)
        {
            TemporaryExecutable fixture;
            const auto executable = SignatureVerification::Verify(fixture.path);
            Assert::IsTrue(executable.result.status == Status::Unsigned);
            Assert::IsTrue(executable.result.publisher.empty());
        }

        TEST_METHOD (CheckedFileCannotBeChangedWhileHeld)
        {
            TemporaryExecutable fixture;
            const auto executable = SignatureVerification::Verify(fixture.path);
            Assert::IsTrue(static_cast<bool>(executable.file));
            wil::unique_hfile writer(CreateFileW(fixture.path.c_str(), GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr));
            Assert::IsFalse(static_cast<bool>(writer));
            Assert::AreEqual(static_cast<DWORD>(ERROR_SHARING_VIOLATION), GetLastError());
            Assert::IsFalse(DeleteFileW(fixture.path.c_str()) != FALSE);
            Assert::AreEqual(static_cast<DWORD>(ERROR_SHARING_VIOLATION), GetLastError());
        }

        TEST_METHOD (CanceledFileVerificationDoesNotOpenTheTarget)
        {
            TemporaryExecutable fixture;
            const auto target = SignatureVerification::Verify(fixture.path, [] { return true; });
            Assert::IsFalse(static_cast<bool>(target.file));
            Assert::IsFalse(target.result.IsVerified());
            Assert::AreEqual(static_cast<LONG>(HRESULT_FROM_WIN32(ERROR_CANCELLED)), target.result.error);
        }

        TEST_METHOD (MalformedImageIsNotReportedAsUnsigned)
        {
            TemporaryExecutable fixture;
            {
                std::ofstream output(std::filesystem::path(fixture.path), std::ios::binary | std::ios::trunc);
                output << "not an executable";
            }
            const auto executable = SignatureVerification::Verify(fixture.path);
            Assert::IsTrue(executable.result.status == Status::UnableToVerify);
        }

        TEST_METHOD (MissingFileIsNotReportedAsUnsigned)
        {
            TemporaryExecutable fixture;
            Assert::IsTrue(DeleteFileW(fixture.path.c_str()) != FALSE);
            const auto executable = SignatureVerification::Verify(fixture.path);
            Assert::IsTrue(executable.result.status == Status::UnableToVerify);
            Assert::AreEqual(static_cast<LONG>(HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND)), executable.result.error);
        }
    };

    TEST_CLASS (SignatureTrustTests)
    {
    public:
        TEST_METHOD (TrustedEmbeddedSignatureChecksChainAndPublisher)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust;
            const auto target = trust.Verify(file.path);
            Assert::IsTrue(target.result.IsVerified());
            Assert::AreEqual(std::wstring(L"embedded"), target.result.source);
            Assert::AreEqual(std::wstring(L"Workspaces signature unit test"), target.result.publisher);
            Assert::AreEqual(1u, trust.chainCalls);
            Assert::AreEqual(1u, trust.policyCalls);
            Assert::IsTrue(trust.algorithms.empty());
        }

        TEST_METHOD (MissingProviderOrSignerCannotTurnTrustSuccessIntoApproval)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust;
            trust.missingProvider = true;
            Assert::IsFalse(trust.Verify(file.path).result.IsVerified());
            trust.missingProvider = false;
            trust.missingSigner = true;
            Assert::IsFalse(trust.Verify(file.path).result.IsVerified());
            trust.missingSigner = false;
            trust.signer.csCertChain = 0;
            Assert::IsFalse(trust.Verify(file.path).result.IsVerified());
        }

        TEST_METHOD (MachineChainRejectsRootOutsideItsTrustStore)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust(false);
            const auto target = trust.Verify(file.path);
            Assert::IsFalse(target.result.IsVerified());
            Assert::AreEqual(static_cast<LONG>(CERT_E_UNTRUSTEDROOT), target.result.error);
            Assert::IsTrue(target.result.publisher.empty());
        }

        TEST_METHOD (MachineChainApiFailureIsInconclusive)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust;
            trust.chainFailure = true;
            const auto target = trust.Verify(file.path);
            Assert::IsTrue(target.result.status == Status::UnableToVerify);
            Assert::AreEqual(static_cast<LONG>(HRESULT_FROM_WIN32(ERROR_INVALID_DATA)), target.result.error);
        }

        TEST_METHOD (MachineChainRejectsHardTrustFailures)
        {
            TemporaryExecutable file;
            for (const auto& [flags, error] : {
                     std::pair<DWORD, LONG>{ CERT_TRUST_IS_REVOKED, CERT_E_REVOKED },
                     { CERT_TRUST_IS_EXPLICIT_DISTRUST, TRUST_E_EXPLICIT_DISTRUST },
                     { CERT_TRUST_IS_UNTRUSTED_ROOT, CERT_E_UNTRUSTEDROOT },
                     { CERT_TRUST_IS_PARTIAL_CHAIN, CERT_E_CHAINING },
                 })
            {
                TrustApi::Fixture trust;
                trust.signerErrors = flags;
                const auto target = trust.Verify(file.path);
                Assert::IsFalse(target.result.IsVerified());
                Assert::AreEqual(error, target.result.error);
                Assert::AreEqual(0u, trust.policyCalls);
            }
        }

        TEST_METHOD (UnavailableCachedRevocationStillRequiresWarning)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust;
            trust.signerErrors = CERT_TRUST_REVOCATION_STATUS_UNKNOWN | CERT_TRUST_IS_OFFLINE_REVOCATION;
            const auto target = trust.Verify(file.path);
            Assert::IsTrue(target.result.status == Status::UnableToVerify);
            Assert::AreEqual(std::wstring(L"revocation-unavailable"), target.result.Reason());
        }

        TEST_METHOD (ExpiredCertificateIsRejectedByAuthenticodeChainPolicy)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust(true, -2);
            const auto target = trust.Verify(file.path);
            Assert::IsFalse(target.result.IsVerified());
            Assert::AreEqual(static_cast<LONG>(CERT_E_EXPIRED), target.result.error);
            Assert::AreEqual(1u, trust.policyCalls);
        }

        TEST_METHOD (AuthenticatedTimestampUsesHistoricalChainTime)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust(true, -2);
            trust.signer.sftVerifyAsOf = TrustApi::Fixture::ShiftDays(trust.signer.sftVerifyAsOf, -2);
            trust.timestamp.sftVerifyAsOf = trust.signer.sftVerifyAsOf;
            trust.signer.csCounterSigners = 1;
            trust.signer.pasCounterSigners = &trust.timestamp;
            Assert::IsTrue(trust.Verify(file.path).result.IsVerified());
            Assert::AreEqual(2u, trust.chainCalls);
            Assert::AreEqual(2u, trust.policyCalls);
        }

        TEST_METHOD (RevokedTimestampRejectsAnOtherwiseTrustedSigner)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust;
            trust.signer.csCounterSigners = 1;
            trust.signer.pasCounterSigners = &trust.timestamp;
            trust.timestampErrors = CERT_TRUST_IS_REVOKED;
            const auto target = trust.Verify(file.path);
            Assert::IsFalse(target.result.IsVerified());
            Assert::AreEqual(static_cast<LONG>(CERT_E_REVOKED), target.result.error);
            Assert::AreEqual(2u, trust.chainCalls);
        }

        TEST_METHOD (BadDigestDoesNotReachSignerChainValidation)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust;
            trust.embedded = { TRUST_E_BAD_DIGEST };
            const auto target = trust.Verify(file.path);
            Assert::IsTrue(target.result.status == Status::InvalidSignature);
            Assert::AreEqual(static_cast<LONG>(TRUST_E_BAD_DIGEST), target.result.error);
            Assert::AreEqual(0u, trust.chainCalls);
        }

        TEST_METHOD (NonzeroSuccessCodeDoesNotReachSignerChainValidation)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust;
            trust.embedded = { S_FALSE };
            Assert::IsFalse(trust.Verify(file.path).result.IsVerified());
            Assert::AreEqual(0u, trust.chainCalls);
        }

        TEST_METHOD (TrustedSecondarySignatureCanVerifyTheExecutable)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust;
            trust.embedded = { TRUST_E_BAD_DIGEST, ERROR_SUCCESS };
            Assert::IsTrue(trust.Verify(file.path).result.IsVerified());
            Assert::AreEqual(2u, trust.verified);
            Assert::AreEqual(1u, trust.chainCalls);
            Assert::IsTrue(trust.algorithms.empty());
        }

        TEST_METHOD (InstalledSha256AndSha1CatalogMatchesAreVerified)
        {
            TemporaryExecutable file;
            for (const auto algorithm : { L"SHA256", L"SHA1" })
            {
                TrustApi::Fixture trust;
                trust.embedded = { TRUST_E_NOSIGNATURE };
                trust.catalogs = { ERROR_SUCCESS };
                trust.catalogAlgorithm = algorithm;
                const auto target = trust.Verify(file.path);
                Assert::IsTrue(target.result.IsVerified());
                Assert::AreEqual(std::wstring(L"catalog"), target.result.source);
                Assert::AreEqual(std::wstring(algorithm), trust.algorithms.back());
                Assert::AreEqual(2u, trust.verified);
                Assert::AreEqual(1u, trust.chainCalls);
            }
        }

        TEST_METHOD (CatalogMemberDigestMismatchIsNotReportedAsUnsigned)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust;
            trust.embedded = { TRUST_E_NOSIGNATURE };
            trust.catalogs = { TRUST_E_BAD_DIGEST };
            const auto target = trust.Verify(file.path);
            Assert::IsTrue(target.result.status == Status::InvalidSignature);
            Assert::AreEqual(static_cast<LONG>(TRUST_E_BAD_DIGEST), target.result.error);
            Assert::AreEqual(0u, trust.chainCalls);
        }

        TEST_METHOD (CatalogSearchContinuesAfterAnInvalidMatch)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust;
            trust.embedded = { TRUST_E_NOSIGNATURE };
            trust.catalogs = { TRUST_E_BAD_DIGEST, ERROR_SUCCESS };
            Assert::IsTrue(trust.Verify(file.path).result.IsVerified());
            Assert::AreEqual(3u, trust.verified);
            Assert::AreEqual(2u, trust.catalogsOpened);
        }

        TEST_METHOD (CatalogSearchFailureCannotConfirmUnsignedStatus)
        {
            TemporaryExecutable file;
            TrustApi::Fixture trust;
            trust.embedded = { TRUST_E_NOSIGNATURE };
            trust.catalogFailure = true;
            const auto target = trust.Verify(file.path);
            Assert::IsTrue(target.result.status == Status::UnableToVerify);
            Assert::AreEqual(static_cast<LONG>(HRESULT_FROM_WIN32(ERROR_ACCESS_DENIED)), target.result.error);
        }

        TEST_METHOD (FinalPathNormalizationPreservesNonDosExtendedPaths)
        {
            TemporaryExecutable file;
            const std::pair<const wchar_t*, const wchar_t*> paths[] = {
                { L"\\\\?\\C:\\Apps\\test.exe", L"C:\\Apps\\test.exe" },
                { L"\\\\?\\z:\\Apps\\test.exe", L"z:\\Apps\\test.exe" },
                { L"\\\\?\\UNC\\server\\share\\test.exe", L"\\\\server\\share\\test.exe" },
                { L"\\\\?\\Volume{12345678-1234-1234-1234-123456789ABC}\\test.exe", L"\\\\?\\Volume{12345678-1234-1234-1234-123456789ABC}\\test.exe" },
                { L"\\\\?\\GLOBALROOT\\Device\\HarddiskVolume1\\test.exe", L"\\\\?\\GLOBALROOT\\Device\\HarddiskVolume1\\test.exe" },
                { L"\\\\?\\1:\\test.exe", L"\\\\?\\1:\\test.exe" },
            };
            TrustApi::Fixture trust;
            for (const auto& [reported, expected] : paths)
            {
                trust.finalPath = reported;
                const auto target = trust.Verify(file.path);
                Assert::IsTrue(target.result.IsVerified());
                Assert::AreEqual(std::wstring(expected), target.path);
            }
        }
    };

    TEST_CLASS (ExecutableTargetTests)
    {
    public:
        TEST_METHOD (FileExecutablePathsRequireVerification)
        {
            for (const auto path : {
                     L"C:\\Example\\app.exe",
                     L"C:\\Example\\APP.ExE",
                     L"\\\\server\\share\\app.exe",
                     L"\\\\?\\C:\\Example\\app.exe",
                     L"\\\\?\\UNC\\server\\share\\app.exe",
                     L"app.exe",
                     L".\\app.exe",
                     L"C:app.exe",
                     L"\\app.exe",
                 })
            {
                Assert::IsTrue(SignatureVerification::IsExecutableTarget(path), path);
            }
        }

        TEST_METHOD (Win32NormalizedFileExtensionsRequireVerification)
        {
            for (const auto path : { L"C:\\app.exe ", L"C:\\app.exe.", L"C:\\app.ExE. . ", L"app.exe. " })
            {
                Assert::IsTrue(SignatureVerification::IsExecutableTarget(path), path);
            }
            TemporaryExecutable fixture;
            const auto executable = SignatureVerification::Verify(fixture.path + L". ");
            Assert::IsTrue(executable.result.status == Status::Unsigned);
            Assert::IsTrue(std::filesystem::equivalent(fixture.path, executable.path));
        }

        TEST_METHOD (RelativeExecutablePathsWarnRatherThanBypass)
        {
            for (const auto path : { L"app.exe", L".\\app.exe", L"C:app.exe", L"\\app.exe" })
            {
                Assert::IsTrue(SignatureVerification::IsExecutableTarget(path), path);
                const auto target = SignatureVerification::Verify(path);
                Assert::IsFalse(target.result.IsVerified());
                Assert::IsFalse(static_cast<bool>(target.file));
                Assert::AreEqual(std::wstring(L"unresolved-target"), target.result.Reason());
            }
        }

        TEST_METHOD (ProtocolsAndShellTargetsBypassEvenWithExecutableSuffix)
        {
            for (const auto path : {
                     L"shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App",
                     L"SHELL:AppsFolder\\Example.exe",
                     L"ms-settings:display",
                     L"steam://rungameid/123",
                     L"https://example.invalid/app.exe",
                     L"custom:app.exe",
                     L"file:///C:/app.exe",
                     L"x://example.invalid/app.exe",
                 })
            {
                Assert::IsFalse(SignatureVerification::IsExecutableTarget(path), path);
                const auto target = SignatureVerification::Verify(path);
                Assert::IsFalse(target.result.IsVerified());
                Assert::IsFalse(static_cast<bool>(target.file));
                Assert::AreEqual(std::wstring(L"unresolved-target"), target.result.Reason());
            }
        }

        TEST_METHOD (OtherFileTypesAreNotExecutableTargets)
        {
            for (const auto path : { L"", L" . ", L"C:\\app.lnk", L"C:\\app.msix", L"C:\\app.exe.txt", L"C:\\app.cmd", L"C:\\app.exe\\", L"C:\\app.exe\\data" })
            {
                Assert::IsFalse(SignatureVerification::IsExecutableTarget(path), path);
            }
        }

        TEST_METHOD (EmbeddedNullCannotHideAnExecutableFromVerification)
        {
            TemporaryExecutable fixture;
            const auto path = fixture.path + std::wstring(1, L'\0') + L".txt";
            Assert::IsTrue(SignatureVerification::IsExecutableTarget(path));
            const auto target = SignatureVerification::Verify(path);
            Assert::IsFalse(target.result.IsVerified());
            Assert::IsFalse(static_cast<bool>(target.file));
            Assert::AreEqual(std::wstring(L"unresolved-target"), target.result.Reason());
        }
    };

    TEST_CLASS (ElevatedLaunchGateTests)
    {
        static void AssertElevatedTargetBypassesVerification(const WorkspacesData::WorkspacesProject::Application& app, const std::wstring& expectedTarget)
        {
            Assert::IsTrue(app.isElevated);
            Assert::IsFalse(static_cast<bool>(shellExecute));
            int launches = 0;
            shellExecute = [&](SHELLEXECUTEINFO* information) {
                ++launches;
                Assert::IsNotNull(information);
                Assert::AreEqual(L"runas", information->lpVerb);
                Assert::AreEqual(expectedTarget.c_str(), information->lpFile);
                Assert::AreEqual(app.commandLineArgs.c_str(), information->lpParameters);
                information->hProcess = nullptr;
                return TRUE;
            };
            auto resetCapture = wil::scope_exit([] { shellExecute = {}; });

            AppLauncher::ErrorList errors;
            int approvals = 0;
            const auto result = AppLauncher::Launch(app, errors, [&](const auto&, const auto&, const auto&) {
                ++approvals;
                return LaunchDecision::Skipped; }, [] { return false; });
            Assert::AreEqual(0, approvals, L"Elevated non-EXE targets must bypass signature approval.");
            Assert::AreEqual(1, launches);
            Assert::IsTrue(result == AppLauncher::LaunchResult::Launched);
            Assert::IsTrue(errors.empty());
        }

        static void AssertDecliningUnsignedExecutableDoesNotLaunch(const std::wstring& requestedPath)
        {
            WorkspacesData::WorkspacesProject::Application app{};
            app.name = L"Signature test";
            app.path = requestedPath;
            app.isElevated = true;
            AppLauncher::ErrorList errors;
            int requests = 0;
            const auto result = AppLauncher::Launch(app, errors, [&](const auto& path, const auto&, const auto& verification) {
                ++requests;
                Assert::IsTrue(std::filesystem::equivalent(requestedPath, path), L"Approval must refer to the requested executable.");
                Assert::IsTrue(verification.status == Status::Unsigned);
                return LaunchDecision::Skipped; }, [] { return false; });
            Assert::IsTrue(result == AppLauncher::LaunchResult::Skipped);
            Assert::AreEqual(1, requests);
            Assert::IsTrue(errors.empty());
        }

    public:
        TEST_METHOD (ElevatedNonExecutableTargetBypassesSignatureVerification)
        {
            TemporaryExecutable fixture(L"application.com");
            WorkspacesData::WorkspacesProject::Application app{};
            app.path = fixture.path;
            app.commandLineArgs = L"--bypass-test \"unchanged arguments\"";
            app.isElevated = true;
            AssertElevatedTargetBypassesVerification(app, fixture.path);
        }

        TEST_METHOD (ElevatedPackagedTargetBypassesSignatureVerification)
        {
            TemporaryExecutable fixture;
            WorkspacesData::WorkspacesProject::Application app{};
            app.path = fixture.path;
            app.packageFullName = L"PowerToys.NonexistentSignatureFixture_0.0.0.0_neutral__0000000000000";
            app.appUserModelId = L"PowerToys.NonexistentSignatureFixture_0000000000000!App";
            app.commandLineArgs = L"--bypass-test \"unchanged arguments\"";
            app.isElevated = true;
            Assert::IsTrue(RegistryUtils::GetUriProtocolNames(app.packageFullName).empty());
            AssertElevatedTargetBypassesVerification(app, L"shell:AppsFolder\\" + app.appUserModelId);
        }

        TEST_METHOD (ElevatedProtocolTargetsBypassSignatureVerification)
        {
            TemporaryExecutable fixture;
            for (const auto target : { L"steam://rungameid/0", L"steam://rungameid/0.exe" })
            {
                WorkspacesData::WorkspacesProject::Application app{};
                app.path = fixture.path;
                app.appUserModelId = target;
                app.commandLineArgs = L"--bypass-test \"unchanged arguments\"";
                app.isElevated = true;
                AssertElevatedTargetBypassesVerification(app, target);
            }
        }

        TEST_METHOD (ApprovingUnsignedExecutableDispatchesCheckedTarget)
        {
            TemporaryExecutable fixture;
            const std::filesystem::path path(fixture.path);
            WorkspacesData::WorkspacesProject::Application app{};
            app.path = (path.parent_path() / L"." / path.filename()).wstring();
            app.commandLineArgs = L"--run-anyway \"unchanged arguments\"";
            app.isElevated = true;

            Assert::IsFalse(static_cast<bool>(shellExecute));
            int launches = 0;
            std::wstring checkedPath;
            shellExecute = [&](SHELLEXECUTEINFO* information) {
                ++launches;
                Assert::IsNotNull(information);
                Assert::IsFalse(checkedPath.empty());
                Assert::AreEqual(L"runas", information->lpVerb);
                Assert::AreEqual(checkedPath.c_str(), information->lpFile);
                Assert::AreEqual(app.commandLineArgs.c_str(), information->lpParameters);
                wil::unique_hfile writer(CreateFileW(information->lpFile, GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr));
                const auto error = GetLastError();
                Assert::IsFalse(static_cast<bool>(writer), L"The checked executable must stay locked through dispatch.");
                Assert::AreEqual(static_cast<DWORD>(ERROR_SHARING_VIOLATION), error);
                information->hProcess = nullptr;
                return TRUE;
            };
            auto resetCapture = wil::scope_exit([] { shellExecute = {}; });

            AppLauncher::ErrorList errors;
            int approvals = 0;
            const auto result = AppLauncher::Launch(app, errors, [&](const auto& target, const auto& arguments, const auto& verification) {
                ++approvals;
                Assert::IsTrue(verification.status == Status::Unsigned);
                Assert::IsTrue(std::filesystem::equivalent(fixture.path, target));
                Assert::AreNotEqual(app.path, target, L"Verification must resolve the dot component before dispatch.");
                Assert::AreEqual(app.commandLineArgs, arguments);
                checkedPath = target;
                return LaunchDecision::Approved; }, [] { return false; });
            Assert::AreEqual(1, approvals);
            Assert::AreEqual(1, launches);
            Assert::IsTrue(result == AppLauncher::LaunchResult::Launched);
            Assert::IsTrue(errors.empty());
            Assert::IsTrue(DeleteFileW(fixture.path.c_str()) != FALSE, L"Dispatch completion must release the checked file handle.");
        }

        TEST_METHOD (DecliningUnsignedExecutableDoesNotLaunch)
        {
            TemporaryExecutable fixture;
            AssertDecliningUnsignedExecutableDoesNotLaunch(fixture.path);
            Assert::IsTrue(DeleteFileW(fixture.path.c_str()) != FALSE, L"Skipping must release the checked file handle.");
        }

        TEST_METHOD (DecliningUnsignedExecutableWithDotComponentDoesNotLaunch)
        {
            TemporaryExecutable fixture;
            const std::filesystem::path path(fixture.path);
            const auto alternatePath = (path.parent_path() / L"." / path.filename()).wstring();
            Assert::AreNotEqual(fixture.path, alternatePath);
            AssertDecliningUnsignedExecutableDoesNotLaunch(alternatePath);
        }

        TEST_METHOD (DecliningUnsignedExecutableWithTrailingDotsAndSpacesDoesNotLaunch)
        {
            TemporaryExecutable fixture;
            AssertDecliningUnsignedExecutableDoesNotLaunch(fixture.path + L". ");
        }

        TEST_METHOD (DecliningUnsignedExecutableWithShortPathDoesNotLaunch)
        {
            TemporaryExecutable fixture;
            const DWORD length = GetShortPathNameW(fixture.path.c_str(), nullptr, 0);
            Assert::IsTrue(length > 0);
            std::wstring shortPath(length, L'\0');
            const DWORD written = GetShortPathNameW(fixture.path.c_str(), shortPath.data(), length);
            Assert::IsTrue(written > 0 && written < length);
            shortPath.resize(written);
            if (shortPath == fixture.path)
            {
                Microsoft::VisualStudio::CppUnitTestFramework::Logger::WriteMessage(L"No alternate 8.3 path is available for the signature test fixture.");
                return;
            }

            AssertDecliningUnsignedExecutableDoesNotLaunch(shortPath);
        }

        TEST_METHOD (DecliningPwaExecutableFallbackDoesNotLaunch)
        {
            for (const auto& browser : { std::wstring(L"msedge"), std::wstring(L"chrome") })
            {
                TemporaryExecutable fixture(browser + L"_proxy.exe");
                WorkspacesData::WorkspacesProject::Application app{};
                app.name = L"Signature test";
                app.path = (std::filesystem::path(fixture.directory) / (browser + L".exe")).wstring();
                app.commandLineArgs = L"--test-argument";
                app.isElevated = true;
                app.pwaAppId = L"prototype-test";
                app.version = L"0";
                AppLauncher::ErrorList errors;
                int requests = 0;
                const auto result = AppLauncher::Launch(app, errors, [&](const auto& path, const auto& arguments, const auto& verification) {
                    ++requests;
                    Assert::IsTrue(std::filesystem::equivalent(fixture.path, path));
                    Assert::AreEqual(std::wstring(L"--profile-directory=Default --app-id=prototype-test --test-argument"), arguments);
                    Assert::IsTrue(verification.status == Status::Unsigned);
                    return LaunchDecision::Skipped; }, [] { return false; });
                Assert::IsTrue(result == AppLauncher::LaunchResult::Skipped);
                Assert::AreEqual(1, requests);
                Assert::IsTrue(errors.empty());
            }
        }

        TEST_METHOD (CanceledWorkspaceDoesNotRequestApproval)
        {
            TemporaryExecutable fixture;
            WorkspacesData::WorkspacesProject::Application app{};
            app.path = fixture.path;
            app.isElevated = true;
            AppLauncher::ErrorList errors;
            int requests = 0;
            const auto result = AppLauncher::Launch(app, errors, [&](const auto&, const auto&, const auto&) {
                ++requests;
                return LaunchDecision::Skipped; }, [] { return true; });
            Assert::IsTrue(result == AppLauncher::LaunchResult::Canceled);
            Assert::AreEqual(0, requests);
        }

        TEST_METHOD (MissingExecutableFailsWithoutRequestingApproval)
        {
            TemporaryExecutable fixture;
            Assert::IsTrue(DeleteFileW(fixture.path.c_str()) != FALSE);
            WorkspacesData::WorkspacesProject::Application app{};
            app.path = fixture.path;
            app.isElevated = true;
            AppLauncher::ErrorList errors;
            int requests = 0;
            const auto result = AppLauncher::Launch(app, errors, [&](const auto&, const auto&, const auto&) {
                ++requests;
                return LaunchDecision::Skipped; }, [] { return false; });
            Assert::IsTrue(result == AppLauncher::LaunchResult::Failed);
            Assert::AreEqual(0, requests);
            Assert::AreEqual(size_t{ 1 }, errors.size());
        }

        TEST_METHOD (DecliningUnreadableExecutableDoesNotLaunch)
        {
            TemporaryExecutable fixture;
            wil::unique_hfile writer(CreateFileW(fixture.path.c_str(), GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr));
            Assert::IsTrue(static_cast<bool>(writer));
            WorkspacesData::WorkspacesProject::Application app{};
            app.path = fixture.path;
            app.isElevated = true;
            AppLauncher::ErrorList errors;
            int requests = 0;
            const auto result = AppLauncher::Launch(app, errors, [&](const auto&, const auto&, const auto& verification) {
                ++requests;
                Assert::IsTrue(verification.status == Status::UnableToVerify);
                Assert::AreEqual(static_cast<LONG>(HRESULT_FROM_WIN32(ERROR_SHARING_VIOLATION)), verification.error);
                return LaunchDecision::Skipped; }, [] { return false; });
            Assert::IsTrue(result == AppLauncher::LaunchResult::Skipped);
            Assert::AreEqual(1, requests);
            Assert::IsTrue(errors.empty());
        }

        TEST_METHOD (ConfirmationFailuresStopAllFallbacksAndAreNotUserCancellation)
        {
            TemporaryExecutable fixture;
            WorkspacesData::WorkspacesProject::Application app{};
            app.path = fixture.path;
            app.isElevated = true;
            app.pwaAppId = L"test";
            app.appUserModelId = L"PowerToys.SignatureTest";
            app.version = L"0";
            for (const auto failure : { LaunchDecision::UiUnavailable, LaunchDecision::TimedOut, LaunchDecision::InvalidResponse })
            {
                AppLauncher::ErrorList errors;
                int requests = 0;
                const auto result = AppLauncher::Launch(app, errors, [&](const auto&, const auto&, const auto&) {
                    ++requests;
                    return failure; }, [] { return false; });
                Assert::IsTrue(result == AppLauncher::LaunchResult::Failed);
                Assert::AreEqual(1, requests);
                Assert::AreEqual(size_t{ 1 }, errors.size());
            }
        }

        TEST_METHOD (WorkspaceCancellationOverridesApprovalBeforeExecution)
        {
            TemporaryExecutable fixture;
            WorkspacesData::WorkspacesProject::Application app{};
            app.path = fixture.path;
            app.isElevated = true;
            AppLauncher::ErrorList errors;
            bool canceled = false;
            const auto result = AppLauncher::Launch(app, errors, [&](const auto&, const auto&, const auto&) {
                canceled = true;
                return LaunchDecision::Approved; }, [&] { return canceled; });
            Assert::IsTrue(result == AppLauncher::LaunchResult::Canceled);
            Assert::IsTrue(errors.empty());
        }

        TEST_METHOD (CancelingConfirmationDoesNotLaunch)
        {
            TemporaryExecutable fixture;
            WorkspacesData::WorkspacesProject::Application app{};
            app.path = fixture.path;
            app.isElevated = true;
            AppLauncher::ErrorList errors;
            int requests = 0;
            const auto result = AppLauncher::Launch(app, errors, [&](const auto&, const auto&, const auto&) {
                ++requests;
                return LaunchDecision::Canceled; }, [] { return false; });
            Assert::IsTrue(result == AppLauncher::LaunchResult::Canceled);
            Assert::AreEqual(1, requests);
            Assert::IsTrue(errors.empty());
        }

        TEST_METHOD (LaunchStatusSnapshotsAndSkipAreTerminal)
        {
            WorkspacesData::WorkspacesProject project{};
            WorkspacesData::WorkspacesProject::Application app{};
            app.id = L"snapshot-test";
            project.apps.push_back(app);
            LaunchingStatus status(project);
            auto snapshot = status.Get();
            status.Update(app, LaunchingState::Skipped);
            Assert::IsTrue(snapshot.at(app).state == LaunchingState::Waiting);
            Assert::IsTrue(status.Get(app)->state == LaunchingState::Skipped);
            Assert::IsTrue(status.AllLaunched());
            Assert::IsTrue(status.AllLaunchedAndMoved());
            status.Update(app, LaunchingState::Launched);
            status.Update(app, nullptr, LaunchingState::LaunchedAndMoved);
            Assert::IsTrue(status.Get(app)->state == LaunchingState::Skipped);
        }
    };

    TEST_CLASS (PendingLaunchApprovalTests)
    {
    public:
        TEST_METHOD (MinimalMessagesRequireTypedChoice)
        {
            const auto approved = LauncherUiMessage::Parse(LR"({"type":"elevation-response","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}","choice":"run"})");
            Assert::IsTrue(approved.has_value());
            Assert::IsTrue(approved->type == LauncherUiMessage::Type::Response);
            Assert::IsTrue(approved->decision == LaunchDecision::Approved);
            const auto skipped = LauncherUiMessage::Parse(LR"({"type":"elevation-response","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}","choice":"skip"})");
            Assert::IsTrue(skipped.has_value());
            Assert::IsTrue(skipped->decision == LaunchDecision::Skipped);
            const auto shown = LauncherUiMessage::Parse(LR"({"type":"warning-shown","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}"})");
            Assert::IsTrue(shown.has_value());
            Assert::IsTrue(shown->type == LauncherUiMessage::Type::WarningShown);
            Assert::AreEqual(approved->requestId, shown->requestId);
            const auto cancel = LauncherUiMessage::Parse(L"cancel");
            Assert::IsTrue(cancel.has_value());
            Assert::IsTrue(cancel->type == LauncherUiMessage::Type::Cancel);
        }

        TEST_METHOD (MalformedAndUnsupportedMessagesAreRejected)
        {
            for (const auto text : {
                     L"ready",
                     L" cancel",
                     L"cancel\n",
                     L"\"cancel\"",
                     L"null",
                     L"[]",
                     LR"({"type":"ready"})",
                     LR"({"type":"cancel"})",
                     LR"({"type":"warning-shown"})",
                     LR"({"type":"warning-shown","requestId":1})",
                     LR"({"type":"warning-shown","requestId":null})",
                     LR"({"type":1,"requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}"})",
                     LR"({"type":"elevation-response","requestId":"request","choice":"run"})",
                     LR"({"type":"elevation-response","requestId":"F4CE1D14-2DAE-47F9-B775-D48D5B494CAB","choice":"run"})",
                     LR"({"type":"elevation-response","requestId":"{G4CE1D14-2DAE-47F9-B775-D48D5B494CAB}","choice":"run"})",
                     LR"({"type":"elevation-response","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}","allow":true})",
                     LR"({"type":"elevation-response","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}","choice":true})",
                     LR"({"type":"elevation-response","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}","choice":"always"})",
                     LR"({"type":"other","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}"})",
                     LR"({"type":"heartbeat","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}"})",
                     LR"({"type":"warning-shown","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB\u0000"})",
                 })
            {
                Assert::IsFalse(LauncherUiMessage::Parse(text).has_value());
            }
        }

        TEST_METHOD (ApprovalMustMatchThePendingRequest)
        {
            PendingLaunchApproval approval;
            Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Approved));
            Assert::IsTrue(approval.Begin(L"request"));
            Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Approved));
            Assert::IsFalse(approval.Acknowledge(L"other-request"));
            Assert::IsTrue(approval.Acknowledge(L"request"));
            Assert::IsFalse(approval.Complete(L"other-request", LaunchDecision::Approved));
            Assert::IsFalse(approval.WaitFor(std::chrono::milliseconds(0)).has_value());
            Assert::IsTrue(approval.Complete(L"request", LaunchDecision::Approved));
            Assert::IsTrue(approval.WaitFor(std::chrono::milliseconds(0)).value() == LaunchDecision::Approved);
        }

        TEST_METHOD (FirstDecisionCannotBeOverwritten)
        {
            for (const auto first : { LaunchDecision::Approved, LaunchDecision::Skipped })
            {
                PendingLaunchApproval approval;
                Assert::IsTrue(approval.Begin(L"request"));
                Assert::IsTrue(approval.Acknowledge(L"request"));
                Assert::IsTrue(approval.Complete(L"request", first));
                Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Approved));
                Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Skipped));
                approval.Fail(LaunchDecision::UiUnavailable);
                Assert::IsTrue(approval.Evaluate().value() == first);
            }
        }

        TEST_METHOD (MatchingSkipCanFinishBeforeWarningIsShown)
        {
            PendingLaunchApproval approval;
            Assert::IsTrue(approval.Begin(L"request"));
            Assert::IsFalse(approval.Complete(L"other", LaunchDecision::Skipped));
            Assert::IsTrue(approval.Complete(L"request", LaunchDecision::Skipped));
            Assert::IsTrue(approval.WaitFor(std::chrono::milliseconds(0)).value() == LaunchDecision::Skipped);
            Assert::IsFalse(approval.Acknowledge(L"request"));
            Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Approved));
        }

        TEST_METHOD (ApprovalDoesNotCarryOverToAnotherLaunch)
        {
            PendingLaunchApproval approval;
            Assert::IsTrue(approval.Begin(L"first"));
            Assert::IsTrue(approval.Acknowledge(L"first"));
            Assert::IsTrue(approval.Complete(L"first", LaunchDecision::Approved));
            approval.End();
            Assert::IsFalse(approval.Complete(L"first", LaunchDecision::Approved));
            Assert::IsFalse(approval.Acknowledge(L"first"));
            Assert::IsTrue(approval.Begin(L"second"));
            Assert::IsFalse(approval.Complete(L"first", LaunchDecision::Approved));
            Assert::IsFalse(approval.WaitFor(std::chrono::milliseconds(0)).has_value());
        }

        TEST_METHOD (CancelWakesTheWaiterAndRejectsLateApproval)
        {
            PendingLaunchApproval approval;
            Assert::IsTrue(approval.Begin(L"request"));
            auto decision = std::async(std::launch::async, [&] { return approval.WaitFor(std::chrono::seconds(5)); });
            approval.Cancel();
            Assert::IsTrue(decision.wait_for(std::chrono::seconds(1)) == std::future_status::ready);
            Assert::IsTrue(decision.get().value() == LaunchDecision::Canceled);
            Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Approved));
            approval.End();
            Assert::IsFalse(approval.Begin(L"next"));
        }

        TEST_METHOD (OverlappingRequestsAreRejected)
        {
            PendingLaunchApproval approval;
            Assert::IsFalse(approval.Begin(L""));
            Assert::IsTrue(approval.Begin(L"first"));
            Assert::IsFalse(approval.Begin(L"second"));
        }

        TEST_METHOD (LateDisplayAcknowledgementCannotReviveExpiredRequest)
        {
            PendingLaunchApproval approval;
            const auto now = PendingLaunchApproval::Clock::now();
            Assert::IsTrue(approval.Begin(L"request", now));
            Assert::IsFalse(approval.Evaluate(now + std::chrono::milliseconds(9999)).has_value());
            Assert::IsFalse(approval.Acknowledge(L"request", now + std::chrono::seconds(10)));
            Assert::IsTrue(approval.Evaluate(now).value() == LaunchDecision::TimedOut);
            Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Approved, now));
            Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Skipped, now));
        }

        TEST_METHOD (OnlyRunAndSkipAreResponses)
        {
            PendingLaunchApproval approval;
            Assert::IsTrue(approval.Begin(L"request"));
            Assert::IsTrue(approval.Acknowledge(L"request"));
            for (const auto decision : { LaunchDecision::Canceled, LaunchDecision::UiUnavailable, LaunchDecision::TimedOut, LaunchDecision::InvalidResponse })
            {
                Assert::IsFalse(approval.Complete(L"request", decision));
                Assert::IsFalse(approval.Evaluate().has_value());
            }
        }

        TEST_METHOD (HumanDecisionHasNoDeadlineAfterDisplay)
        {
            PendingLaunchApproval approval;
            auto now = PendingLaunchApproval::Clock::now();
            Assert::IsTrue(approval.Begin(L"request", now));
            Assert::IsTrue(approval.Acknowledge(L"request", now));
            now += std::chrono::hours(24);
            Assert::IsFalse(approval.Evaluate(now).has_value());
            Assert::IsTrue(approval.Complete(L"request", LaunchDecision::Approved, now));
        }

        TEST_METHOD (DisplayAcknowledgementCannotReviveFailedRequest)
        {
            for (const auto failure : { LaunchDecision::UiUnavailable, LaunchDecision::InvalidResponse, LaunchDecision::TimedOut })
            {
                PendingLaunchApproval approval;
                Assert::IsTrue(approval.Begin(L"request"));
                approval.Fail(failure);
                Assert::IsFalse(approval.Acknowledge(L"request"));
                Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Skipped));
                Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Approved));
                Assert::IsTrue(approval.Evaluate().value() == failure);
            }
        }

        TEST_METHOD (InfrastructureFailureWakesWaiterAndRejectsApproval)
        {
            for (const auto failure : { LaunchDecision::UiUnavailable, LaunchDecision::InvalidResponse, LaunchDecision::TimedOut })
            {
                PendingLaunchApproval approval;
                Assert::IsTrue(approval.Begin(L"request"));
                Assert::IsTrue(approval.Acknowledge(L"request"));
                auto decision = std::async(std::launch::async, [&] { return approval.WaitFor(std::chrono::seconds(5)); });
                approval.Fail(failure);
                Assert::IsTrue(decision.wait_for(std::chrono::seconds(1)) == std::future_status::ready);
                Assert::IsTrue(decision.get().value() == failure);
                Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Approved));
            }
        }

        TEST_METHOD (CancelTakesPrecedenceOverAnAlreadyReceivedApproval)
        {
            PendingLaunchApproval approval;
            Assert::IsTrue(approval.Begin(L"request"));
            Assert::IsTrue(approval.Acknowledge(L"request"));
            Assert::IsTrue(approval.Complete(L"request", LaunchDecision::Approved));
            approval.Cancel();
            Assert::IsTrue(approval.Evaluate().value() == LaunchDecision::Canceled);
        }
    };
}
