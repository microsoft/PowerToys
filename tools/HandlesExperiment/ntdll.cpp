#include "ntdll.hpp"

#include <stdexcept>

Ntdll::Ntdll() {
    m_module = GetModuleHandleW(L"ntdll.dll");
    if (m_module == 0) {
        throw std::runtime_error{ "GetModuleHandleW returned null" };
    }

    m_NtQuerySystemInformation = (NtQuerySystemInformation_t)GetProcAddress(m_module, "NtQuerySystemInformation");
    if (m_NtQuerySystemInformation == 0) {
        throw std::runtime_error{ "GetProcAddress returned null for NtQuerySystemInformation" };
    }

    m_NtDuplicateObject = (NtDuplicateObject_t)GetProcAddress(m_module, "NtDuplicateObject");
    if (m_NtDuplicateObject == 0) {
        throw std::runtime_error{ "GetProcAddress returned null for NtDuplicateObject" };
    }

    m_NtQueryObject = (NtQueryObject_t)GetProcAddress(m_module, "NtQueryObject");
    if (m_NtQueryObject == 0) {
        throw std::runtime_error{ "GetProcAddress returned null for NtQueryObject" };
    }
}

NTSTATUS Ntdll::NtQuerySystemInformation(
    ULONG SystemInformationClass,
    PVOID SystemInformation,
    ULONG SystemInformationLength,
    PULONG ReturnLength)
{
    return m_NtQuerySystemInformation(SystemInformationClass, SystemInformation, SystemInformationLength, ReturnLength);
}

NTSTATUS Ntdll::NtDuplicateObject(
    HANDLE SourceProcessHandle,
    HANDLE SourceHandle,
    HANDLE TargetProcessHandle,
    PHANDLE TargetHandle,
    ACCESS_MASK DesiredAccess,
    ULONG Attributes,
    ULONG Options)
{
    return m_NtDuplicateObject(SourceProcessHandle, SourceHandle, TargetProcessHandle, TargetHandle, DesiredAccess, Attributes, Options);
}

NTSTATUS Ntdll::NtQueryObject(
    HANDLE ObjectHandle,
    ULONG ObjectInformationClass,
    PVOID ObjectInformation,
    ULONG ObjectInformationLength,
    PULONG ReturnLength)
{
    return m_NtQueryObject(ObjectHandle, ObjectInformationClass, ObjectInformation, ObjectInformationLength, ReturnLength);
}

std::wstring_view NtdllExtensions::unicode_to_view(UNICODE_STRING unicode_str) {
    return std::wstring_view(unicode_str.Buffer, unicode_str.Length / sizeof(WCHAR));
}

std::wstring NtdllExtensions::unicode_to_str(UNICODE_STRING unicode_str) {
    return std::wstring(unicode_str.Buffer, unicode_str.Length / sizeof(WCHAR));
}

// Calls NtQuerySystemInformation and returns a buffer containing the result.

NtdllExtensions::MemoryLoopResult NtdllExtensions::NtQuerySystemInformationMemoryLoop(ULONG SystemInformationClass) {
    MemoryLoopResult result;
    result.memory.resize(DefaultResultBufferSize);

    while (result.memory.size() <= MaxResultBufferSize) {
        ULONG result_len;
        result.status = NtQuerySystemInformation(SystemInformationClass, result.memory.data(), (ULONG)result.memory.size(), &result_len);

        if (result.status == STATUS_INFO_LENGTH_MISMATCH) {
            result.memory.resize(result.memory.size() * 2);
            continue;
        }

        if (NT_ERROR(result.status)) {
            result.memory.clear();
        }

        return result;
    }

    result.status = STATUS_INFO_LENGTH_MISMATCH;
    result.memory.clear();
    return result;
}

std::wstring NtdllExtensions::file_handle_to_kernel_name(HANDLE file_handle, std::vector<BYTE>& buffer) {
    if (GetFileType(file_handle) != FILE_TYPE_DISK) {
        return L"";
    }

    ULONG return_length;
    auto status = NtQueryObject(file_handle, ObjectNameInformation, buffer.data(), (ULONG)buffer.size(), &return_length);
    if (NT_SUCCESS(status)) {
        auto object_name_info = (UNICODE_STRING*)buffer.data();
        return unicode_to_str(*object_name_info);
    }

    return L"";
}

std::wstring NtdllExtensions::file_handle_to_kernel_name(HANDLE file_handle) {
    std::vector<BYTE> buffer(DefaultResultBufferSize);
    return file_handle_to_kernel_name(file_handle, buffer);
}

std::vector<NtdllExtensions::HandleInfo> NtdllExtensions::handles() noexcept {
    auto get_info_result = NtQuerySystemInformationMemoryLoop(SystemHandleInformation);
    if (NT_ERROR(get_info_result.status)) {
        return {};
    }

    auto info_ptr = (SYSTEM_HANDLE_INFORMATION*)get_info_result.memory.data();

    std::map<DWORD, HANDLE> pid_to_handle;
    std::vector<HandleInfo> result;

    std::vector<BYTE> object_info_buffer(DefaultResultBufferSize);

    for (ULONG i = 0; i < info_ptr->HandleCount; i++) {
        auto handle_info = info_ptr->Handles + i;
        DWORD pid = handle_info->ProcessId;

        HANDLE process_handle = NULL;
        auto iter = pid_to_handle.find(pid);
        if (iter != pid_to_handle.end()) {
            process_handle = iter->second;
        }
        else {
            process_handle = OpenProcess(PROCESS_DUP_HANDLE, FALSE, pid);
            if (!process_handle) {
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
        if (error) {
            // Ignore this handle.
            continue;
        }

        ULONG return_length;
        auto status = NtQueryObject(handle_copy, ObjectTypeInformation, object_info_buffer.data(), (ULONG)object_info_buffer.size(), &return_length);
        if (NT_ERROR(status)) {
            // Ignore this handle.
            CloseHandle(handle_copy);
            continue;
        }

        auto object_type_info = (OBJECT_TYPE_INFORMATION*)object_info_buffer.data();
        auto type_name = unicode_to_str(object_type_info->Name);

        std::wstring file_name = file_handle_to_kernel_name(handle_copy, object_info_buffer);

        if (type_name == L"File") {
            file_name = file_handle_to_kernel_name(handle_copy, object_info_buffer);
        }

        result.push_back(HandleInfo{ pid, handle_info->Handle, type_name, file_name });
        CloseHandle(handle_copy);
    }

    for (auto [pid, handle] : pid_to_handle) {
        CloseHandle(handle);
    }

    return result;
}

// Returns the list of all processes.
// On failure, returns an empty vector.

std::vector<NtdllExtensions::ProcessInfo> NtdllExtensions::processes() noexcept {
    auto get_info_result = NtQuerySystemInformationMemoryLoop(SystemProcessInformation);

    if (NT_ERROR(get_info_result.status)) {
        return {};
    }

    std::vector<ProcessInfo> result;
    auto info_ptr = (PSYSTEM_PROCESS_INFORMATION)get_info_result.memory.data();

    while (info_ptr->NextEntryOffset) {
        info_ptr = decltype(info_ptr)((LPBYTE)info_ptr + info_ptr->NextEntryOffset);

        ProcessInfo item;
        item.name = unicode_to_str(info_ptr->ImageName);
        item.pid = (DWORD)(uintptr_t)info_ptr->UniqueProcessId;

        result.push_back(item);
    }

    return result;
}
