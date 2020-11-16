#pragma once
#include <string>

struct LogSettings
{
    inline const static std::wstring defaultLogLevel = L"warn";
    inline const static std::wstring logLevelOption = L"logLevel";

    std::wstring logLevel;
    LogSettings();
};

// Get log settings from file. File with default options is created if it does not exist
LogSettings getLogSettings(std::wstring_view file_name);