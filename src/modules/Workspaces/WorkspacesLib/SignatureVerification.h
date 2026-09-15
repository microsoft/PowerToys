// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <windows.h>
#include <wintrust.h>
#include <cstdint>
#include <functional>
#include <optional>
#include <string>
#include <wil/resource.h>

namespace SignatureVerification
{
    enum class Status
    {
        Verified,
        Unsigned,
        InvalidSignature,
        CertificateUntrusted,
        UnableToVerify,
    };

    struct Result
    {
        Status status{ Status::UnableToVerify };
        LONG error{ TRUST_E_SUBJECT_FORM_UNKNOWN };
        std::wstring publisher;
        std::wstring source;
        std::wstring detailReason;

        bool IsVerified() const noexcept;
        std::wstring Reason() const;
    };

    struct PackageIdentity
    {
        std::wstring fullName;
        std::wstring applicationUserModelId;
        std::wstring installedPath;
        std::wstring effectivePath;
        std::wstring externalPath;
        std::wstring mutablePath;
        int32_t signatureKind{};
        bool developmentMode{};

        bool operator==(const PackageIdentity&) const = default;
    };

    struct LaunchTarget
    {
        std::wstring path;
        Result result;
        // Keep the checked file open without write/delete sharing through the launch.
        wil::unique_hfile file;
        std::optional<PackageIdentity> package;
    };

    Status Classify(LONG error, bool confirmedUnsigned = false) noexcept;
    const wchar_t* StatusName(Status status) noexcept;
    LaunchTarget Verify(const std::wstring& path, const std::function<bool()>& isCanceled = {});
    bool IsCurrent(const LaunchTarget& target, const std::function<bool()>& isCanceled = {});
}
