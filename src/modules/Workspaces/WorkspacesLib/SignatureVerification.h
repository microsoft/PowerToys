// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <windows.h>
#include <wintrust.h>
#include <functional>
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

        bool IsVerified() const noexcept;
        std::wstring Reason() const;
    };

    struct LaunchTarget
    {
        std::wstring path;
        Result result;
        // Keep the checked file open without write/delete sharing through the launch.
        wil::unique_hfile file;
    };

    Status Classify(LONG error, bool confirmedUnsigned = false) noexcept;
    const wchar_t* StatusName(Status status) noexcept;
    // Includes unresolved relative EXE paths; classification does not establish trust.
    bool IsExecutableTarget(const std::wstring& path);
    LaunchTarget Verify(const std::wstring& path, const std::function<bool()>& isCanceled = {});
}
