#include "pch.h"

#include "NtdllExtensions.h"

#define STATUS_INFO_LENGTH_MISMATCH ((LONG)0xC0000004)

// Calls NtQuerySystemInformation and returns a buffer containing the result.

namespace
{
    std::wstring_view unicode_to_view(UNICODE_STRING unicode_str)
    {
        if (!unicode_str.Buffer || unicode_str.Length == 0)
        {
            return {};
        }

        return std::wstring_view(unicode_str.Buffer, unicode_str.Length / sizeof(WCHAR));
    }

    std::wstring unicode_to_str(UNICODE_STRING unicode_str)
    {
        if (!unicode_str.Buffer || unicode_str.Length == 0)
        {
            return {};
        }

        return std::wstring(unicode_str.Buffer, unicode_str.Length / sizeof(WCHAR));
    }

    // Implementation adapted from src/common/utils
    inline std::wstring get_module_name(HANDLE process, HMODULE mod)
    {
        wchar_t buffer[MAX_PATH + 1];
        DWORD actual_length = GetModuleFileNameExW(process, mod, buffer, MAX_PATH + 1);
        if (GetLastError() == ERROR_INSUFFICIENT_BUFFER)
        {
            const DWORD long_path_length = 0xFFFF; // should be always enough
            std::wstring long_filename(long_path_length, L'\0');
            actual_length = GetModuleFileNameW(mod, long_filename.data(), long_path_length);
            long_filename.resize(std::wcslen(long_filename.data()));
            long_filename.shrink_to_fit();
            return long_filename;
        }

        return { buffer, static_cast<UINT>(lstrlenW(buffer)) };
    }

    constexpr size_t DefaultModulesResultSize = 512;

    std::vector<std::wstring> process_modules(DWORD pid)
    {
        HANDLE process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, pid);
        if (!process)
        {
            return {};
        }

        std::vector<std::wstring> result;

        bool completed = false;
        std::vector<HMODULE> modules(DefaultModulesResultSize);
        while (!completed)
        {
            DWORD needed;
            auto status = EnumProcessModules(process, modules.data(), static_cast<DWORD>(modules.size() * sizeof(HMODULE)), &needed);
        
            if (!status)
            {
                // Give up
                return {};
            }

            if (needed > modules.size() * sizeof(HMODULE))
            {
                // Array is too small
                modules.resize(needed / sizeof(HMODULE));
                continue;
            }

            // Okay
            modules.resize(needed / sizeof(HMODULE));
        
            for (auto mod : modules)
            {
                result.push_back(get_module_name(process, mod));
            }

            completed = true;
        }

        CloseHandle(process);
        return result;
    }
}

NtdllExtensions::MemoryLoopResult NtdllExtensions::NtQuerySystemInformationMemoryLoop(ULONG SystemInformationClass)
{
    MemoryLoopResult result;
    result.memory.resize(DefaultResultBufferSize);

    while (result.memory.size() <= MaxResultBufferSize)
    {
        ULONG result_len;
        result.status = NtQuerySystemInformation(SystemInformationClass, result.memory.data(), static_cast<ULONG>(result.memory.size()), &result_len);

        if (result.status == STATUS_INFO_LENGTH_MISMATCH)
        {
            result.memory.resize(result.memory.size() * 2);
            continue;
        }

        if (NT_ERROR(result.status))
        {
            result.memory.clear();
        }

        return result;
    }

    result.status = STATUS_INFO_LENGTH_MISMATCH;
    result.memory.clear();
    return result;
}

std::wstring NtdllExtensions::file_handle_to_kernel_name(HANDLE file_handle, std::vector<BYTE>& buffer)
{
    if (GetFileType(file_handle) != FILE_TYPE_DISK)
    {
        return L"";
    }

    ULONG return_length;
    auto status = NtQueryObject(file_handle, ObjectNameInformation, buffer.data(), static_cast<ULONG>(buffer.size()), &return_length);
    if (NT_SUCCESS(status))
    {
        auto object_name_info = reinterpret_cast<UNICODE_STRING*>(buffer.data());
        return unicode_to_str(*object_name_info);
    }

    return L"";
}

std::wstring NtdllExtensions::file_handle_to_kernel_name(HANDLE file_handle)
{
    std::vector<BYTE> buffer(DefaultResultBufferSize);
    return file_handle_to_kernel_name(file_handle, buffer);
}

std::wstring NtdllExtensions::path_to_kernel_name(LPCWSTR path)
{
    HANDLE file_handle = CreateFileW(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, NULL);
    if (file_handle == INVALID_HANDLE_VALUE)
    {
        return {};
    }

    auto kernel_name = file_handle_to_kernel_name(file_handle);
    CloseHandle(file_handle);
    return kernel_name;
}

std::vector<NtdllExtensions::HandleInfo> NtdllExtensions::handles() noexcept
{
    auto get_info_result = NtQuerySystemInformationMemoryLoop(SystemExtendedHandleInformation);
    if (NT_ERROR(get_info_result.status))
    {
        return {};
    }

    auto info_ptr = reinterpret_cast<SYSTEM_HANDLE_INFORMATION_EX*>(get_info_result.memory.data());

    std::map<ULONG_PTR, HANDLE> pid_to_handle;
    std::vector<HandleInfo> result;
    std::vector<BYTE> object_info_buffer(DefaultResultBufferSize);

    const ULONG_PTR handle_count = info_ptr->NumberOfHandles;
    for (ULONG_PTR i = 0; i < handle_count; i++)
    {
        const auto* handle_info = info_ptr->Handles + i;
        const auto pid = handle_info->UniqueProcessId;

        HANDLE process_handle = NULL;
        if (auto iter = pid_to_handle.find(pid); iter != pid_to_handle.end())
        {
            process_handle = iter->second;
        }
        else
        {
            process_handle = OpenProcess(PROCESS_DUP_HANDLE, FALSE, static_cast<DWORD>(pid));
            if (!process_handle)
            {
                continue;
            }

            pid_to_handle[pid] = process_handle;
        }

        HANDLE handle_copy = NULL;
        if (!DuplicateHandle(
                process_handle,
                reinterpret_cast<HANDLE>(handle_info->HandleValue),
                GetCurrentProcess(),
                &handle_copy,
                0,
                FALSE,
                DUPLICATE_SAME_ACCESS))
        {
            continue;
        }

        ULONG return_length = 0;
        const auto status = NtQueryObject(
            handle_copy,
            ObjectTypeInformation,
            object_info_buffer.data(),
            static_cast<ULONG>(object_info_buffer.size()),
            &return_length);
        if (NT_ERROR(status))
        {
            CloseHandle(handle_copy);
            continue;
        }

        auto object_type_info = reinterpret_cast<OBJECT_TYPE_INFORMATION*>(object_info_buffer.data());
        auto type_name = unicode_to_str(object_type_info->Name);

        if (type_name == L"File")
        {
            auto file_name = file_handle_to_kernel_name(handle_copy, object_info_buffer);
            result.push_back(HandleInfo{ pid, handle_info->HandleValue, std::move(type_name), std::move(file_name) });
        }

        CloseHandle(handle_copy);
    }

    for (auto [pid, handle] : pid_to_handle)
    {
        CloseHandle(handle);
    }

    return result;
}

// Returns the list of all processes.
// On failure, returns an empty vector.

std::wstring NtdllExtensions::pid_to_user(DWORD pid)
{
    HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    std::wstring user;
    std::wstring domain;

    if (process == nullptr)
    {
        return user;
    }

    HANDLE token = nullptr;

    if (!OpenProcessToken(process, TOKEN_QUERY, &token))
    {
        return user;
    }

    DWORD token_size = 0;
    const bool ok = GetTokenInformation(token, TokenUser, nullptr, 0, &token_size);
    if ((!ok && GetLastError() != ERROR_INSUFFICIENT_BUFFER) || !token_size)
    {
        return user;
    }

    std::vector<BYTE> token_buffer(token_size);
    GetTokenInformation(token, TokenUser, token_buffer.data(), token_size, &token_size);
    TOKEN_USER* user_ptr = reinterpret_cast<TOKEN_USER*>(token_buffer.data());
    PSID psid = user_ptr->User.Sid;
    DWORD user_buf_size = 0;
    DWORD domain_buf_size = 0;
    SID_NAME_USE sid_name;
    LookupAccountSidW(nullptr, psid, nullptr, &user_buf_size, nullptr, &domain_buf_size, &sid_name);
    if (!user_buf_size || !domain_buf_size)
    {
        return user;
    }

    user.resize(user_buf_size);
    domain.resize(domain_buf_size);
    LookupAccountSidW(nullptr, psid, user.data(), &user_buf_size, domain.data(), &domain_buf_size, &sid_name);
    user.resize(user.size() - 1);
    domain.resize(domain.size() - 1);
    CloseHandle(token);
    CloseHandle(process);

    return user;
}


std::vector<NtdllExtensions::ProcessInfo> NtdllExtensions::processes() noexcept
{
    auto get_info_result = NtQuerySystemInformationMemoryLoop(SystemProcessInformation);

    if (NT_ERROR(get_info_result.status))
    {
        return {};
    }

    std::vector<ProcessInfo> result;
    auto info_ptr = reinterpret_cast<PSYSTEM_PROCESS_INFORMATION>(get_info_result.memory.data());

    while (info_ptr->NextEntryOffset)
    {
        info_ptr = reinterpret_cast<decltype(info_ptr)>(reinterpret_cast<LPBYTE>(info_ptr) + info_ptr->NextEntryOffset);

        ProcessInfo item;
        item.name = unicode_to_str(info_ptr->ImageName);
        item.pid = static_cast<DWORD>(reinterpret_cast<uintptr_t>(info_ptr->UniqueProcessId));
        item.modules = process_modules(item.pid);
        item.user = pid_to_user(item.pid);

        result.push_back(item);
    }

    return result;
}
