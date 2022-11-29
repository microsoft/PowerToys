#include "pch.h"

#include "NtdllBase.h"

#include <future>
#include <thread>
#include <chrono>

#define STATUS_UNSUCCESSFUL ((NTSTATUS)0xC0000001L)

Ntdll::Ntdll()
{
    m_module = GetModuleHandleW(L"ntdll.dll");
    if (m_module == 0)
    {
        throw std::runtime_error{ "GetModuleHandleW returned null" };
    }

    m_NtQuerySystemInformation = (NtQuerySystemInformation_t)GetProcAddress(m_module, "NtQuerySystemInformation");
    if (m_NtQuerySystemInformation == 0)
    {
        throw std::runtime_error{ "GetProcAddress returned null for NtQuerySystemInformation" };
    }

    m_NtDuplicateObject = (NtDuplicateObject_t)GetProcAddress(m_module, "NtDuplicateObject");
    if (m_NtDuplicateObject == 0)
    {
        throw std::runtime_error{ "GetProcAddress returned null for NtDuplicateObject" };
    }

    m_NtQueryObject = (NtQueryObject_t)GetProcAddress(m_module, "NtQueryObject");
    if (m_NtQueryObject == 0)
    {
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
    // On very specific cases, NtQueryObject seems to hang.
    // "file object opened by a kernel-mode driver with FILE_SYNCHRONOUS_IO_NONALERT" and handles opened with certain permissions were examples I could find being reported on other sources.
    // Given this, lets do this call in a thread we can cancel.
    std::packaged_task<NTSTATUS()> task([=]() {
        return m_NtQueryObject(ObjectHandle, ObjectInformationClass, ObjectInformation, ObjectInformationLength, ReturnLength);
    });
    std::future<NTSTATUS> future = task.get_future();
    std::thread myThread(std::move(task));
    myThread.detach();

    std::future_status status = future.wait_for(std::chrono::milliseconds(100));
    if (status != std::future_status::ready)
    {
        return STATUS_UNSUCCESSFUL;
        TerminateThread(myThread.native_handle(), 1);
    }
    return future.get();

}
