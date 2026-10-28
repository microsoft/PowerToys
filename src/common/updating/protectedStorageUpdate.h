// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <windows.h>

#include <filesystem>
#include <string>
#include <string_view>

namespace updating
{
    enum class ProtectedStorageSyncState
    {
        Completed,
        RestartRequired,
        DeferredOwnerContext,
        RetryRequired,
        OutcomeUnknown,
    };

    struct ProtectedStorageSyncResult
    {
        ProtectedStorageSyncState state;
        DWORD nativeCode;
    };

    inline ProtectedStorageSyncResult ClassifyProtectedStorageSyncExit(DWORD code)
    {
        if (code == ERROR_SUCCESS)
        {
            return { ProtectedStorageSyncState::Completed, code };
        }
        if (code == ERROR_SUCCESS_REBOOT_REQUIRED || code == ERROR_SUCCESS_REBOOT_INITIATED)
        {
            return { ProtectedStorageSyncState::RestartRequired, code };
        }
        if (code == WAIT_TIMEOUT || code == ERROR_IO_PENDING)
        {
            return { ProtectedStorageSyncState::OutcomeUnknown, code };
        }
        return { ProtectedStorageSyncState::RetryRequired, code };
    }

    inline bool IsProtectedStorageOwnerSid(const std::wstring& sid)
    {
        std::wstring_view remaining = sid;
        if (remaining.starts_with(L"S-1-5-21-"))
        {
            remaining.remove_prefix(9);
        }
        else if (remaining.starts_with(L"S-1-12-1-"))
        {
            remaining.remove_prefix(9);
        }
        else
        {
            return false;
        }
        for (unsigned part = 0; part < 4; ++part)
        {
            const auto end = remaining.find(L'-');
            const auto component = remaining.substr(0, end);
            if (component.empty())
            {
                return false;
            }
            DWORD value = 0;
            for (const auto character : component)
            {
                if (character < L'0' || character > L'9' ||
                    value > (MAXDWORD - static_cast<DWORD>(character - L'0')) / 10)
                {
                    return false;
                }
                value = value * 10 + static_cast<DWORD>(character - L'0');
            }
            if (part == 3)
            {
                return end == std::wstring_view::npos;
            }
            if (end == std::wstring_view::npos)
            {
                return false;
            }
            remaining.remove_prefix(end + 1);
        }
        return false;
    }

    inline const wchar_t* ProtectedStorageSyncStateName(ProtectedStorageSyncState state)
    {
        switch (state)
        {
        case ProtectedStorageSyncState::Completed:
            return L"completed";
        case ProtectedStorageSyncState::RestartRequired:
            return L"restartRequired";
        case ProtectedStorageSyncState::DeferredOwnerContext:
            return L"deferredOwnerContext";
        case ProtectedStorageSyncState::RetryRequired:
            return L"retryRequired";
        case ProtectedStorageSyncState::OutcomeUnknown:
            return L"outcomeUnknown";
        }
        return L"invalidState";
    }

    // This entry never elevates or provisions a new service. The owner Setup
    // checks the real installation/version state before performing maintenance.
    ProtectedStorageSyncResult SynchronizeProtectedStorageForOwner(const std::filesystem::path& installDirectory);

    constexpr bool ShouldRemoveOwnerCarrier(bool perUserBundle, bool explicitUninstall, bool relatedBundle, bool mainSucceeded)
    {
        return perUserBundle && explicitUninstall && !relatedBundle && mainSucceeded;
    }

    // Called only by the outer bundle after its main MSI transaction completes.
    ProtectedStorageSyncResult RemoveProtectedStorageForOwner(const std::filesystem::path& setupDirectory);
}
