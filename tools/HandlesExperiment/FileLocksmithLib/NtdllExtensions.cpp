#include "pch.h"

#include "NtdllExtensions.h"

#define STATUS_INFO_LENGTH_MISMATCH ((LONG)0xC00000004)

// Calls NtQuerySystemInformation and returns a buffer containing the result.

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

        auto error = DuplicateHandle(process_handle, (HANDLE)handle_info->Handle, GetCurrentProcess(), &handle_copy, 0, 0, DUPLICATE_SAME_ACCESS);
        if (error)
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

        result.push_back(item);
    }

    return result;
}
