#pragma once

#include <string>
#include <guiddef.h>

void LogToFile(std::string what);
std::string toMediaTypeString(GUID subtype);

#define RETURN_IF_FAILED_WITH_LOGGING(val)                                               \
    hr = (val);                                                                          \
    if (FAILED(hr))                                                                      \
    {                                                                                    \
        LogToFile(std::string(#val) + " Failed with error code: " + std::to_string(hr)); \
        return hr;                                                                       \
    }
