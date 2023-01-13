#pragma once
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <Windows.h>
#include <string>

#include "wil/resource.h"
#include <lmcons.h>

namespace
{
    constexpr inline wchar_t POWERTOYS_MSI_MUTEX_NAME[] = L"Local\\PowerToys_Runner_MSI_InstanceMutex";
    constexpr inline wchar_t POWERTOYS_BOOTSTRAPPER_MUTEX_NAME[] = L"Local\\PowerToys_Bootstrapper_InstanceMutex";
}

inline wil::unique_mutex_nothrow createAppMutex(const std::wstring& mutexName)
{
    wil::unique_mutex_nothrow result{ CreateMutexW(nullptr, TRUE, mutexName.c_str()) };

    return GetLastError() == ERROR_ALREADY_EXISTS ? wil::unique_mutex_nothrow{} : std::move(result);
}
