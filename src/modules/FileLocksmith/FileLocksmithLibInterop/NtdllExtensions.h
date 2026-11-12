#pragma once

#include "pch.h"

#include "NtdllBase.h"

#include <chrono>

class NtdllExtensions : protected Ntdll
{
private:
    constexpr static size_t DefaultResultBufferSize = 64 * 1024;
    constexpr static size_t MaxResultBufferSize = 1024 * 1024 * 1024;

    constexpr static int ObjectNameInformation = 1;
    constexpr static int SystemExtendedHandleInformation = 64;

    struct MemoryLoopResult
    {
        NTSTATUS status = 0;
        std::vector<BYTE> memory;
    };

    // Calls NtQuerySystemInformation and returns a buffer containing the result.
    MemoryLoopResult NtQuerySystemInformationMemoryLoop(ULONG SystemInformationClass);

public:
    constexpr static std::chrono::milliseconds DefaultHandleStallTimeout{ 200 };
    struct ProcessInfo
    {
        DWORD pid = 0;
        std::wstring name;
        std::wstring user;
        std::vector<std::wstring> modules;
    };

    struct HandleInfo
    {
        ULONG_PTR pid;
        ULONG_PTR handle;
        std::wstring type_name;
        std::wstring kernel_file_name;
    };

    std::wstring file_handle_to_kernel_name(HANDLE file_handle);

    std::wstring path_to_kernel_name(LPCWSTR path);

    // Gives the user name of the account running this process
    std::wstring pid_to_user(DWORD pid);

    // Returns all open file handles in the system. A handle whose query makes no progress within
    // stall_timeout is skipped.
    std::vector<HandleInfo> handles(std::chrono::milliseconds stall_timeout = DefaultHandleStallTimeout) noexcept;

    // Returns the list of all processes.
    // On failure, returns an empty vector.
    std::vector<ProcessInfo> processes() noexcept;
};
