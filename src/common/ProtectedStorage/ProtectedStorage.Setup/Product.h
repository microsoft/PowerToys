// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#pragma once
#include "Native.h"
#include <shlobj.h>
#include <cerrno>

namespace product
{
#if __has_include("ReleaseIdentity.h")
#include "ReleaseIdentity.h"
#else
#define PROTECTED_STORAGE_PRODUCT_CODE L""
#define PROTECTED_STORAGE_VERSION L"0.0.0"
#define PROTECTED_STORAGE_PE_VERSION L"0.0.0.0"
#endif
    inline constexpr wchar_t upgradeCode[] = L"{9A6A81D4-D538-4D8A-9B30-602C38B1BA23}";
    inline constexpr wchar_t code[] = PROTECTED_STORAGE_PRODUCT_CODE;
    inline constexpr wchar_t version[] = PROTECTED_STORAGE_VERSION;
    inline constexpr wchar_t liveSeedVersion[] = PROTECTED_STORAGE_PE_VERSION;
    inline constexpr wchar_t name[] = L"PowerToysProtectedStorage";
    inline constexpr wchar_t setupPrefix[] = L"PowerToysProtectedStorageSetup_";
    inline std::filesystem::path known_folder(REFKNOWNFOLDERID id)
    {
        PWSTR text = nullptr;
        const HRESULT result = SHGetKnownFolderPath(id, KF_FLAG_DEFAULT, nullptr, &text);
        if (FAILED(result)) throw PowerToysProtectedStorage::failure("SHGetKnownFolderPath", static_cast<DWORD>(result));
        std::filesystem::path path(text);
        CoTaskMemFree(text);
        return path;
    }
    inline std::filesystem::path code_root() { return known_folder(FOLDERID_ProgramFiles) / name; }
    inline std::filesystem::path state_root() { return known_folder(FOLDERID_ProgramData) / name; }
    inline std::filesystem::path stage_root() { return known_folder(FOLDERID_ProgramData); }
    inline std::filesystem::path cleanup_hint()
    {
        return known_folder(FOLDERID_LocalAppData) / name / L"MsiOperations" / (std::wstring(code) + L".cleanup-pending");
    }
    inline std::wstring service_name(const std::wstring& owner) { return L"PowerToysProtectedStorage_" + owner; }
    inline std::wstring service_account(const std::wstring& owner) { return L"NT SERVICE\\" + service_name(owner); }
    inline std::filesystem::path owner_code(const std::wstring& owner) { return code_root() / L"Owners" / owner / L"Code"; }
    inline std::wstring image_path(const std::wstring& owner)
    {
        return L"\"" + (owner_code(owner) / L"Bootstrap.exe").wstring() + L"\" --service \"" + owner + L"\"";
    }
    inline std::vector<BYTE> resource(WORD id)
    {
        HMODULE module = GetModuleHandleW(nullptr);
        HRSRC found = FindResourceW(module, MAKEINTRESOURCEW(id), RT_RCDATA);
        PowerToysProtectedStorage::check(found != nullptr, "FindResource");
        HGLOBAL loaded = LoadResource(module, found);
        PowerToysProtectedStorage::check(loaded != nullptr, "LoadResource");
        const auto* data = static_cast<const BYTE*>(LockResource(loaded));
        PowerToysProtectedStorage::check(data != nullptr, "LockResource");
        return { data, data + SizeofResource(module, found) };
    }
    inline bool has_resource(WORD id)
    {
        return FindResourceW(GetModuleHandleW(nullptr), MAKEINTRESOURCEW(id), RT_RCDATA) != nullptr;
    }
    inline void write_new(const std::filesystem::path& path, const std::vector<BYTE>& data, const std::wstring& security = {})
    {
        // LocalSystem's default TokenOwner can be Administrators. Keep the inherited DACL, but pin its files to SYSTEM.
        const auto effectiveSecurity = security.empty() && PowerToysProtectedStorage::actor_sid() == L"S-1-5-18" ? std::wstring(L"O:SY") : security;
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        if (!effectiveSecurity.empty())
            PowerToysProtectedStorage::check(ConvertStringSecurityDescriptorToSecurityDescriptorW(effectiveSecurity.c_str(), SDDL_REVISION_1, &descriptor, nullptr) != FALSE, "new file security");
        struct free_descriptor { PSECURITY_DESCRIPTOR value; ~free_descriptor() { if (value) LocalFree(value); } } free{ descriptor };
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), descriptor, FALSE };
        PowerToysProtectedStorage::handle file(CreateFileW(path.c_str(), GENERIC_WRITE, 0, descriptor ? &attributes : nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr));
        PowerToysProtectedStorage::check(file.value != INVALID_HANDLE_VALUE, "create immutable file");
        if (data.size() > MAXDWORD) throw PowerToysProtectedStorage::failure("resource too large", ERROR_FILE_TOO_LARGE);
        DWORD written = 0;
        PowerToysProtectedStorage::check(WriteFile(file.value, data.data(), static_cast<DWORD>(data.size()), &written, nullptr) && written == data.size(), "write immutable file");
        PowerToysProtectedStorage::check(FlushFileBuffers(file.value) != FALSE, "FlushFileBuffers");
    }
    inline void write_new_text(const std::filesystem::path& path, const std::wstring& text)
    {
        if (text.find_first_not_of(L"\r\n\t !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~") != std::wstring::npos)
            throw PowerToysProtectedStorage::failure("ASCII installer metadata required", ERROR_INVALID_DATA);
        std::vector<BYTE> bytes;
        bytes.reserve(text.size());
        for (wchar_t value : text) bytes.push_back(static_cast<BYTE>(value));
        write_new(path, bytes);
    }
    inline void privilege(const wchar_t* privilegeName)
    {
        PowerToysProtectedStorage::handle token;
        PowerToysProtectedStorage::check(OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &token.value) != FALSE, "privilege token");
        TOKEN_PRIVILEGES state{};
        state.PrivilegeCount = 1;
        PowerToysProtectedStorage::check(LookupPrivilegeValueW(nullptr, privilegeName, &state.Privileges[0].Luid) != FALSE, "LookupPrivilegeValue");
        state.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
        PowerToysProtectedStorage::check(AdjustTokenPrivileges(token.value, FALSE, &state, 0, nullptr, nullptr) != FALSE, "AdjustTokenPrivileges");
        PowerToysProtectedStorage::result(GetLastError(), "privilege assignment");
    }
    inline ULONGLONG process_birth(HANDLE process)
    {
        FILETIME created{}, exited{}, kernel{}, user{};
        PowerToysProtectedStorage::check(GetProcessTimes(process, &created, &exited, &kernel, &user) != FALSE, "GetProcessTimes");
        return (static_cast<ULONGLONG>(created.dwHighDateTime) << 32) | created.dwLowDateTime;
    }
    inline std::wstring module_path()
    {
        std::vector<wchar_t> text(32768);
        DWORD size = GetModuleFileNameW(nullptr, text.data(), static_cast<DWORD>(text.size()));
        PowerToysProtectedStorage::check(size > 0 && size < text.size(), "GetModuleFileName");
        return std::wstring(text.data(), size);
    }
    inline ULONGLONG unsigned_number(const std::wstring& text)
    {
        if (text.empty() || text.find_first_not_of(L"0123456789") != std::wstring::npos)
            throw PowerToysProtectedStorage::failure("unsigned numeric argument required", ERROR_BAD_ARGUMENTS);
        errno = 0;
        wchar_t* end = nullptr;
        const ULONGLONG value = wcstoull(text.c_str(), &end, 10);
        if (errno == ERANGE || !end || *end || !value) throw PowerToysProtectedStorage::failure("numeric argument range", ERROR_BAD_ARGUMENTS);
        return value;
    }
    inline void assert_system_directory(const std::filesystem::path& path)
    {
        const DWORD attributes = GetFileAttributesW(path.c_str());
        if (attributes == INVALID_FILE_ATTRIBUTES || !(attributes & FILE_ATTRIBUTE_DIRECTORY) || (attributes & FILE_ATTRIBUTE_REPARSE_POINT))
            throw PowerToysProtectedStorage::failure("unsafe SYSTEM directory", ERROR_ACCESS_DENIED);
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        PSID owner = nullptr;
        PACL dacl = nullptr;
        PowerToysProtectedStorage::result(GetNamedSecurityInfoW(path.c_str(), SE_FILE_OBJECT, OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION,
            &owner, nullptr, &dacl, nullptr, &descriptor), "SYSTEM directory security");
        struct release { PSECURITY_DESCRIPTOR value; ~release() { LocalFree(value); } } free{ descriptor };
        SECURITY_DESCRIPTOR_CONTROL control{};
        DWORD revision = 0;
        PowerToysProtectedStorage::check(GetSecurityDescriptorControl(descriptor, &control, &revision) != FALSE, "SYSTEM directory control");
        if (PowerToysProtectedStorage::sid_string(owner) != L"S-1-5-18" || !dacl || !(control & SE_DACL_PROTECTED))
            throw PowerToysProtectedStorage::failure("SYSTEM directory ownership/DACL", ERROR_ACCESS_DENIED);
        constexpr DWORD mutation = FILE_WRITE_DATA | FILE_APPEND_DATA | FILE_WRITE_EA | FILE_WRITE_ATTRIBUTES |
            FILE_DELETE_CHILD | DELETE | WRITE_DAC | WRITE_OWNER | GENERIC_WRITE | GENERIC_ALL;
        for (DWORD index = 0; index < dacl->AceCount; ++index)
        {
            LPVOID raw = nullptr;
            PowerToysProtectedStorage::check(GetAce(dacl, index, &raw) != FALSE, "SYSTEM directory ACE");
            auto* ace = static_cast<ACCESS_ALLOWED_ACE*>(raw);
            if (ace->Header.AceType != ACCESS_ALLOWED_ACE_TYPE ||
                ((ace->Mask & mutation) && PowerToysProtectedStorage::sid_string(&ace->SidStart) != L"S-1-5-18"))
                throw PowerToysProtectedStorage::failure("non-SYSTEM stage writer", ERROR_ACCESS_DENIED);
        }
    }
}
