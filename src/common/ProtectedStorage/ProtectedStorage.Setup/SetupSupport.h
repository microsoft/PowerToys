// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#pragma once
#include "Product.h"
#include "PackageTrust.h"
#include <objbase.h>

namespace setup
{
    // The authorization rendezvous belongs to the live, ordinary-token Setup,
    // never to the alternate administrator that answers an OTS prompt.
    // opens Global\<setupPrefix><pid>_<birth>_<nonce>_{ready,succeeded,failed}.
    // The original normal-token Setup creates all three manual-reset events, initially
    // unsignaled, owned by its real-user SID, with only SYSTEM/BA/that SID granted access.
    // ready permits native owner MSI execution; succeeded/failed reports its known outcome.
    // No token, target SID or arbitrary MSI path is accepted from the elevated credential user.
    // A timeout/process exit without a signal is unknown, not a license for destructive rollback.
    struct msi_handle
    {
        MSIHANDLE value = 0;
        ~msi_handle() { if (value) MsiCloseHandle(value); }
        msi_handle() = default;
        msi_handle(const msi_handle&) = delete;
        msi_handle& operator=(const msi_handle&) = delete;
    };
    inline std::wstring database_property(MSIHANDLE database, const wchar_t* name)
    {
        msi_handle query, parameter, record;
        PowerToysProtectedStorage::result(MsiDatabaseOpenViewW(database, L"SELECT `Value` FROM `Property` WHERE `Property` = ?", &query.value), "embedded MSI property query");
        parameter.value = MsiCreateRecord(1);
        PowerToysProtectedStorage::check(parameter.value != 0, "embedded MSI property parameter");
        PowerToysProtectedStorage::result(MsiRecordSetStringW(parameter.value, 1, name), "embedded MSI property name");
        PowerToysProtectedStorage::result(MsiViewExecute(query.value, parameter.value), "embedded MSI property execute");
        const UINT fetched = MsiViewFetch(query.value, &record.value);
        if (fetched == ERROR_NO_MORE_ITEMS) return L"";
        PowerToysProtectedStorage::result(fetched, "embedded MSI property fetch");
        DWORD size = 0;
        wchar_t empty = 0;
        const UINT sized = MsiRecordGetStringW(record.value, 1, &empty, &size);
        if (sized != ERROR_MORE_DATA && sized != ERROR_SUCCESS) PowerToysProtectedStorage::result(sized, "embedded MSI property size");
        std::vector<wchar_t> value(static_cast<size_t>(size) + 1);
        ++size;
        PowerToysProtectedStorage::result(MsiRecordGetStringW(record.value, 1, value.data(), &size), "embedded MSI property value");
        return value.data();
    }
    inline void verify_package(MSIHANDLE database)
    {
        if (database_property(database, L"ProductCode") != product::code ||
            database_property(database, L"ProductVersion") != product::version ||
            database_property(database, L"UpgradeCode") != product::upgradeCode ||
            !database_property(database, L"ALLUSERS").empty())
            throw PowerToysProtectedStorage::failure("embedded MSI is not this exact-release ordinary per-user carrier", ERROR_INSTALL_PACKAGE_INVALID);
    }
    inline std::vector<BYTE> binary_stream(MSIHANDLE database, const wchar_t* name)
    {
        msi_handle view, parameter, record;
        PowerToysProtectedStorage::result(MsiDatabaseOpenViewW(database,
            L"SELECT `Data` FROM `Binary` WHERE `Name` = ?", &view.value), "open fixed Binary stream");
        parameter.value = MsiCreateRecord(1);
        PowerToysProtectedStorage::check(parameter.value != 0, "Binary query parameter");
        PowerToysProtectedStorage::result(MsiRecordSetStringW(parameter.value, 1, name), "fixed Binary name");
        PowerToysProtectedStorage::result(MsiViewExecute(view.value, parameter.value), "execute fixed Binary query");
        PowerToysProtectedStorage::result(MsiViewFetch(view.value, &record.value), "fetch fixed Binary stream");
        std::vector<BYTE> bytes;
        for (;;)
        {
            BYTE buffer[65536]{};
            DWORD count = sizeof(buffer);
            PowerToysProtectedStorage::result(MsiRecordReadStream(record.value, 1, reinterpret_cast<char*>(buffer), &count),
                "read fixed Binary stream");
            if (!count) break;
            if (bytes.size() + count > 64ull * 1024 * 1024)
                throw PowerToysProtectedStorage::failure("fixed Binary exceeds payload bound", ERROR_FILE_TOO_LARGE);
            bytes.insert(bytes.end(), buffer, buffer + count);
        }
        if (bytes.empty()) throw PowerToysProtectedStorage::failure("empty fixed Binary", ERROR_INVALID_DATA);
        return bytes;
    }
    inline std::wstring product_information(const std::wstring& productCode, const std::wstring& owner, const wchar_t* property)
    {
        DWORD count = 0;
        wchar_t empty = 0;
        UINT code = MsiGetProductInfoExW(productCode.c_str(), owner.c_str(), MSIINSTALLCONTEXT_USERUNMANAGED, property, &empty, &count);
        if (code != ERROR_MORE_DATA && code != ERROR_SUCCESS)
            PowerToysProtectedStorage::result(code, "owner registration property size");
        std::vector<wchar_t> value(static_cast<size_t>(count) + 1);
        ++count;
        PowerToysProtectedStorage::result(MsiGetProductInfoExW(productCode.c_str(), owner.c_str(), MSIINSTALLCONTEXT_USERUNMANAGED,
            property, value.data(), &count), "owner registration property");
        return value.data();
    }
    inline void verify_owner_registration(const std::wstring& owner, bool installed)
    {
        if (installed)
        {
            if (product_information(product::code, owner, INSTALLPROPERTY_PRODUCTSTATE) != std::to_wstring(INSTALLSTATE_DEFAULT) ||
                product_information(product::code, owner, INSTALLPROPERTY_VERSIONSTRING) != product::version)
                throw PowerToysProtectedStorage::failure("actual owner registration does not match committed release", ERROR_INVALID_STATE);
            return;
        }
        for (DWORD index = 0;; ++index)
        {
            wchar_t code[39]{}, sid[256]{};
            DWORD length = static_cast<DWORD>(std::size(sid));
            MSIINSTALLCONTEXT context{};
            const auto result = MsiEnumProductsExW(nullptr, owner.c_str(), MSIINSTALLCONTEXT_USERUNMANAGED, index,
                code, &context, sid, &length);
            if (result == ERROR_NO_MORE_ITEMS) break;
            PowerToysProtectedStorage::result(result, "inspect actual owner removal registration");
            if (context != MSIINSTALLCONTEXT_USERUNMANAGED || owner != sid)
                throw PowerToysProtectedStorage::failure("registration context changed", ERROR_INVALID_OWNER);
            if (product_information(code, owner, INSTALLPROPERTY_INSTALLEDPRODUCTNAME) != L"PowerToys Protected Storage")
                continue;
            const auto localPackage = product_information(code, owner, INSTALLPROPERTY_LOCALPACKAGE);
            wchar_t windows[MAX_PATH]{};
            PowerToysProtectedStorage::check(GetWindowsDirectoryW(windows, MAX_PATH) != 0, "Windows Installer cache root");
            const std::filesystem::path cached(localPackage);
            const auto cacheRoot = std::filesystem::path(windows) / L"Installer";
            if (localPackage.empty() || !cached.is_absolute() || cached != cached.lexically_normal() ||
                _wcsicmp(cached.parent_path().c_str(), cacheRoot.c_str()) || _wcsicmp(cached.extension().c_str(), L".msi"))
                throw PowerToysProtectedStorage::failure("unexpected MSI-owned cache metadata path", ERROR_BAD_CONFIGURATION);
            PowerToysProtectedStorage::handle held(CreateFileW(cached.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr,
                OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            PowerToysProtectedStorage::check(held.value != INVALID_HANDLE_VALUE, "hold MSI-owned cached metadata");
            BY_HANDLE_FILE_INFORMATION information{};
            PowerToysProtectedStorage::check(GetFileInformationByHandle(held.value, &information) != FALSE, "MSI cache file identity");
            if ((information.dwFileAttributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT)) || information.nNumberOfLinks != 1)
                throw PowerToysProtectedStorage::failure("unsafe MSI-owned cache metadata object", ERROR_BAD_CONFIGURATION);
            msi_handle database;
            PowerToysProtectedStorage::result(MsiOpenDatabaseW(localPackage.c_str(), MSIDBOPEN_READONLY, &database.value),
                "read MSI-owned cached registration metadata");
            if (database_property(database.value, L"UpgradeCode") == product::upgradeCode)
                throw PowerToysProtectedStorage::failure("carrier registration still exists; removal outcome not committed", ERROR_INVALID_STATE);
        }
    }
    constexpr bool success(UINT value)
    {
        return value == ERROR_SUCCESS || value == ERROR_SUCCESS_REBOOT_REQUIRED || value == ERROR_SUCCESS_REBOOT_INITIATED;
    }
    inline bool exists(const std::filesystem::path& path)
    {
        if (GetFileAttributesW(path.c_str()) != INVALID_FILE_ATTRIBUTES) return true;
        const DWORD error = GetLastError();
        if (error != ERROR_FILE_NOT_FOUND && error != ERROR_PATH_NOT_FOUND) throw PowerToysProtectedStorage::failure("inspect setup path", error);
        return false;
    }
    inline bool valid_id(const std::wstring& id)
    {
        return id.size() == 32 && id.find_first_not_of(L"0123456789ABCDEFabcdef") == std::wstring::npos;
    }
    inline std::wstring unique_id()
    {
        GUID guid{};
        const HRESULT result = CoCreateGuid(&guid);
        if (FAILED(result)) throw PowerToysProtectedStorage::failure("setup GUID", static_cast<DWORD>(result));
        wchar_t text[40]{};
        if (!StringFromGUID2(guid, text, 40)) throw PowerToysProtectedStorage::failure("format setup GUID", ERROR_INVALID_DATA);
        std::wstring id;
        for (wchar_t value : std::wstring(text))
            if ((value >= L'0' && value <= L'9') || (value >= L'A' && value <= L'F') || (value >= L'a' && value <= L'f')) id.push_back(value);
        if (!valid_id(id)) throw PowerToysProtectedStorage::failure("setup GUID format", ERROR_INVALID_DATA);
        return id;
    }
    inline std::wstring event_name(DWORD pid, ULONGLONG birth, const std::wstring& nonce, const wchar_t* signal)
    {
        if (!valid_id(nonce)) throw PowerToysProtectedStorage::failure("rendezvous nonce", ERROR_BAD_ARGUMENTS);
        return L"Global\\" + std::wstring(product::setupPrefix) + std::to_wstring(pid) + L"_" +
            std::to_wstring(birth) + L"_" + nonce + L"_" + signal;
    }
    inline void require_normal(HANDLE token)
    {
        TOKEN_ELEVATION elevation{};
        DWORD returned = 0;
        PowerToysProtectedStorage::check(GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &returned) != FALSE, "original owner elevation");
        DWORD size = 0;
        GetTokenInformation(token, TokenIntegrityLevel, nullptr, 0, &size);
        std::vector<BYTE> data(size);
        PowerToysProtectedStorage::check(GetTokenInformation(token, TokenIntegrityLevel, data.data(), size, &size) != FALSE, "original owner integrity");
        PSID integrity = reinterpret_cast<TOKEN_MANDATORY_LABEL*>(data.data())->Label.Sid;
        const DWORD level = *GetSidSubAuthority(integrity, *GetSidSubAuthorityCount(integrity) - 1);
        if (elevation.TokenIsElevated || level >= SECURITY_MANDATORY_HIGH_RID)
            throw PowerToysProtectedStorage::failure("run Setup from the original owner's normal desktop token, not an elevated shell", ERROR_ELEVATION_REQUIRED);
    }
    inline void require_profile(const std::wstring& owner)
    {
        HKEY hive = nullptr;
        PowerToysProtectedStorage::result(RegOpenKeyExW(HKEY_USERS, owner.c_str(), 0, KEY_READ, &hive), "original owner profile must be loaded");
        RegCloseKey(hive);
    }
    inline std::wstring requester_owner(DWORD pid, ULONGLONG birth, PowerToysProtectedStorage::handle& process, bool sameImage)
    {
        process.value = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, pid);
        PowerToysProtectedStorage::check(process.value != nullptr, "open original requester");
        if (product::process_birth(process.value) != birth || WaitForSingleObject(process.value, 0) != WAIT_TIMEOUT)
            throw PowerToysProtectedStorage::failure("original requester exited or PID was reused", ERROR_INVALID_OWNER);
        if (sameImage)
        {
            std::vector<wchar_t> image(32768);
            DWORD size = static_cast<DWORD>(image.size());
            PowerToysProtectedStorage::check(QueryFullProcessImageNameW(process.value, 0, image.data(), &size) != FALSE, "original requester image");
            PowerToysProtectedStorage::Maintenance::VerifySameImage(process.value, std::filesystem::path(std::wstring(image.data(), size)));
        }
        PowerToysProtectedStorage::handle token;
        PowerToysProtectedStorage::check(OpenProcessToken(process.value, TOKEN_QUERY, &token.value) != FALSE, "original owner token");
        require_normal(token.value);
        const auto owner = PowerToysProtectedStorage::canonical_owner(PowerToysProtectedStorage::token_sid(token.value));
        require_profile(owner);
        return owner;
    }
    inline void new_directory(const std::filesystem::path& path, const std::wstring& sddl)
    {
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        PowerToysProtectedStorage::check(ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, &descriptor, nullptr) != FALSE, "new setup directory security");
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), descriptor, FALSE };
        const BOOL created = CreateDirectoryW(path.c_str(), &attributes);
        const DWORD error = GetLastError();
        LocalFree(descriptor);
        if (!created) throw PowerToysProtectedStorage::failure("create unique setup directory (no reuse)", error);
    }
    inline DWORD fixed_machine_child(const std::filesystem::path& executable, const wchar_t* fixedVerb)
    {
        PowerToysProtectedStorage::require_system();
        product::assert_system_directory(executable.parent_path());
        PowerToysProtectedStorage::Maintenance::VerifySignedFile(executable);
        PowerToysProtectedStorage::handle file(CreateFileW(executable.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr,
            OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        PowerToysProtectedStorage::check(file.value != INVALID_HANDLE_VALUE, "hold protected machine helper");
        std::wstring command = L"\"" + executable.wstring() + L"\" " + fixedVerb;
        STARTUPINFOW startup{ sizeof(startup) };
        startup.dwFlags = STARTF_USESTDHANDLES;
        startup.hStdInput = GetStdHandle(STD_INPUT_HANDLE);
        startup.hStdOutput = GetStdHandle(STD_OUTPUT_HANDLE);
        startup.hStdError = GetStdHandle(STD_ERROR_HANDLE);
        PROCESS_INFORMATION child{};
        PowerToysProtectedStorage::check(CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, TRUE,
            CREATE_NO_WINDOW, nullptr, executable.parent_path().c_str(), &startup, &child) != FALSE, "run fixed machine cleanup");
        PowerToysProtectedStorage::handle process(child.hProcess), thread(child.hThread);
        if (WaitForSingleObject(process.value, 300000) != WAIT_OBJECT_0)
            throw PowerToysProtectedStorage::failure("machine cleanup outcome unknown; protected stage retained", WAIT_TIMEOUT);
        DWORD code = ERROR_INSTALL_FAILURE;
        PowerToysProtectedStorage::check(GetExitCodeProcess(process.value, &code) != FALSE, "machine cleanup result");
        return code;
    }
}
