#pragma once

#include <string>
#include <guiddef.h>
#include <system_error>

#include <wil/com.h>
#include <Windows.h>

void LogToFile(std::string what, const bool verbose = false);
void LogToFile(std::wstring what, const bool verbose = false);
std::string toMediaTypeString(GUID subtype);

#define RETURN_IF_FAILED_WITH_LOGGING(val)                                                             \
    hr = (val);                                                                                        \
    if (FAILED(hr))                                                                                    \
    {                                                                                                  \
        LogToFile(std::string(__FUNCTION__ "() ") + #val + ": " + std::system_category().message(hr)); \
        return hr;                                                                                     \
    }

#define RETURN_NULLPTR_IF_FAILED_WITH_LOGGING(val)                                                     \
    hr = val;                                                                                          \
    if (FAILED(hr))                                                                                    \
    {                                                                                                  \
        LogToFile(std::string(__FUNCTION__ "() ") + #val + ": " + std::system_category().message(hr)); \
        return nullptr;                                                                                \
    }

#define VERBOSE_LOG                                                 \
    std::string functionNameTMPVAR = __FUNCTION__;                  \
    LogToFile(std::string(functionNameTMPVAR + " enter"), true);    \
    auto verboseLogOnScopeEnd = wil::scope_exit([&] {               \
        LogToFile(std::string(functionNameTMPVAR + " exit"), true); \
    });

#if defined(PowerToysInterop)
#undef LOG
#define LOG(...)
#else
#define LOG(str) LogToFile(str, false);
#endif

constexpr inline bool failed(HRESULT hr)
{
    return hr != S_OK;
}

constexpr inline bool failed(bool val)
{
    return val == false;
}

template<typename T>
inline bool failed(wil::com_ptr_nothrow<T>& ptr)
{
    return ptr == nullptr;
}

#define OK_OR_BAIL(expr) \
    if (failed(expr))    \
        return {};
