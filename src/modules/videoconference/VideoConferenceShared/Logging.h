#pragma once

#include <string>
#include <guiddef.h>
#include <system_error>

void LogToFile(std::string what, const bool verbose = false);
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