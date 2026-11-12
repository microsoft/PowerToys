#include "pch.h"

#include "NtdllExtensions.h"
#include <optional>

#define STATUS_INFO_LENGTH_MISMATCH ((LONG)0xC0000004)

// Calls NtQuerySystemInformation and returns a buffer containing the result.

namespace
{
    using NtQueryObjectFn = NTSTATUS(NTAPI*)(HANDLE, ULONG, PVOID, ULONG, PULONG);

    constexpr ULONG ObjectNameInformationClass = 1;
    constexpr size_t ObjectInfoBufferSize = 64 * 1024;

    std::wstring_view unicode_to_view(UNICODE_STRING unicode_str)
    {
        return std::wstring_view(unicode_str.Buffer, unicode_str.Length / sizeof(WCHAR));
    }

    std::wstring unicode_to_str(UNICODE_STRING unicode_str)
    {
        return std::wstring(unicode_str.Buffer, unicode_str.Length / sizeof(WCHAR));
    }

    NtQueryObjectFn get_nt_query_object()
    {
        static const auto nt_query_object = reinterpret_cast<NtQueryObjectFn>(GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "NtQueryObject"));
        return nt_query_object;
    }

    std::wstring file_handle_to_kernel_name(NtQueryObjectFn nt_query_object, HANDLE file_handle, std::vector<BYTE>& buffer)
    {
        if (!nt_query_object || GetFileType(file_handle) != FILE_TYPE_DISK)
        {
            return L"";
        }

        ULONG return_length;
        auto status = nt_query_object(file_handle, ObjectNameInformationClass, buffer.data(), static_cast<ULONG>(buffer.size()), &return_length);
        if (NT_SUCCESS(status))
        {
            auto object_name_info = reinterpret_cast<UNICODE_STRING*>(buffer.data());
            return unicode_to_str(*object_name_info);
        }

        return L"";
    }

    // GetFileType and NtQueryObject(ObjectNameInformation) can block indefinitely on some file
    // handles (e.g. synchronous handles with pending I/O), and neither accepts a timeout. They run
    // on a helper thread that is abandoned, never terminated, when it stalls.
    //
    // An abandoned thread can wake up at any time, including during process shutdown, so it must
    // not use the CRT or anything owned by the caller. It only touches this heap block, which it
    // shares with the caller through a reference count, and calls into kernel32/ntdll.
    struct NameQueryBlock
    {
        enum State : LONG
        {
            Busy,
            Done,
            Abandoned,
        };

        volatile LONG ref_count;
        volatile LONG state;
        HANDLE request_event;
        HANDLE done_event;
        NtQueryObjectFn nt_query_object;

        // Request. A null file_handle asks the thread to exit.
        HANDLE file_handle;

        // Response.
        bool is_disk;
        NTSTATUS status;
        alignas(UNICODE_STRING) BYTE buffer[ObjectInfoBufferSize];
    };

    void release_name_query_block(NameQueryBlock* block) noexcept
    {
        if (InterlockedDecrement(&block->ref_count) == 0)
        {
            if (block->request_event)
            {
                CloseHandle(block->request_event);
            }

            if (block->done_event)
            {
                CloseHandle(block->done_event);
            }

            HeapFree(GetProcessHeap(), 0, block);
        }
    }

    DWORD WINAPI name_query_thread(LPVOID parameter) noexcept
    {
        auto block = static_cast<NameQueryBlock*>(parameter);

        for (;;)
        {
            WaitForSingleObject(block->request_event, INFINITE);
            if (!block->file_handle)
            {
                break;
            }

            block->is_disk = GetFileType(block->file_handle) == FILE_TYPE_DISK;
            if (block->is_disk)
            {
                ULONG return_length;
                block->status = block->nt_query_object(block->file_handle, ObjectNameInformationClass, block->buffer, sizeof(block->buffer), &return_length);
            }

            if (InterlockedCompareExchange(&block->state, NameQueryBlock::Done, NameQueryBlock::Busy) == NameQueryBlock::Abandoned)
            {
                // The caller gave up on this request and transferred ownership of the handle to us.
                CloseHandle(block->file_handle);
                break;
            }

            SetEvent(block->done_event);
        }

        release_name_query_block(block);
        return 0;
    }

    class FileNameQueryWorker
    {
    public:
        FileNameQueryWorker(NtQueryObjectFn nt_query_object, std::chrono::milliseconds stall_timeout) :
            m_nt_query_object(nt_query_object),
            m_timeout(static_cast<DWORD>(stall_timeout.count()))
        {
        }

        FileNameQueryWorker(const FileNameQueryWorker&) = delete;
        FileNameQueryWorker& operator=(const FileNameQueryWorker&) = delete;

        ~FileNameQueryWorker()
        {
            if (m_block)
            {
                m_block->file_handle = nullptr;
                SetEvent(m_block->request_event);
                release_name_query_block(m_block);
            }
        }

        // Takes ownership of file_handle. Returns the kernel name of the file (empty if it is not
        // a disk file or the name can't be queried), or nullopt if the query stalled.
        std::optional<std::wstring> query(HANDLE file_handle)
        {
            if (!ensure_thread())
            {
                CloseHandle(file_handle);
                return std::nullopt;
            }

            m_block->file_handle = file_handle;
            m_block->is_disk = false;
            InterlockedExchange(&m_block->state, NameQueryBlock::Busy);
            SetEvent(m_block->request_event);

            if (WaitForSingleObject(m_block->done_event, m_timeout) != WAIT_OBJECT_0)
            {
                if (InterlockedCompareExchange(&m_block->state, NameQueryBlock::Abandoned, NameQueryBlock::Busy) == NameQueryBlock::Busy)
                {
                    release_name_query_block(m_block);
                    m_block = nullptr;
                    return std::nullopt;
                }

                // The query completed just as we timed out; consume its completion signal.
                WaitForSingleObject(m_block->done_event, INFINITE);
            }

            // The helper is done with the handle; close it before anything below can throw.
            CloseHandle(file_handle);

            std::wstring result;
            if (m_block->is_disk && NT_SUCCESS(m_block->status))
            {
                result = unicode_to_str(*reinterpret_cast<UNICODE_STRING*>(m_block->buffer));
            }

            return result;
        }

    private:
        bool ensure_thread() noexcept
        {
            if (m_block)
            {
                return true;
            }

            auto block = static_cast<NameQueryBlock*>(HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, sizeof(NameQueryBlock)));
            if (!block)
            {
                return false;
            }

            block->ref_count = 1;
            block->nt_query_object = m_nt_query_object;
            block->request_event = CreateEventW(nullptr, FALSE, FALSE, nullptr);
            block->done_event = CreateEventW(nullptr, FALSE, FALSE, nullptr);
            if (!block->request_event || !block->done_event)
            {
                release_name_query_block(block);
                return false;
            }

            InterlockedIncrement(&block->ref_count);
            HANDLE thread = CreateThread(nullptr, 0, name_query_thread, block, 0, nullptr);
            if (!thread)
            {
                InterlockedDecrement(&block->ref_count);
                release_name_query_block(block);
                return false;
            }

            CloseHandle(thread);
            m_block = block;
            return true;
        }

        NtQueryObjectFn m_nt_query_object;
        DWORD m_timeout;
        NameQueryBlock* m_block = nullptr;
    };
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

std::wstring NtdllExtensions::file_handle_to_kernel_name(HANDLE file_handle)
{
    std::vector<BYTE> buffer(DefaultResultBufferSize);
    return ::file_handle_to_kernel_name(get_nt_query_object(), file_handle, buffer);
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

std::vector<NtdllExtensions::HandleInfo> NtdllExtensions::handles(std::chrono::milliseconds stall_timeout) noexcept
{
    auto get_info_result = NtQuerySystemInformationMemoryLoop(SystemExtendedHandleInformation);
    if (NT_ERROR(get_info_result.status))
    {
        return {};
    }

    auto info_ptr = reinterpret_cast<SYSTEM_HANDLE_INFORMATION_EX*>(get_info_result.memory.data());

    std::map<ULONG_PTR, HANDLE> pid_to_handle;
    std::vector<HandleInfo> result;

    try
    {
        std::vector<BYTE> object_info_buffer(DefaultResultBufferSize);
        FileNameQueryWorker name_query_worker(get_nt_query_object(), stall_timeout);

        for (ULONG_PTR i = 0; i < info_ptr->NumberOfHandles; i++)
        {
            auto handle_info = info_ptr->Handles + i;
            auto pid = handle_info->UniqueProcessId;

            HANDLE process_handle;
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

            HANDLE handle_copy;
            if (!DuplicateHandle(process_handle, reinterpret_cast<HANDLE>(handle_info->HandleValue), GetCurrentProcess(), &handle_copy, 0, 0, DUPLICATE_SAME_ACCESS))
            {
                // Ignore this handle.
                continue;
            }

            ULONG return_length;
            auto status = NtQueryObject(handle_copy, ObjectTypeInformation, object_info_buffer.data(), static_cast<ULONG>(object_info_buffer.size()), &return_length);
            if (NT_ERROR(status))
            {
                // Ignore this handle.
                CloseHandle(handle_copy);
                continue;
            }

            // Compare without allocating so nothing can throw while handle_copy is open.
            auto object_type_info = reinterpret_cast<OBJECT_TYPE_INFORMATION*>(object_info_buffer.data());
            if (unicode_to_view(object_type_info->Name) != L"File")
            {
                CloseHandle(handle_copy);
                continue;
            }

            // A stalled query is skipped.
            if (auto file_name = name_query_worker.query(handle_copy))
            {
                result.push_back(HandleInfo{ pid, handle_info->HandleValue, L"File", std::move(*file_name) });
            }
        }
    }
    catch (...)
    {
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
