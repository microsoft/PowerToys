// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "SignatureVerification.h"
#include <winrt/Windows.ApplicationModel.h>

namespace PackageVerification
{
    struct ApplicationIdentity
    {
        std::wstring aumid;
        std::wstring familyName;
        std::wstring applicationId;
    };

    struct PackageState
    {
        winrt::Windows::ApplicationModel::PackageSignatureKind signatureKind{
            winrt::Windows::ApplicationModel::PackageSignatureKind::None
        };
        bool identityMatches{};
        bool developmentMode{};
        bool externalContent{};
        bool mutableContent{};
        bool stub{};
        bool statusOk{};
        bool modified{};
        bool integrityChecked{};
        bool integrityValid{};
    };

    std::optional<ApplicationIdentity> ParseTarget(const std::wstring& target);
    SignatureVerification::Result Evaluate(const PackageState& state);
    SignatureVerification::LaunchTarget Verify(const std::wstring& target, const std::function<bool()>& isCanceled);
    bool IsCurrent(const SignatureVerification::LaunchTarget& target, const std::function<bool()>& isCanceled);

    namespace details
    {
        struct Registration
        {
            SignatureVerification::PackageIdentity identity;
            PackageState state;
        };

        using RegistrationResolver = std::function<std::optional<Registration>(const ApplicationIdentity&, const std::function<bool()>&)>;

        bool IsCurrent(const SignatureVerification::LaunchTarget& target, const std::function<bool()>& isCanceled, const RegistrationResolver& resolve);
    }
}
