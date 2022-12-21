#pragma once

#include "pch.h"

#define DECLARE_NTDLL_FUNCTION(name, ...)       \
private:                                        \
    typedef NTSTATUS(NTAPI* name ## _t)(        \
        __VA_ARGS__                             \
    );                                          \
    name ## _t m_ ## name;                      \
public:                                         \
    NTSTATUS name(__VA_ARGS__);

class Ntdll
{
private:
    HMODULE m_module;
public:
    struct SYSTEM_HANDLE
    {
        ULONG ProcessId;
        BYTE ObjectTypeNumber;
        BYTE Flags;
        USHORT Handle;
        PVOID Object;
        ACCESS_MASK GrantedAccess;
    };

    struct SYSTEM_HANDLE_INFORMATION
    {
        ULONG HandleCount;
        SYSTEM_HANDLE Handles[1];
    };

    enum POOL_TYPE
    {
        NonPagedPool,
        PagedPool,
        NonPagedPoolMustSucceed,
        DontUseThisType,
        NonPagedPoolCacheAligned,
        PagedPoolCacheAligned,
        NonPagedPoolCacheAlignedMustS
    };

    struct OBJECT_TYPE_INFORMATION
    {
        UNICODE_STRING Name;
        ULONG TotalNumberOfObjects;
        ULONG TotalNumberOfHandles;
        ULONG TotalPagedPoolUsage;
        ULONG TotalNonPagedPoolUsage;
        ULONG TotalNamePoolUsage;
        ULONG TotalHandleTableUsage;
        ULONG HighWaterNumberOfObjects;
        ULONG HighWaterNumberOfHandles;
        ULONG HighWaterPagedPoolUsage;
        ULONG HighWaterNonPagedPoolUsage;
        ULONG HighWaterNamePoolUsage;
        ULONG HighWaterHandleTableUsage;
        ULONG InvalidAttributes;
        GENERIC_MAPPING GenericMapping;
        ULONG ValidAccess;
        BOOLEAN SecurityRequired;
        BOOLEAN MaintainHandleCount;
        USHORT MaintainTypeList;
        POOL_TYPE PoolType;
        ULONG PagedPoolUsage;
        ULONG NonPagedPoolUsage;
    };

    Ntdll();

    DECLARE_NTDLL_FUNCTION(NtQuerySystemInformation,
        ULONG SystemInformationClass,
        PVOID SystemInformation,
        ULONG SystemInformationLength,
        PULONG ReturnLength
    )

    DECLARE_NTDLL_FUNCTION(NtDuplicateObject,
        HANDLE SourceProcessHandle,
        HANDLE SourceHandle,
        HANDLE TargetProcessHandle,
        PHANDLE TargetHandle,
        ACCESS_MASK DesiredAccess,
        ULONG Attributes,
        ULONG Options
    )

    DECLARE_NTDLL_FUNCTION(NtQueryObject,
        HANDLE ObjectHandle,
        ULONG ObjectInformationClass,
        PVOID ObjectInformation,
        ULONG ObjectInformationLength,
        PULONG ReturnLength
    );
};
