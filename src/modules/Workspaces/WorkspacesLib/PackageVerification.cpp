// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "PackageVerification.h"
#include "PackageVerificationAsync.h"

#include <appmodel.h>
#include <chrono>
#include <string_view>

#include <common/logger/logger.h>
#include <winrt/Windows.ApplicationModel.Core.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.Storage.h>

namespace PackageVerification
{
    using SignatureVerification::Status;
    using winrt::Windows::ApplicationModel::PackageSignatureKind;

    namespace
    {
        constexpr std::wstring_view AppsFolderPrefix = L"shell:AppsFolder\\";

        struct ResolvedPackage
        {
            winrt::Windows::ApplicationModel::Package package{ nullptr };
            SignatureVerification::PackageIdentity identity;
            PackageState state;
        };

        SignatureVerification::Result Failure(const wchar_t* reason, LONG error, Status status = Status::UnableToVerify)
        {
            SignatureVerification::Result result{ status, error };
            result.source = L"msix";
            result.detailReason = reason;
            return result;
        }

        bool MetadataEligible(const PackageState& state)
        {
            return state.identityMatches && !state.developmentMode && !state.externalContent &&
                   !state.mutableContent && !state.stub && state.statusOk && !state.modified &&
                   (state.signatureKind == PackageSignatureKind::Store || state.signatureKind == PackageSignatureKind::System);
        }

        std::optional<ResolvedPackage> Resolve(const ApplicationIdentity& identity, const std::function<bool()>& isCanceled)
        {
            details::ThrowIfCanceled(isCanceled);
            winrt::Windows::Management::Deployment::PackageManager manager;
            std::optional<ResolvedPackage> resolved;
            for (const auto& package : manager.FindPackagesForUser(L""))
            {
                details::ThrowIfCanceled(isCanceled);
                if (package.Id().FamilyName() != identity.familyName || package.IsFramework() || package.IsResourcePackage())
                {
                    continue;
                }
                const auto entries = details::Await(package.GetAppListEntriesAsync(), std::chrono::seconds(10), isCanceled);
                for (const auto& entry : entries)
                {
                    if (entry.AppUserModelId() != identity.aumid)
                    {
                        continue;
                    }
                    if (resolved)
                    {
                        winrt::throw_hresult(HRESULT_FROM_WIN32(ERROR_DUP_NAME));
                    }

                    ResolvedPackage candidate;
                    candidate.package = package;
                    candidate.identity.fullName = package.Id().FullName().c_str();
                    candidate.identity.applicationUserModelId = entry.AppUserModelId().c_str();
                    candidate.identity.installedPath = package.InstalledLocation().Path().c_str();
                    candidate.identity.effectivePath = candidate.identity.installedPath;
                    candidate.identity.developmentMode = package.IsDevelopmentMode();
                    candidate.identity.signatureKind = static_cast<int32_t>(package.SignatureKind());
                    candidate.state.identityMatches = true;
                    candidate.state.developmentMode = candidate.identity.developmentMode;
                    candidate.state.signatureKind = package.SignatureKind();
                    const auto status = package.Status();
                    candidate.state.statusOk = status.VerifyIsOK();
                    candidate.state.modified = status.Modified() || status.Tampered();

                    // External-location and mutable payloads require a separate file-based policy.
                    if (const auto extended = package.try_as<winrt::Windows::ApplicationModel::IPackage8>())
                    {
                        candidate.identity.effectivePath = extended.EffectivePath().c_str();
                        candidate.identity.externalPath = extended.EffectiveExternalPath().c_str();
                        candidate.identity.mutablePath = extended.MutablePath().c_str();
                        candidate.state.externalContent = !candidate.identity.externalPath.empty();
                        candidate.state.mutableContent = !candidate.identity.mutablePath.empty() ||
                                                         candidate.identity.effectivePath != candidate.identity.installedPath;
                        candidate.state.stub = candidate.state.stub || extended.IsStub();
                    }
                    resolved = std::move(candidate);
                }
            }
            return resolved;
        }
    }

    std::optional<ApplicationIdentity> ParseTarget(const std::wstring& target)
    {
        if (target.size() <= AppsFolderPrefix.size() || target.find(L'\0') != std::wstring::npos ||
            _wcsnicmp(target.c_str(), AppsFolderPrefix.data(), AppsFolderPrefix.size()) != 0)
        {
            return std::nullopt;
        }

        ApplicationIdentity identity;
        identity.aumid = target.substr(AppsFolderPrefix.size());
        UINT32 familyLength = 0;
        UINT32 applicationLength = 0;
        if (ParseApplicationUserModelId(identity.aumid.c_str(), &familyLength, nullptr, &applicationLength, nullptr) != ERROR_INSUFFICIENT_BUFFER ||
            familyLength == 0 || applicationLength == 0)
        {
            return std::nullopt;
        }
        identity.familyName.resize(familyLength);
        identity.applicationId.resize(applicationLength);
        if (ParseApplicationUserModelId(identity.aumid.c_str(), &familyLength, identity.familyName.data(), &applicationLength, identity.applicationId.data()) != ERROR_SUCCESS)
        {
            return std::nullopt;
        }
        identity.familyName.resize(familyLength - 1);
        identity.applicationId.resize(applicationLength - 1);
        return identity;
    }

    SignatureVerification::Result Evaluate(const PackageState& state)
    {
        if (!state.identityMatches)
        {
            return Failure(L"package-not-found", HRESULT_FROM_WIN32(ERROR_NOT_FOUND));
        }
        if (state.developmentMode)
        {
            return Failure(L"package-development", HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED));
        }
        if (state.externalContent || state.mutableContent)
        {
            return Failure(L"package-external-content", HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED));
        }
        if (state.modified)
        {
            return Failure(L"package-integrity-failed", TRUST_E_BAD_DIGEST, Status::InvalidSignature);
        }
        if (!state.statusOk || state.stub)
        {
            return Failure(L"package-unavailable", HRESULT_FROM_WIN32(ERROR_NOT_READY));
        }
        if (state.signatureKind == PackageSignatureKind::None)
        {
            return Failure(L"package-unsigned", TRUST_E_NOSIGNATURE, Status::Unsigned);
        }
        if (state.signatureKind != PackageSignatureKind::Store && state.signatureKind != PackageSignatureKind::System)
        {
            // Developer is a signing-source category, not development mode or proof of distrust.
            return Failure(L"package-signing-policy", HRESULT_FROM_WIN32(ERROR_NOT_SUPPORTED));
        }
        if (!state.integrityChecked)
        {
            return Failure(L"package-verification-unavailable", E_PENDING);
        }
        if (!state.integrityValid)
        {
            return Failure(L"package-integrity-failed", TRUST_E_BAD_DIGEST, Status::InvalidSignature);
        }
        SignatureVerification::Result result{ Status::Verified, ERROR_SUCCESS };
        result.source = L"msix";
        return result;
    }

    SignatureVerification::LaunchTarget Verify(const std::wstring& path, const std::function<bool()>& isCanceled)
    {
        SignatureVerification::LaunchTarget target;
        target.path = path;
        target.result = Failure(L"package-not-found", HRESULT_FROM_WIN32(ERROR_NOT_FOUND));
        const auto application = ParseTarget(path);
        if (!application)
        {
            return target;
        }
        try
        {
            auto resolved = Resolve(application.value(), isCanceled);
            if (!resolved)
            {
                return target;
            }
            target.package = resolved->identity;
            if (MetadataEligible(resolved->state))
            {
                resolved->state.integrityValid = details::Await(resolved->package.VerifyContentIntegrityAsync(), std::chrono::seconds(30), isCanceled);
                resolved->state.integrityChecked = true;
            }
            target.result = Evaluate(resolved->state);
            if (target.result.IsVerified())
            {
                if (!PackageVerification::IsCurrent(target, isCanceled))
                {
                    target.result = Failure(L"package-changed", HRESULT_FROM_WIN32(ERROR_RETRY));
                }
                else
                {
                    target.result.publisher = resolved->package.Id().Publisher().c_str();
                }
            }
        }
        catch (const winrt::hresult_error& error)
        {
            Logger::warn(L"Package verification could not complete: {:#010x}", static_cast<uint32_t>(error.code().value));
            target.result = Failure(L"package-verification-unavailable", error.code().value);
        }
        return target;
    }

    bool IsCurrent(const SignatureVerification::LaunchTarget& target, const std::function<bool()>& isCanceled)
    {
        if (isCanceled && isCanceled())
        {
            return false;
        }
        if (!target.package)
        {
            return true;
        }
        const auto application = ParseTarget(target.path);
        if (!application)
        {
            return false;
        }
        try
        {
            const auto current = Resolve(application.value(), isCanceled);
            return current && current->identity == target.package.value() &&
                   (!target.result.IsVerified() || MetadataEligible(current->state));
        }
        catch (const winrt::hresult_error& error)
        {
            Logger::warn(L"Unable to revalidate package registration: {:#010x}", static_cast<uint32_t>(error.code().value));
            return false;
        }
    }
}
