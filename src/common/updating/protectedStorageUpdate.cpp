// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "protectedStorageUpdate.h"

#include <sddl.h>
#include <wil/resource.h>
#include <fstream>
#include <system_error>
#include <vector>

#include <common/SettingsAPI/settings_helpers.h>
#include <common/logger/logger.h>
#include <common/utils/json.h>
#include <common/utils/owner_process.h>

#include "installer.h"

namespace
{
    std::wstring CurrentOwnerSid(HANDLE token)
    {
        DWORD size = 0;
        GetTokenInformation(token, TokenUser, nullptr, 0, &size);
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            throw std::system_error(GetLastError(), std::system_category(), "Read protected-storage update owner");
        }
        std::vector<BYTE> data(size);
        if (!GetTokenInformation(token, TokenUser, data.data(), size, &size))
        {
            throw std::system_error(GetLastError(), std::system_category(), "Read protected-storage update token");
        }
        wil::unique_hlocal_string sid;
        if (!ConvertSidToStringSidW(reinterpret_cast<TOKEN_USER*>(data.data())->User.Sid, sid.put()))
        {
            throw std::system_error(GetLastError(), std::system_category(), "Format protected-storage owner SID");
        }
        return sid.get();
    }

    void RecordResult(const std::wstring& owner, updating::ProtectedStorageSyncResult result)
    {
        const auto directory = std::filesystem::path(PTSettingsHelper::get_root_save_folder_location()) / L"ProtectedStorage";
        std::filesystem::create_directories(directory);
        json::JsonObject record;
        record.SetNamedValue(L"ownerSid", json::value(owner));
        record.SetNamedValue(L"state", json::value(updating::ProtectedStorageSyncStateName(result.state)));
        record.SetNamedValue(L"nativeCode", json::value(static_cast<int64_t>(result.nativeCode)));
        // This owner-writable record is diagnostic state, never a command or
        // authorization source. Setup always rechecks the actual instance.
        std::ofstream output;
        output.exceptions(std::ios::failbit | std::ios::badbit);
        output.open(directory / L"update-state.json", std::ios::binary | std::ios::trunc);
        output << winrt::to_string(record.Stringify());
        output.close();
    }

    void RequireNoOrphanService(const std::wstring& owner)
    {
        SC_HANDLE manager = OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT);
        if (!manager)
        {
            throw std::system_error(GetLastError(), std::system_category(), "Inspect retained owner service");
        }
        const auto closeManager = wil::scope_exit([&] { CloseServiceHandle(manager); });
        SC_HANDLE service = OpenServiceW(manager, (L"PowerToysProtectedStorage_" + owner).c_str(), SERVICE_QUERY_STATUS);
        const DWORD error = GetLastError();
        if (service)
        {
            CloseServiceHandle(service);
            throw std::system_error(ERROR_INVALID_STATE, std::system_category(), "Carrier registration is absent but the owner service remains");
        }
        if (error != ERROR_SERVICE_DOES_NOT_EXIST)
        {
            throw std::system_error(error, std::system_category(), "Owner service absence could not be verified");
        }
    }
}

namespace updating
{
    static ProtectedStorageSyncResult RunOwnerMaintenance(const std::filesystem::path& installDirectory, bool remove)
    {
        ProtectedStorageSyncResult result{ ProtectedStorageSyncState::RetryRequired, ERROR_GEN_FAILURE };
        std::wstring owner;
        bool childStarted = false;
        bool mayRecordOwnerState = false;
        try
        {
            wil::unique_handle token;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, token.put()))
            {
                throw std::system_error(GetLastError(), std::system_category(), "Open protected-storage update token");
            }
            TOKEN_ELEVATION elevation{};
            DWORD size = 0;
            if (!GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size))
            {
                throw std::system_error(GetLastError(), std::system_category(), "Read protected-storage update elevation");
            }
            owner = CurrentOwnerSid(token.get());
            mayRecordOwnerState = !elevation.TokenIsElevated;
            if (!IsProtectedStorageOwnerSid(owner))
            {
                Logger::warn("Protected storage synchronization deferred: a bound non-elevated owner context is required.");
                return { ProtectedStorageSyncState::DeferredOwnerContext, ERROR_ELEVATION_REQUIRED };
            }
            if (remove)
            {
                if (elevation.TokenIsElevated)
                {
                    Logger::warn("Owner carrier uninstall deferred: the bundle must retain its original ordinary owner.");
                    return { ProtectedStorageSyncState::DeferredOwnerContext, ERROR_ELEVATION_REQUIRED };
                }
                HKEY key = nullptr;
                const auto found = RegOpenKeyExW(HKEY_CURRENT_USER, L"Software\\Microsoft\\PowerToys\\ProtectedStorage", 0, KEY_QUERY_VALUE, &key);
                if (found == ERROR_FILE_NOT_FOUND)
                {
                    RequireNoOrphanService(owner);
                    return { ProtectedStorageSyncState::Completed, ERROR_SUCCESS };
                }
                if (found != ERROR_SUCCESS)
                {
                    throw std::system_error(found, std::system_category(), "Inspect owner carrier uninstall registration");
                }
                const auto closeKey = wil::scope_exit([&] { RegCloseKey(key); });
                DWORD markerSize = 0;
                const auto version = RegQueryValueExW(key, L"CarrierVersion", nullptr, nullptr, nullptr, &markerSize);
                if (version == ERROR_FILE_NOT_FOUND)
                {
                    RequireNoOrphanService(owner);
                    return { ProtectedStorageSyncState::Completed, ERROR_SUCCESS };
                }
                if (version != ERROR_SUCCESS)
                {
                    throw std::system_error(version, std::system_category(), "Read owner carrier uninstall marker");
                }
            }
            if (!installDirectory.is_absolute())
            {
                throw std::system_error(ERROR_BAD_PATHNAME, std::system_category(), "Protected-storage install directory must be absolute");
            }
            const auto image = installDirectory / L"PowerToys.ProtectedStorageSetup.exe";
            wil::unique_hfile heldImage{ CreateFileW(image.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr) };
            if (!heldImage)
            {
                throw std::system_error(GetLastError(), std::system_category(), "Open protected-storage maintenance image");
            }
            if (!verify_protected_storage_setup_trust(image.wstring(), heldImage.get()))
            {
                throw std::system_error(ERROR_INVALID_IMAGE_HASH, std::system_category(), "Protected-storage maintenance image is not trusted");
            }
            wil::unique_handle process;
            const auto launchError = owner_process::Start(image, remove ? L"remove --keep-data --json" : L"sync", installDirectory, process);
            if (launchError == ERROR_INVALID_OWNER || launchError == ERROR_NO_SUCH_LOGON_SESSION || launchError == ERROR_PRIVILEGE_NOT_HELD)
            {
                Logger::warn("Protected storage synchronization deferred: no matching ordinary owner launch context ({})", launchError);
                return { ProtectedStorageSyncState::DeferredOwnerContext, launchError };
            }
            if (launchError)
            {
                throw std::system_error(launchError, std::system_category(), "Start protected-storage owner synchronization");
            }
            childStarted = true;
            const auto wait = WaitForSingleObject(process.get(), 300000);
            if (wait == WAIT_TIMEOUT)
            {
                // Never kill an MSI client merely because the outer update UI
                // timed out. Its next retry must inspect the persisted outcome.
                result = { ProtectedStorageSyncState::OutcomeUnknown, WAIT_TIMEOUT };
            }
            else if (wait != WAIT_OBJECT_0)
            {
                throw std::system_error(GetLastError(), std::system_category(), "Wait for protected-storage synchronization");
            }
            else
            {
                DWORD code = 0;
                if (!GetExitCodeProcess(process.get(), &code))
                {
                    throw std::system_error(GetLastError(), std::system_category(), "Read protected-storage synchronization result");
                }
                result = ClassifyProtectedStorageSyncExit(code);
            }
        }
        catch (const std::system_error& error)
        {
            result = { childStarted ? ProtectedStorageSyncState::OutcomeUnknown : ProtectedStorageSyncState::RetryRequired,
                       static_cast<DWORD>(error.code().value()) };
            Logger::error("Protected storage synchronization failed: {}", error.what());
        }
        catch (const winrt::hresult_error& error)
        {
            result = { childStarted ? ProtectedStorageSyncState::OutcomeUnknown : ProtectedStorageSyncState::RetryRequired,
                       static_cast<DWORD>(error.code().value) };
            Logger::error(L"Protected storage synchronization failed: {}", error.message());
        }
        catch (const std::exception& error)
        {
            result = { childStarted ? ProtectedStorageSyncState::OutcomeUnknown : ProtectedStorageSyncState::RetryRequired, ERROR_GEN_FAILURE };
            Logger::error("Protected storage synchronization failed unexpectedly: {}", error.what());
        }
        if (!owner.empty() && mayRecordOwnerState)
        {
            try
            {
                RecordResult(owner, result);
            }
            catch (const std::exception& error)
            {
                Logger::error("Could not persist protected-storage update status: {}", error.what());
            }
            catch (const winrt::hresult_error& error)
            {
                Logger::error(L"Could not persist protected-storage update status: {}", error.message());
            }
        }
        Logger::info(L"Protected storage owner synchronization: state={}, nativeCode={}", ProtectedStorageSyncStateName(result.state), result.nativeCode);
        return result;
    }

    ProtectedStorageSyncResult SynchronizeProtectedStorageForOwner(const std::filesystem::path& installDirectory)
    {
        return RunOwnerMaintenance(installDirectory, false);
    }

    ProtectedStorageSyncResult RemoveProtectedStorageForOwner(const std::filesystem::path& setupDirectory)
    {
        return RunOwnerMaintenance(setupDirectory, true);
    }
}
