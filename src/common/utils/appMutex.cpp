#include "pch.h"
#include "appMutex.h"

wil::unique_mutex_nothrow createAppMutex(const std::wstring& mutexName)
{
    wil::unique_mutex_nothrow result{ CreateMutexW(nullptr, TRUE, mutexName.c_str()) };

    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        return {};
    }

    return result;
}
