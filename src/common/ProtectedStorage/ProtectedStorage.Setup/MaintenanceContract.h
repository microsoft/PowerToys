// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#pragma once
#include "SetupSupport.h"
#include <array>
#include <string_view>

namespace PowerToysProtectedStorage::Maintenance
{
    enum class RemovalMode
    {
        KeepData,
        PurgeData,
    };

    struct Request
    {
        std::wstring verb;
        std::wstring operation;
        RemovalMode removal = RemovalMode::KeepData;
        bool json = false;
        bool authorizeRepair = false;
    };

    struct RemovalPlan
    {
        bool removeService = true;
        bool removeCode = true;
        bool removeData = false;
        bool preserveInventory = true;
        bool suppressLegacyImport = false;
    };

    constexpr RemovalPlan PlanRemoval(RemovalMode mode)
    {
        return { true, true, mode == RemovalMode::PurgeData, true, mode == RemovalMode::PurgeData };
    }

    inline void ValidateRemovalReceipt(const std::wstring& expectedId, RemovalMode expectedMode,
                                       const std::wstring& actualId, const std::wstring& actualMode)
    {
        if (!setup::valid_id(expectedId) || actualId != expectedId ||
            actualMode != (expectedMode == RemovalMode::PurgeData ? L"purge" : L"keep"))
            throw failure("removal receipt is not bound to this authorization", ERROR_INVALID_OWNER);
    }

    struct OwnerBinding
    {
        DWORD pid = 0;
        ULONGLONG birth = 0;
        std::wstring nonce;
        std::wstring owner;
        std::wstring verb;
    };

    inline void ValidateOwnerBinding(const OwnerBinding& expected, const OwnerBinding& actual)
    {
        canonical_owner(actual.owner);
        if (!expected.pid || !expected.birth || !setup::valid_id(expected.nonce) ||
            expected.pid != actual.pid || expected.birth != actual.birth || expected.nonce != actual.nonce ||
            expected.owner != actual.owner || expected.verb != actual.verb)
            throw failure("original owner authorization binding mismatch", ERROR_INVALID_OWNER);
    }

    inline Request Parse(const std::vector<std::wstring>& arguments)
    {
        if (arguments.empty())
            throw failure("maintenance verb required", ERROR_BAD_ARGUMENTS);
        Request request{ arguments.front() };
        if (request.verb != L"inspect" && request.verb != L"sync" && request.verb != L"ensure" && request.verb != L"upgrade" &&
            request.verb != L"repair" && request.verb != L"remove" && request.verb != L"retry")
            throw failure("unsupported maintenance verb", ERROR_BAD_ARGUMENTS);
        bool removalSeen = false;
        for (size_t index = 1; index < arguments.size(); ++index)
        {
            const auto& argument = arguments[index];
            if (argument == L"--json" && !request.json)
                request.json = true;
            else if (argument == L"--authorize-repair" && request.verb == L"repair" && !request.authorizeRepair)
                request.authorizeRepair = true;
            else if ((argument == L"--keep-data" || argument == L"--purge-data") && request.verb == L"remove" && !removalSeen)
            {
                request.removal = argument == L"--purge-data" ? RemovalMode::PurgeData : RemovalMode::KeepData;
                removalSeen = true;
            }
            else if (request.verb == L"retry" && request.operation.empty() && setup::valid_id(argument))
                request.operation = argument;
            else
                throw failure("unexpected or duplicate maintenance argument", ERROR_BAD_ARGUMENTS);
        }
        if (request.verb == L"retry" && request.operation.empty())
            throw failure("retry requires an opaque operation ID", ERROR_BAD_ARGUMENTS);
        return request;
    }

    inline const wchar_t* RetryClass(DWORD nativeCode)
    {
        switch (nativeCode)
        {
        case ERROR_SUCCESS: return L"None";
        case ERROR_SUCCESS_REBOOT_REQUIRED:
        case ERROR_SUCCESS_REBOOT_INITIATED: return L"RetryAfterRestart";
        case ERROR_INSTALL_ALREADY_RUNNING:
        case ERROR_BUSY: return L"RetryNow";
        case ERROR_CANCELLED:
        case ERROR_INSTALL_USEREXIT:
        case ERROR_ELEVATION_REQUIRED:
        case ERROR_SERVICE_DOES_NOT_EXIST:
        case ERROR_SERVICE_NOT_ACTIVE:
        case ERROR_ACCESS_DENIED: return L"RetryAfterAuthorization";
        case WAIT_TIMEOUT:
        case ERROR_INSTALL_FAILURE:
        case ERROR_PROCESS_ABORTED:
        case ERROR_INVALID_STATE: return L"InspectUnknownOutcome";
        default: return L"RetryAfterInputFix";
        }
    }

    inline bool KnownTerminalPhase(std::string_view phase)
    {
        return phase == "committed" || phase == "rolled_back" || phase == "unchanged";
    }

    inline void RequireHealthyControlStatus(const PowerToys::ProtectedStorage::Value& status, std::string_view expectedVersion)
    {
        try
        {
            if (status.At("protocolMajor").Number() != 1 || status.At("protocolMinor").Number() != 0)
                throw failure("unsupported service health protocol", ERROR_REVISION_MISMATCH);
            const auto& phase = status.At("phase").Text();
            if (!status.At("healthy").Boolean() || !status.At("workerReady").Boolean() ||
                status.At("dataRecoveryRequired").Boolean() || status.At("hasUnresolvedTransaction").Boolean() ||
                status.At("maintenance").Boolean() || status.At("version").Text() != expectedVersion ||
                (phase != "none" && !KnownTerminalPhase(phase)))
                throw failure("owner instance is unhealthy or requires reconciliation", ERROR_INVALID_STATE);
        }
        catch (const PowerToys::ProtectedStorage::Error&)
        {
            throw failure("incomplete service health response; reconciliation required", ERROR_INVALID_STATE);
        }
    }

    template<typename Cleanup>
    bool TryTerminalCleanup(std::string_view phase, Cleanup&& cleanup)
    {
        if (!KnownTerminalPhase(phase))
            throw failure("cleanup cannot decide an unknown transaction outcome", ERROR_INVALID_STATE);
        try
        {
            cleanup();
            return true;
        }
        catch (const std::exception&)
        {
            return false;
        }
    }

    constexpr bool MayRetryMsi(DWORD code)
    {
        return code == ERROR_INSTALL_ALREADY_RUNNING || code == ERROR_BUSY;
    }

    inline std::wstring JsonEscape(std::wstring_view value)
    {
        std::wstring result;
        for (const auto character : value)
        {
            if (character == L'"' || character == L'\\') result += L'\\';
            if (character < 0x20)
                throw failure("control character in maintenance result", ERROR_INVALID_DATA);
            result += character;
        }
        return result;
    }

    inline std::wstring ResultJson(const std::wstring& operation, const std::wstring& state, DWORD code,
                                  bool cleanupPending = false, bool dataRetained = false,
                                  const std::wstring& productCode = {}, const std::wstring& installedVersion = {})
    {
        return L"{\"operationId\":\"" + JsonEscape(operation) + L"\",\"state\":\"" + JsonEscape(state) +
            L"\",\"nativeCode\":" + std::to_wstring(code) + L",\"retryKind\":\"" + RetryClass(code) +
            L"\",\"cleanupPending\":" + (cleanupPending ? L"true" : L"false") +
            L",\"dataRetained\":" + (dataRetained ? L"true" : L"false") +
            L",\"productCode\":\"" + JsonEscape(productCode) +
            L"\",\"installedVersion\":\"" + JsonEscape(installedVersion) + L"\"}\n";
    }

    inline void RequireMachineKeepData(const std::vector<std::wstring>& arguments)
    {
        if (arguments != std::vector<std::wstring>{ L"machine-remove", L"--keep-data" })
            throw failure("machine removal only supports the fixed keep-data command", ERROR_BAD_ARGUMENTS);
    }

    struct MachineOwnerResult
    {
        std::wstring owner;
        bool serviceStopped = false;
        bool serviceRemoved = false;
        bool codeRemoved = false;
        bool msiRegistrationRemoved = false;
        bool dataRetained = true;
        bool profileRemoved = false;
        std::vector<DWORD> nativeErrors;

        constexpr bool Complete() const
        {
            return serviceStopped && serviceRemoved && codeRemoved && msiRegistrationRemoved && profileRemoved && nativeErrors.empty();
        }

        std::wstring Json() const
        {
            const auto boolean = [](bool value) { return value ? L"true" : L"false"; };
            std::wstring pending;
            const auto append = [&](const wchar_t* value) {
                if (!pending.empty()) pending += L",";
                pending += L"\"" + std::wstring(value) + L"\"";
            };
            if (!serviceStopped) append(L"running-or-unverified-instance");
            if (!serviceRemoved) append(L"service-removal");
            if (!codeRemoved) append(L"code-removal");
            if (!msiRegistrationRemoved) append(L"owner-context-msi-reconciliation");
            if (!profileRemoved) append(L"virtual-account-profile-review");
            std::wstring errors;
            for (const auto error : nativeErrors)
            {
                if (!errors.empty()) errors += L",";
                errors += std::to_wstring(error);
            }
            return L"{\"ownerSid\":\"" + JsonEscape(owner) +
                L"\",\"serviceStopped\":" + boolean(serviceStopped) +
                L",\"serviceRemoved\":" + boolean(serviceRemoved) +
                L",\"codeRemoved\":" + boolean(codeRemoved) +
                L",\"msiRegistrationRemoved\":" + boolean(msiRegistrationRemoved) +
                L",\"dataRetained\":" + boolean(dataRetained) +
                L",\"profileRemoved\":" + boolean(profileRemoved) +
                L",\"pendingItems\":[" + pending + L"],\"nativeErrors\":[" + errors +
                L"],\"retryClass\":\"" + (Complete() ? L"None" : L"RetryAfterInputFix") + L"\"}";
        }
    };

    inline unsigned long long ReleaseNumber(const std::wstring& value)
    {
        std::wistringstream input(value);
        unsigned long long major = 0, minor = 0, build = 0;
        wchar_t first = 0, second = 0;
        if (!(input >> major >> first >> minor >> second >> build) || first != L'.' || second != L'.' ||
            !input.eof() || major > 255 || minor > 255 || build > 65535 ||
            value != std::to_wstring(major) + L"." + std::to_wstring(minor) + L"." + std::to_wstring(build))
            throw failure("invalid MSI release version", ERROR_INVALID_DATA);
        return (major << 24) | (minor << 16) | build;
    }

    enum class SyncPlan
    {
        Absent,
        VerifyCurrent,
        Upgrade,
    };

    inline SyncPlan PlanSync(size_t registrations, bool servicePresent, bool pending,
                             unsigned long long installedRelease, unsigned long long targetRelease,
                             bool sameProductCode)
    {
        if (pending || registrations > 1 || (registrations != 0) != servicePresent)
            throw failure("partial or unresolved owner instance must be reconciled", ERROR_INVALID_STATE);
        if (registrations == 0) return SyncPlan::Absent;
        if (installedRelease > targetRelease || (installedRelease == targetRelease && !sameProductCode))
            throw failure("installed carrier is newer or incompatible", ERROR_PRODUCT_VERSION);
        return installedRelease == targetRelease ? SyncPlan::VerifyCurrent : SyncPlan::Upgrade;
    }
}
