#include "pch.h"

#include "NtdllExtensions.h"

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
    auto get_info_result = NtQuerySystemInformationMemoryLoop(SystemHandleInformation);
    if (NT_ERROR(get_info_result.status))
    {
        return {};
    }

    auto info_ptr = (SYSTEM_HANDLE_INFORMATION*)get_info_result.memory.data();

    std::map<DWORD, HANDLE> pid_to_handle;
    std::vector<HandleInfo> result;

    std::vector<BYTE> object_info_buffer(DefaultResultBufferSize);

    for (ULONG i = 0; i < info_ptr->HandleCount; i++)
    {
        auto handle_info = info_ptr->Handles + i;
        DWORD pid = handle_info->ProcessId;

        HANDLE process_handle = NULL;
        auto iter = pid_to_handle.find(pid);
        if (iter != pid_to_handle.end())
        {
            process_handle = iter->second;
        }
        else
        {
            process_handle = OpenProcess(PROCESS_DUP_HANDLE, FALSE, pid);
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

        HANDLE handle_copy;

        auto dh_result = DuplicateHandle(process_handle, (HANDLE)handle_info->Handle, GetCurrentProcess(), &handle_copy, 0, 0, DUPLICATE_SAME_ACCESS);
        if (dh_result == 0)
        {
            // Ignore this handle.
            continue;
        }

        ULONG return_length;
        auto status = NtQueryObject(handle_copy, ObjectTypeInformation, object_info_buffer.data(), (ULONG)object_info_buffer.size(), &return_length);
        if (NT_ERROR(status))
        {
            // Ignore this handle.
            CloseHandle(handle_copy);
            continue;
        }

        auto object_type_info = (OBJECT_TYPE_INFORMATION*)object_info_buffer.data();
        auto type_name = unicode_to_str(object_type_info->Name);

        std::wstring file_name = file_handle_to_kernel_name(handle_copy, object_info_buffer);

        if (type_name == L"File")
        {
            file_name = file_handle_to_kernel_name(handle_copy, object_info_buffer);
        }

        result.push_back(HandleInfo{ pid, handle_info->Handle, type_name, file_name });
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

        result.push_back(item);
    }

    return result;
}
