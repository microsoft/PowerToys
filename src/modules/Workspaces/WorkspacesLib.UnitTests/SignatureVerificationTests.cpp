// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include <WorkspacesLib/SignatureVerification.h>
#include <WorkspacesLib/PendingLaunchApproval.h>
#include <WorkspacesLib/PackageVerification.h>
#include <WorkspacesLib/LauncherUiMessage.h>
#include <WorkspacesLauncher/AppLauncher.h>
#include <softpub.h>
#include <wincrypt.h>

#include <array>
#include <filesystem>
#include <fstream>
#include <future>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using SignatureVerification::Status;

namespace WorkspacesLibUnitTests
{
    namespace
    {
        struct TemporaryExecutable
        {
            std::wstring path;

            TemporaryExecutable()
            {
                wchar_t temporary[MAX_PATH]{};
                Assert::IsTrue(GetTempFileNameW(std::filesystem::temp_directory_path().c_str(), L"wsg", 0, temporary) != 0);
                path = std::wstring(temporary) + L".exe";
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

        TEST_METHOD (CheckedFileCannotBeRewrittenWhileHeld)
        {
            TemporaryExecutable fixture;
            const auto executable = SignatureVerification::Verify(fixture.path);
            Assert::IsTrue(static_cast<bool>(executable.file));
            wil::unique_hfile writer(CreateFileW(fixture.path.c_str(), GENERIC_WRITE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr));
            Assert::IsFalse(static_cast<bool>(writer));
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

    TEST_CLASS (PackageVerificationTests)
    {
        static PackageVerification::details::Registration RegisteredPackage()
        {
            PackageVerification::details::Registration registration;
            registration.identity.fullName = L"PowerToys.Test_1.0.0.0_x64__8wekyb3d8bbwe";
            registration.identity.applicationUserModelId = L"PowerToys.Test_8wekyb3d8bbwe!App";
            registration.identity.installedPath = L"C:\\Packages\\Test";
            registration.identity.effectivePath = registration.identity.installedPath;
            registration.state = HealthyPackage();
            registration.identity.signatureKind = static_cast<int32_t>(registration.state.signatureKind);
            return registration;
        }

        static SignatureVerification::LaunchTarget MissingPackageTarget()
        {
            SignatureVerification::LaunchTarget target;
            target.path = L"shell:AppsFolder\\" + RegisteredPackage().identity.applicationUserModelId;
            target.result = { Status::UnableToVerify, HRESULT_FROM_WIN32(ERROR_NOT_FOUND) };
            return target;
        }

        static constexpr PackageVerification::PackageState HealthyPackage()
        {
            PackageVerification::PackageState state;
            state.identityMatches = true;
            state.signatureKind = winrt::Windows::ApplicationModel::PackageSignatureKind::Store;
            state.statusOk = true;
            state.integrityChecked = true;
            state.integrityValid = true;
            return state;
        }

    public:
        TEST_METHOD (ParseRegisteredApplicationIdentity)
        {
            const auto identity = PackageVerification::ParseTarget(L"shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App");
            Assert::IsTrue(identity.has_value());
            Assert::AreEqual(std::wstring(L"Microsoft.WindowsTerminal_8wekyb3d8bbwe"), identity->familyName);
            Assert::AreEqual(std::wstring(L"App"), identity->applicationId);
            Assert::IsTrue(PackageVerification::ParseTarget(L"SHELL:APPSFOLDER\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App").has_value());
        }

        TEST_METHOD (OtherShellTargetsAreNotPackageIdentities)
        {
            for (const auto target : { L"shell:AppsFolder\\Example.App", L"shell:AppsFolder\\", L"https://example.invalid", L"Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", L"C:\\application.exe" })
            {
                Assert::IsFalse(PackageVerification::ParseTarget(target).has_value());
            }
            std::wstring embeddedNull = L"shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App";
            embeddedNull.push_back(L'\0');
            embeddedNull += L"ignored";
            Assert::IsFalse(PackageVerification::ParseTarget(embeddedNull).has_value());
        }

        TEST_METHOD (StoreAndSystemPackagesNeedCompleteVerification)
        {
            auto state = HealthyPackage();
            Assert::IsTrue(PackageVerification::Evaluate(state).IsVerified());
            state.signatureKind = winrt::Windows::ApplicationModel::PackageSignatureKind::System;
            Assert::IsTrue(PackageVerification::Evaluate(state).IsVerified());
            state.integrityChecked = false;
            Assert::IsFalse(PackageVerification::Evaluate(state).IsVerified());
            Assert::AreEqual(std::wstring(L"package-verification-unavailable"), PackageVerification::Evaluate(state).Reason());
        }

        TEST_METHOD (DevelopmentRegistrationIsNotDeveloperSigning)
        {
            auto state = HealthyPackage();
            state.developmentMode = true;
            Assert::AreEqual(std::wstring(L"package-development"), PackageVerification::Evaluate(state).Reason());
            state.developmentMode = false;
            state.signatureKind = winrt::Windows::ApplicationModel::PackageSignatureKind::Developer;
            const auto result = PackageVerification::Evaluate(state);
            Assert::IsTrue(result.status == Status::UnableToVerify);
            Assert::AreEqual(std::wstring(L"package-signing-policy"), result.Reason());
            state.signatureKind = winrt::Windows::ApplicationModel::PackageSignatureKind::Enterprise;
            Assert::AreEqual(std::wstring(L"package-signing-policy"), PackageVerification::Evaluate(state).Reason());
        }

        TEST_METHOD (UnsignedOrUnknownSigningSourceIsNotAccepted)
        {
            auto state = HealthyPackage();
            state.signatureKind = winrt::Windows::ApplicationModel::PackageSignatureKind::None;
            Assert::IsTrue(PackageVerification::Evaluate(state).status == Status::Unsigned);
            state.signatureKind = static_cast<winrt::Windows::ApplicationModel::PackageSignatureKind>(99);
            Assert::IsFalse(PackageVerification::Evaluate(state).IsVerified());
        }

        TEST_METHOD (ExternalAndMutablePayloadsRequireConfirmation)
        {
            auto state = HealthyPackage();
            state.externalContent = true;
            Assert::AreEqual(std::wstring(L"package-external-content"), PackageVerification::Evaluate(state).Reason());
            state.externalContent = false;
            state.mutableContent = true;
            Assert::AreEqual(std::wstring(L"package-external-content"), PackageVerification::Evaluate(state).Reason());
        }

        TEST_METHOD (MissingUnavailableAndStubPackagesAreNotAccepted)
        {
            auto state = HealthyPackage();
            state.identityMatches = false;
            Assert::AreEqual(std::wstring(L"package-not-found"), PackageVerification::Evaluate(state).Reason());
            state.identityMatches = true;
            state.statusOk = false;
            Assert::AreEqual(std::wstring(L"package-unavailable"), PackageVerification::Evaluate(state).Reason());
            state.statusOk = true;
            state.stub = true;
            Assert::AreEqual(std::wstring(L"package-unavailable"), PackageVerification::Evaluate(state).Reason());
        }

        TEST_METHOD (ModifiedOrFailedIntegrityIsDistinctFromUnknown)
        {
            auto state = HealthyPackage();
            state.modified = true;
            Assert::IsTrue(PackageVerification::Evaluate(state).status == Status::InvalidSignature);
            state.modified = false;
            state.integrityValid = false;
            Assert::AreEqual(std::wstring(L"package-integrity-failed"), PackageVerification::Evaluate(state).Reason());
        }

        TEST_METHOD (RegistrationIdentityIncludesVersionAndPayloadLocation)
        {
            SignatureVerification::PackageIdentity first;
            first.fullName = L"Example_1.0.0.0_x64__8wekyb3d8bbwe";
            first.applicationUserModelId = L"Example_8wekyb3d8bbwe!App";
            first.installedPath = L"C:\\Packages\\Example";
            first.effectivePath = first.installedPath;
            auto second = first;
            Assert::IsTrue(first == second);
            second.fullName = L"Example_2.0.0.0_x64__8wekyb3d8bbwe";
            Assert::IsFalse(first == second);
            second = first;
            second.externalPath = L"C:\\Other";
            Assert::IsFalse(first == second);
            second = first;
            second.mutablePath = L"C:\\Modifiable";
            Assert::IsFalse(first == second);
            second = first;
            second.applicationUserModelId = L"Example_8wekyb3d8bbwe!Other";
            Assert::IsFalse(first == second);
        }

        TEST_METHOD (CanceledPackageVerificationDoesNotResolveOrLaunch)
        {
            const std::wstring path = L"shell:AppsFolder\\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App";
            const auto target = SignatureVerification::Verify(path, [] { return true; });
            Assert::IsFalse(target.result.IsVerified());
            Assert::AreEqual(static_cast<LONG>(HRESULT_FROM_WIN32(ERROR_CANCELLED)), target.result.error);
            Assert::AreEqual(path, target.path);
        }

        TEST_METHOD (UnregisteredPackageCannotBecomeVerifiedFromItsName)
        {
            const auto result = std::async(std::launch::async, [] {
                                    winrt::init_apartment(winrt::apartment_type::multi_threaded);
                                    auto uninitialize = wil::scope_exit([] { winrt::uninit_apartment(); });
                                    return SignatureVerification::Verify(L"shell:AppsFolder\\PowerToys.NonexistentSignatureFixture_8wekyb3d8bbwe!App").result;
                                }).get();
            Assert::IsFalse(result.IsVerified());
            Assert::AreEqual(std::wstring(L"package-not-found"), result.Reason());
        }

        TEST_METHOD (MissingPackageIdentityDoesNotSkipResolution)
        {
            auto target = MissingPackageTarget();
            int lookups = 0;
            const auto current = PackageVerification::details::IsCurrent(target, {}, [&](const auto& application, const auto&) -> std::optional<PackageVerification::details::Registration> {
                ++lookups;
                Assert::AreEqual(RegisteredPackage().identity.applicationUserModelId, application.aumid);
                return RegisteredPackage();
            });
            Assert::IsFalse(current);
            Assert::AreEqual(1, lookups);
        }

        TEST_METHOD (MissingPackageIdentityRejectsAResolutionError)
        {
            auto target = MissingPackageTarget();
            int lookups = 0;
            const auto current = PackageVerification::details::IsCurrent(target, {}, [&](const auto&, const auto&) -> std::optional<PackageVerification::details::Registration> {
                ++lookups;
                winrt::throw_hresult(E_ACCESSDENIED);
            });
            Assert::IsFalse(current);
            Assert::AreEqual(1, lookups);
        }

        TEST_METHOD (PublicRevalidationReachesLookupWithAMissingIdentity)
        {
            auto target = MissingPackageTarget();
            int cancellationChecks = 0;
            Assert::IsFalse(PackageVerification::IsCurrent(target, [&] {
                // Stop inside the real resolver before it consults installed packages.
                return ++cancellationChecks > 1;
            }));
            Assert::AreEqual(2, cancellationChecks);
        }

        TEST_METHOD (NonPackageTargetsDoNotResolvePackages)
        {
            SignatureVerification::LaunchTarget target;
            target.path = L"C:\\Example\\app.exe";
            int cancellationChecks = 0;
            Assert::IsTrue(PackageVerification::IsCurrent(target, [&] {
                ++cancellationChecks;
                return false;
            }));
            Assert::AreEqual(1, cancellationChecks);
            target.package.emplace();
            Assert::IsFalse(PackageVerification::IsCurrent(target, {}));
        }

        TEST_METHOD (PackageRegistrationThatRemainsAbsentIsUnchanged)
        {
            auto target = MissingPackageTarget();
            int lookups = 0;
            const auto current = PackageVerification::details::IsCurrent(target, {}, [&](const auto&, const auto&) -> std::optional<PackageVerification::details::Registration> {
                ++lookups;
                return std::nullopt;
            });
            Assert::IsTrue(current);
            Assert::AreEqual(1, lookups);
        }

        TEST_METHOD (ExistingPackageRegistrationMustRemainPresentAndIdentical)
        {
            auto target = MissingPackageTarget();
            const auto original = RegisteredPackage();
            target.package = original.identity;
            Assert::IsTrue(PackageVerification::details::IsCurrent(target, {}, [&](const auto&, const auto&) { return std::optional{ original }; }));
            Assert::IsFalse(PackageVerification::details::IsCurrent(target, {}, [](const auto&, const auto&) -> std::optional<PackageVerification::details::Registration> { return std::nullopt; }));
            auto updated = original;
            updated.identity.fullName = L"PowerToys.Test_2.0.0.0_x64__8wekyb3d8bbwe";
            Assert::IsFalse(PackageVerification::details::IsCurrent(target, {}, [&](const auto&, const auto&) { return std::optional{ updated }; }));
        }

        TEST_METHOD (PreviouslyVerifiedPackageMustRetainEligibleMetadata)
        {
            auto target = MissingPackageTarget();
            auto current = RegisteredPackage();
            target.package = current.identity;
            target.result = { Status::Verified, ERROR_SUCCESS };
            Assert::IsTrue(PackageVerification::details::IsCurrent(target, {}, [&](const auto&, const auto&) { return std::optional{ current }; }));
            current.state.statusOk = false;
            Assert::IsFalse(PackageVerification::details::IsCurrent(target, {}, [&](const auto&, const auto&) { return std::optional{ current }; }));
        }

        TEST_METHOD (CanceledRevalidationDoesNotConsultTheResolver)
        {
            auto target = MissingPackageTarget();
            int lookups = 0;
            Assert::IsFalse(PackageVerification::details::IsCurrent(target, [] { return true; }, [&](const auto&, const auto&) {
                ++lookups;
                return std::optional{ RegisteredPackage() }; }));
            Assert::AreEqual(0, lookups);
        }
    };

    TEST_CLASS (ElevatedLaunchGateTests)
    {
    public:
        TEST_METHOD (DecliningUnsignedExecutableDoesNotLaunch)
        {
            TemporaryExecutable fixture;
            WorkspacesData::WorkspacesProject::Application app{};
            app.name = L"Signature test";
            app.path = fixture.path;
            app.isElevated = true;
            AppLauncher::ErrorList errors;
            int requests = 0;
            const auto result = AppLauncher::Launch(app, errors, [&](const auto& path, const auto&, const auto& verification) {
                ++requests;
                Assert::AreEqual(fixture.path, path);
                Assert::IsTrue(verification.status == Status::Unsigned);
                return LaunchDecision::Skipped; }, [] { return false; });
            Assert::IsTrue(result == AppLauncher::LaunchResult::Skipped);
            Assert::AreEqual(1, requests);
            Assert::IsTrue(errors.empty());
        }

        TEST_METHOD (DecliningPwaTargetDoesNotTryNativeFallback)
        {
            TemporaryExecutable fixture;
            WorkspacesData::WorkspacesProject::Application app{};
            app.name = L"Signature test";
            app.path = fixture.path;
            app.isElevated = true;
            app.pwaAppId = L"prototype-test";
            app.appUserModelId = L"PowerToys.SignatureTest";
            app.version = L"1";
            AppLauncher::ErrorList errors;
            int requests = 0;
            const auto result = AppLauncher::Launch(app, errors, [&](const auto& path, const auto&, const auto&) {
                ++requests;
                Assert::AreEqual(std::wstring(L"shell:AppsFolder\\PowerToys.SignatureTest"), path);
                return LaunchDecision::Skipped; }, [] { return false; });
            Assert::IsTrue(result == AppLauncher::LaunchResult::Skipped);
            Assert::AreEqual(1, requests);
            Assert::IsTrue(errors.empty());
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

        TEST_METHOD (DecliningUnresolvedPackageDoesNotTryTheSavedExecutable)
        {
            const auto result = std::async(std::launch::async, [] {
                                    winrt::init_apartment(winrt::apartment_type::multi_threaded);
                                    auto uninitialize = wil::scope_exit([] { winrt::uninit_apartment(); });
                                    TemporaryExecutable fixture;
                                    WorkspacesData::WorkspacesProject::Application app{};
                                    app.path = fixture.path;
                                    app.isElevated = true;
                                    app.pwaAppId = L"test";
                                    app.appUserModelId = L"PowerToys.NonexistentSignatureFixture_8wekyb3d8bbwe!App";
                                    app.version = L"1";
                                    AppLauncher::ErrorList errors;
                                    int requests = 0;
                                    const auto outcome = AppLauncher::Launch(app, errors, [&](const auto& path, const auto&, const auto& verification) {
                    ++requests;
                    Assert::AreEqual(std::wstring(L"shell:AppsFolder\\") + app.appUserModelId, path);
                    Assert::AreEqual(std::wstring(L"package-not-found"), verification.Reason());
                    return LaunchDecision::Skipped; }, [] { return false; });
                                    Assert::AreEqual(1, requests);
                                    Assert::IsTrue(errors.empty());
                                    return outcome;
                                }).get();
            Assert::IsTrue(result == AppLauncher::LaunchResult::Skipped);
        }

        TEST_METHOD (ConfirmationFailuresStopAllFallbacksAndAreNotUserCancellation)
        {
            TemporaryExecutable fixture;
            WorkspacesData::WorkspacesProject::Application app{};
            app.path = fixture.path;
            app.isElevated = true;
            app.pwaAppId = L"test";
            app.appUserModelId = L"PowerToys.SignatureTest";
            app.version = L"1";
            for (const auto failure : { LaunchDecision::UiUnavailable, LaunchDecision::TimedOut, LaunchDecision::InvalidResponse, LaunchDecision::TargetChanged })
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
        TEST_METHOD (ProtocolRequiresVersionAndTypedChoice)
        {
            const auto approved = LauncherUiMessage::Parse(LR"({"protocolVersion":1,"type":"elevation-response","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}","choice":"run"})");
            Assert::IsTrue(approved.has_value());
            Assert::IsTrue(approved->type == LauncherUiMessage::Type::Response);
            Assert::IsTrue(approved->decision == LaunchDecision::Approved);
            const auto skipped = LauncherUiMessage::Parse(LR"({"protocolVersion":1,"type":"elevation-response","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}","choice":"skip"})");
            Assert::IsTrue(skipped.has_value());
            Assert::IsTrue(skipped->decision == LaunchDecision::Skipped);
            Assert::IsTrue(LauncherUiMessage::Parse(LR"({"protocolVersion":1,"type":"ready"})")->type == LauncherUiMessage::Type::Ready);
            Assert::IsTrue(LauncherUiMessage::Parse(LR"({"protocolVersion":1,"type":"cancel"})")->type == LauncherUiMessage::Type::Cancel);
        }

        TEST_METHOD (MalformedOrLegacyMessagesCannotApprove)
        {
            for (const auto text : {
                     L"ready",
                     L"cancel",
                     L"null",
                     L"[]",
                     LR"({"type":"ready"})",
                     LR"({"protocolVersion":2,"type":"ready"})",
                     LR"({"protocolVersion":"1","type":"ready"})",
                     LR"({"protocolVersion":1,"type":"elevation-response","requestId":"request","choice":"run"})",
                     LR"({"protocolVersion":1,"type":"elevation-response","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}","allow":true})",
                     LR"({"protocolVersion":1,"type":"elevation-response","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}","choice":true})",
                     LR"({"protocolVersion":1,"type":"elevation-response","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}","choice":"always"})",
                     LR"({"protocolVersion":1,"type":"other","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB}"})",
                     LR"({"protocolVersion":1,"type":"heartbeat","requestId":"{F4CE1D14-2DAE-47F9-B775-D48D5B494CAB\u0000"})",
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
            PendingLaunchApproval approval;
            Assert::IsTrue(approval.Begin(L"request"));
            Assert::IsTrue(approval.Acknowledge(L"request"));
            Assert::IsTrue(approval.Complete(L"request", LaunchDecision::Skipped));
            Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Approved));
            Assert::IsTrue(approval.WaitFor(std::chrono::milliseconds(0)).value() == LaunchDecision::Skipped);
        }

        TEST_METHOD (ApprovalDoesNotCarryOverToAnotherLaunch)
        {
            PendingLaunchApproval approval;
            Assert::IsTrue(approval.Begin(L"first"));
            Assert::IsTrue(approval.Acknowledge(L"first"));
            Assert::IsTrue(approval.Complete(L"first", LaunchDecision::Approved));
            approval.End();
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
            Assert::IsFalse(approval.Acknowledge(L"request", now + PendingLaunchApproval::DisplayTimeout));
            Assert::IsTrue(approval.Evaluate(now).value() == LaunchDecision::TimedOut);
            Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Approved, now));
        }

        TEST_METHOD (HeartbeatRequiresDisplayedMatchingRequest)
        {
            PendingLaunchApproval approval;
            const auto now = PendingLaunchApproval::Clock::now();
            Assert::IsTrue(approval.Begin(L"request", now));
            Assert::IsFalse(approval.Heartbeat(L"request", now));
            Assert::IsTrue(approval.Acknowledge(L"request", now));
            Assert::IsFalse(approval.Heartbeat(L"other", now + std::chrono::seconds(5)));
            Assert::IsTrue(approval.Evaluate(now + PendingLaunchApproval::HeartbeatTimeout).value() == LaunchDecision::UiUnavailable);
        }

        TEST_METHOD (HumanDecisionTimeIsUnlimitedWhileTheUiIsResponsive)
        {
            PendingLaunchApproval approval;
            auto now = PendingLaunchApproval::Clock::now();
            Assert::IsTrue(approval.Begin(L"request", now));
            Assert::IsTrue(approval.Acknowledge(L"request", now));
            for (int i = 0; i < 3600; ++i)
            {
                now += std::chrono::seconds(1);
                Assert::IsTrue(approval.Heartbeat(L"request", now));
                Assert::IsFalse(approval.Evaluate(now).has_value());
            }
            Assert::IsTrue(approval.Complete(L"request", LaunchDecision::Approved, now));
        }

        TEST_METHOD (LateHeartbeatAndApprovalCannotReviveAnUnresponsiveUi)
        {
            for (const bool heartbeatFirst : { false, true })
            {
                PendingLaunchApproval approval;
                auto now = PendingLaunchApproval::Clock::now();
                Assert::IsTrue(approval.Begin(L"request", now));
                Assert::IsTrue(approval.Acknowledge(L"request", now));
                now += PendingLaunchApproval::HeartbeatTimeout;
                if (heartbeatFirst)
                {
                    Assert::IsFalse(approval.Heartbeat(L"request", now));
                }
                Assert::IsFalse(approval.Complete(L"request", LaunchDecision::Approved, now));
                Assert::IsTrue(approval.Evaluate(now).value() == LaunchDecision::UiUnavailable);
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
