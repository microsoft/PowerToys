#pragma once
#include <string>

struct LogSettings
{
    inline const static std::wstring defaultLogLevel = L"warn";

    std::wstring logLevel;
    LogSettings();
};

LogSettings getLogSettings(std::wstring_view file_name);