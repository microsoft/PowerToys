// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#pragma once
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#include <sddl.h>
#include <aclapi.h>
#include <msi.h>
#include <msiquery.h>
#include <userenv.h>
#include <string>
#include <vector>
#include <stdexcept>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <iostream>

namespace PowerToysProtectedStorage
{
    inline std::string ToUtf8(std::wstring_view text)
    {
        if (text.empty()) return {};
        const int length = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(), static_cast<int>(text.size()),
            nullptr, 0, nullptr, nullptr);
        if (!length) throw std::runtime_error("UTF-8 conversion");
        std::string result(static_cast<size_t>(length), '\0');
        if (!WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, text.data(), static_cast<int>(text.size()),
            result.data(), length, nullptr, nullptr)) throw std::runtime_error("UTF-8 conversion");
        return result;
    }
    struct failure : std::runtime_error
    {
        DWORD code;
        failure(const char* operation, DWORD value) : std::runtime_error(operation), code(value) {}
    };
    inline void check(bool ok, const char* operation)
    {
        if (!ok) throw failure(operation, GetLastError());
    }
    inline void result(DWORD code, const char* operation)
    {
        if (code != ERROR_SUCCESS) throw failure(operation, code);
    }
    inline void initialize_process_security()
    {
        check(SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32) != FALSE, "restrict helper DLL search");
    }
    struct handle
    {
        HANDLE value = nullptr;
        explicit handle(HANDLE v = nullptr) : value(v) {}
        ~handle() { if (value && value != INVALID_HANDLE_VALUE) CloseHandle(value); }
        handle(const handle&) = delete;
        handle& operator=(const handle&) = delete;
    };
    struct service
    {
        SC_HANDLE value = nullptr;
        explicit service(SC_HANDLE v = nullptr) : value(v) {}
        ~service() { if (value) CloseServiceHandle(value); }
        service(const service&) = delete;
        service& operator=(const service&) = delete;
    };
    inline std::wstring sid_string(PSID sid)
    {
        LPWSTR text = nullptr;
        check(ConvertSidToStringSidW(sid, &text) != FALSE, "ConvertSidToStringSid");
        std::wstring answer(text);
        LocalFree(text);
        return answer;
    }
    inline std::wstring token_sid(HANDLE token)
    {
        DWORD size = 0;
        GetTokenInformation(token, TokenUser, nullptr, 0, &size);
        std::vector<BYTE> data(size);
        check(GetTokenInformation(token, TokenUser, data.data(), size, &size) != FALSE, "TokenUser");
        return sid_string(reinterpret_cast<TOKEN_USER*>(data.data())->User.Sid);
    }
    inline std::wstring actor_sid()
    {
        handle token;
        if (!OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &token.value))
        {
            check(GetLastError() == ERROR_NO_TOKEN, "OpenThreadToken");
            check(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token.value) != FALSE, "OpenProcessToken");
        }
        return token_sid(token.value);
    }
    inline void require_system()
    {
        if (actor_sid() != L"S-1-5-18") throw failure("SYSTEM required", ERROR_ACCESS_DENIED);
    }
    inline std::wstring canonical_owner(const std::wstring& input)
    {
        PSID sid = nullptr;
        check(ConvertStringSidToSidW(input.c_str(), &sid) != FALSE, "owner SID");
        std::wstring canonical = sid_string(sid);
        const bool supported = IsValidSid(sid) && *GetSidSubAuthorityCount(sid) == 5 && canonical == input &&
            (input.rfind(L"S-1-5-21-", 0) == 0 || input.rfind(L"S-1-12-1-", 0) == 0);
        LocalFree(sid);
        if (!supported) throw failure("real business owner SID required", ERROR_INVALID_SID);
        return canonical;
    }
    inline std::wstring account_sid(const std::wstring& account)
    {
        DWORD sidSize = 0, domainSize = 0;
        SID_NAME_USE use{};
        LookupAccountNameW(nullptr, account.c_str(), nullptr, &sidSize, nullptr, &domainSize, &use);
        std::vector<BYTE> sid(sidSize);
        std::vector<wchar_t> domain(domainSize);
        check(LookupAccountNameW(nullptr, account.c_str(), sid.data(), &sidSize, domain.data(), &domainSize, &use) != FALSE, "LookupAccountName");
        return sid_string(sid.data());
    }
    inline void set_acl(const std::filesystem::path& path, const std::wstring& sddl)
    {
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        check(ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, &descriptor, nullptr) != FALSE, "SDDL");
        const BOOL ok = SetFileSecurityW(path.c_str(), OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION | PROTECTED_DACL_SECURITY_INFORMATION, descriptor);
        const DWORD error = GetLastError();
        LocalFree(descriptor);
        if (!ok) throw failure("SetFileSecurity", error);
    }
    inline void directory(const std::filesystem::path& path, const std::wstring& sddl)
    {
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        check(ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, &descriptor, nullptr) != FALSE, "directory SDDL");
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), descriptor, FALSE };
        const BOOL created = CreateDirectoryW(path.c_str(), &attributes);
        const DWORD error = GetLastError();
        LocalFree(descriptor);
        if (!created && error != ERROR_ALREADY_EXISTS) throw failure("CreateDirectory", error);
        const DWORD flags = GetFileAttributesW(path.c_str());
        if (flags == INVALID_FILE_ATTRIBUTES || (flags & FILE_ATTRIBUTE_REPARSE_POINT) || !(flags & FILE_ATTRIBUTE_DIRECTORY))
            throw failure("unsafe directory", ERROR_ACCESS_DENIED);
        if (!created)
        {
            PSID owner = nullptr;
            PSECURITY_DESCRIPTOR existing = nullptr;
            result(GetNamedSecurityInfoW(path.c_str(), SE_FILE_OBJECT, OWNER_SECURITY_INFORMATION, &owner, nullptr, nullptr, nullptr, &existing), "directory owner");
            const bool safe = sid_string(owner) == L"S-1-5-18";
            LocalFree(existing);
            if (!safe) throw failure("preexisting non-SYSTEM directory", ERROR_ACCESS_DENIED);
        }
    }
    inline std::wstring system_acl()
    {
        return L"O:SYG:SYD:P(A;OICI;FA;;;SY)(A;OICI;FRFX;;;BA)(A;OICI;FRFX;;;AU)";
    }
    inline std::wstring private_acl(const std::wstring& va, const std::wstring& owner = {})
    {
        return L"O:SYG:SYD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;" + va + L")" +
            (owner.empty() ? L"" : L"(A;OICI;FRFX;;;" + owner + L")");
    }
    inline void write_text(const std::filesystem::path& path, const std::wstring& text)
    {
        std::wofstream out(path, std::ios::trunc);
        out << text;
        if (!out) throw failure("write evidence", ERROR_WRITE_FAULT);
    }
    inline std::wstring msi_property(MSIHANDLE session, const wchar_t* name)
    {
        DWORD size = 0;
        wchar_t empty = 0;
        UINT code = MsiGetPropertyW(session, name, &empty, &size);
        if (code != ERROR_SUCCESS && code != ERROR_MORE_DATA) result(code, "MsiGetProperty size");
        std::vector<wchar_t> data(static_cast<size_t>(size) + 1);
        ++size;
        result(MsiGetPropertyW(session, name, data.data(), &size), "MsiGetProperty");
        return data.data();
    }
    inline void msi_log(MSIHANDLE session, const std::wstring& text)
    {
        MSIHANDLE record = MsiCreateRecord(1);
        MsiRecordSetStringW(record, 0, text.c_str());
        MsiProcessMessage(session, INSTALLMESSAGE_INFO, record);
        MsiCloseHandle(record);
    }
}
