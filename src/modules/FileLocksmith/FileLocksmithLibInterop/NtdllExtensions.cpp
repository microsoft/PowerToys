#include "pch.h"

#include "NtdllExtensions.h"
#include <thread>
#include <atomic>
#include <memory>
#include <mutex>

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

    // A worker blocked inside NtQueryObject cannot be stopped safely, so this function never
    // terminates one.
    //
    // This previously called TerminateThread(offload_function.native_handle(), 1) on a
    // std::thread that had already been detached. On the MSVC STL detach() clears the internal
    // handle, so native_handle() returns null and the call fails with ERROR_INVALID_HANDLE:
    // nothing was terminated, the stuck worker kept running, and the loop started another one
    // on the same std::map, std::vector and scratch buffer with no synchronization. Had the
    // terminate landed it would be no better, since killing a thread inside a syscall can
    // leave the CRT heap lock held or a container half-updated.
    //
    // Instead a stuck worker is abandoned. Everything it can still reach is either owned by it
    // outright or kept alive by this shared_ptr, so it stays harmless whether it eventually
    // returns or blocks forever.
    struct SharedState
    {
        MemoryLoopResult info;
        SYSTEM_HANDLE_INFORMATION_EX* info_ptr = nullptr;
        ULONG_PTR handle_count = 0;

        // Index of the next handle to hand out. Drives termination.
        std::atomic<ULONG_PTR> next_index{ 0 };
        // Handles fully inspected. Only used as the watchdog's progress signal; it advances
        // for skipped handles too, so only a genuinely stuck query looks like a stall.
        std::atomic<ULONG_PTR> processed{ 0 };

        std::mutex result_mutex;
        std::vector<HandleInfo> result;
    };

    auto state = std::make_shared<SharedState>();
    state->info = std::move(get_info_result);
    state->info_ptr = reinterpret_cast<SYSTEM_HANDLE_INFORMATION_EX*>(state->info.memory.data());
    state->handle_count = state->info_ptr->NumberOfHandles;

    // Each worker owns its scratch buffer and its process-handle cache outright, so an
    // abandoned worker shares no mutable state. `result` is the one exception and is only
    // touched while holding `result_mutex`, never across a blocking call.
    auto worker = [this, state] {
        std::vector<BYTE> object_info_buffer(DefaultResultBufferSize);
        std::map<ULONG_PTR, HANDLE> pid_to_handle;

        for (;;)
        {
            const ULONG_PTR index = state->next_index++;
            if (index >= state->handle_count)
            {
                break;
            }

            auto handle_info = state->info_ptr->Handles + index;
            auto pid = handle_info->UniqueProcessId;

            // A `GrantedAccess == 0x0012019f` skip used to sit here behind a "TODO uncomment
            // and investigate", suggested as the cure for the NtQueryObject hang. It does stop
            // the hang, but it also stops the tool from finding anything: 0x0012019F is exactly
            // FILE_GENERIC_READ | FILE_GENERIC_WRITE (0x120089 | 0x120116), the granted access
            // of an ordinary read-write file handle, which is the case File Locksmith exists to
            // report. With it enabled, a process holding a file opened with FILE_SHARE_NONE is
            // no longer listed. The stall is handled below instead, by abandoning the stuck
            // worker safely.

            HANDLE process_handle = NULL;
            auto iter = pid_to_handle.find(pid);
            if (iter != pid_to_handle.end())
            {
                process_handle = iter->second;
            }
            else
            {
                process_handle = OpenProcess(PROCESS_DUP_HANDLE, FALSE, static_cast<DWORD>(pid));
                if (!process_handle)
                {
                    state->processed++;
                    continue;
                }
                pid_to_handle[pid] = process_handle;
            }

            HANDLE handle_copy = NULL;
            auto dh_result = DuplicateHandle(process_handle, reinterpret_cast<HANDLE>(handle_info->HandleValue), GetCurrentProcess(), &handle_copy, 0, 0, DUPLICATE_SAME_ACCESS);
            if (dh_result == 0)
            {
                // Ignore this handle.
                state->processed++;
                continue;
            }

            ULONG return_length;
            auto status = NtQueryObject(handle_copy, ObjectTypeInformation, object_info_buffer.data(), static_cast<ULONG>(object_info_buffer.size()), &return_length);
            if (NT_ERROR(status))
            {
                // Ignore this handle.
                CloseHandle(handle_copy);
                state->processed++;
                continue;
            }

            auto object_type_info = reinterpret_cast<OBJECT_TYPE_INFORMATION*>(object_info_buffer.data());
            auto type_name = unicode_to_str(object_type_info->Name);

            if (type_name == L"File")
            {
                auto file_name = file_handle_to_kernel_name(handle_copy, object_info_buffer);

                std::scoped_lock lock{ state->result_mutex };
                state->result.push_back(HandleInfo{ pid, handle_info->HandleValue, type_name, file_name });
            }

            CloseHandle(handle_copy);
            state->processed++;
        }

        for (auto [pid, handle] : pid_to_handle)
        {
            CloseHandle(handle);
        }
    };

    constexpr DWORD poll_interval_ms = 200;
    // A worker is only declared stuck after several consecutive polls with no progress at
    // all. The previous code treated a single missed 200 ms tick as a hang, so a merely slow
    // query on a busy machine was enough to trigger the recovery path.
    constexpr int stalled_polls_before_abandon = 10;
    // Bounds how much we leak, and how long a pathological machine can keep us here. When
    // exceeded we return what has been collected so far instead of spawning more workers.
    constexpr int max_abandoned_workers = 4;

    int abandoned_workers = 0;

    while (state->next_index < state->handle_count && abandoned_workers < max_abandoned_workers)
    {
        std::thread worker_thread(worker);

        ULONG_PTR last_processed = state->processed;
        int stalled_polls = 0;

        for (;;)
        {
            // Valid here precisely because the thread has not been detached yet.
            if (WaitForSingleObject(worker_thread.native_handle(), poll_interval_ms) == WAIT_OBJECT_0)
            {
                worker_thread.join();
                break;
            }

            const ULONG_PTR processed_now = state->processed;
            if (processed_now != last_processed)
            {
                last_processed = processed_now;
                stalled_polls = 0;
                continue;
            }

            if (++stalled_polls < stalled_polls_before_abandon)
            {
                continue;
            }

            // Stuck in a call we cannot interrupt. Let it go and continue with a fresh
            // worker; `next_index` has already moved past the offending handle.
            worker_thread.detach();
            abandoned_workers++;
            break;
        }
    }

    std::scoped_lock lock{ state->result_mutex };
    return state->result;
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
