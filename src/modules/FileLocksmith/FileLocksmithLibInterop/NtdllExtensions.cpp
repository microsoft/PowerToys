#include "pch.h"

#include "NtdllExtensions.h"
#include <thread>
#include <atomic>

#define STATUS_INFO_LENGTH_MISMATCH ((LONG)0xC0000004)

// Calls NtQuerySystemInformation and returns a buffer containing the result.

namespace
{
    std::wstring_view unicode_to_view(UNICODE_STRING unicode_str)
    {
        return std::wstring_view(unicode_str.Buffer, unicode_str.Length / sizeof(WCHAR));
    }

    std::wstring unicode_to_str(UNICODE_STRING unicode_str)
    {
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

        return { buffer, (UINT)lstrlenW(buffer) };
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
        result.status = NtQuerySystemInformation(SystemInformationClass, result.memory.data(), (ULONG)result.memory.size(), &result_len);

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
    auto status = NtQueryObject(file_handle, ObjectNameInformation, buffer.data(), (ULONG)buffer.size(), &return_length);
    if (NT_SUCCESS(status))
    {
        auto object_name_info = (UNICODE_STRING*)buffer.data();
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

    auto info_ptr = (SYSTEM_HANDLE_INFORMATION_EX*)get_info_result.memory.data();

    std::map<ULONG_PTR, HANDLE> pid_to_handle;
    std::vector<HandleInfo> result;

    std::vector<BYTE> object_info_buffer(DefaultResultBufferSize);

    std::atomic<ULONG> i = 0;
    std::atomic<ULONG_PTR> handle_count = info_ptr->NumberOfHandles;
    std::atomic<HANDLE> process_handle = NULL;
    std::atomic<HANDLE> handle_copy = NULL;
    ULONG previous_i;


    while (i < handle_count)
    {
        previous_i = i;

        // The system calls we use in this block were reported to hang on some machines.
        // We need to offload the cycle to another thread and keep track of progress to terminate and resume when needed.
        // Unfortunately, there are no alternative APIs to what we're using that accept timeouts. (NtQueryObject and GetFileType)
        auto offload_function = std::thread([&] {
            for (; i < handle_count; i++)
            {
                process_handle = NULL;
                handle_copy = NULL;

                auto handle_info = info_ptr->Handles + i;
                auto pid = handle_info->UniqueProcessId;

                auto iter = pid_to_handle.find(pid);
                if (iter != pid_to_handle.end())
                {
                    process_handle = iter->second;
                }
                else
                {
                    process_handle = OpenProcess(PROCESS_DUP_HANDLE, FALSE, (DWORD)pid);
                    if (!process_handle)
                    {
                        continue;
                    }
                    pid_to_handle[pid] = process_handle;
                }

                // According to this:
                // https://stackoverflow.com/questions/46384048/enumerate-handles
                // NtQueryObject could hang

                // TODO uncomment and investigate
                // if (handle_info->GrantedAccess == 0x0012019f) {
                //     continue;
                // }

                HANDLE local_handle_copy;
                auto dh_result = DuplicateHandle(process_handle, (HANDLE)handle_info->HandleValue, GetCurrentProcess(), &local_handle_copy, 0, 0, DUPLICATE_SAME_ACCESS);
                if (dh_result == 0)
                {
                    // Ignore this handle.
                    continue;
                }
                handle_copy = local_handle_copy;

                ULONG return_length;
                auto status = NtQueryObject(handle_copy, ObjectTypeInformation, object_info_buffer.data(), (ULONG)object_info_buffer.size(), &return_length);
                if (NT_ERROR(status))
                {
                    // Ignore this handle.
                    CloseHandle(handle_copy);
                    handle_copy = NULL;
                    continue;
                }

                auto object_type_info = (OBJECT_TYPE_INFORMATION*)object_info_buffer.data();
                auto type_name = unicode_to_str(object_type_info->Name);

                std::wstring file_name;

                if (type_name == L"File")
                {
                    file_name = file_handle_to_kernel_name(handle_copy, object_info_buffer);
                    result.push_back(HandleInfo{ pid, handle_info->HandleValue, type_name, file_name });
                }

                CloseHandle(handle_copy);
                handle_copy = NULL;
            }
        });

        offload_function.detach();
        do
        {
            Sleep(200); // Timeout in milliseconds for detecting that the system hang on getting information for a handle.
            if (i >= handle_count)
            {
                // We're done.
                break;
            }

            if (previous_i >= i)
            {
                // The thread looks like it's hanging on some handle. Let's kill it and resume.

                // HACK: This is unsafe and may leak something, but looks like there's no way to properly clean up a thread when it's hanging on a system call.
                TerminateThread(offload_function.native_handle(), 1);

                // Close Handles that might be lingering.
                if (handle_copy!=NULL)
                {
                    CloseHandle(handle_copy);
                }
                i++;
                break;
            }
            previous_i = i;
        } while (1);

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
    TOKEN_USER* user_ptr = (TOKEN_USER*)token_buffer.data();
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
    auto info_ptr = (PSYSTEM_PROCESS_INFORMATION)get_info_result.memory.data();

    while (info_ptr->NextEntryOffset)
    {
        info_ptr = decltype(info_ptr)((LPBYTE)info_ptr + info_ptr->NextEntryOffset);

        ProcessInfo item;
        item.name = unicode_to_str(info_ptr->ImageName);
        item.pid = (DWORD)(uintptr_t)info_ptr->UniqueProcessId;
        item.modules = process_modules(item.pid);
        item.user = pid_to_user(item.pid);

        result.push_back(item);
    }

    return result;
}
