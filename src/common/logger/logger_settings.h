#pragma once
#include <string>

struct LogSettings
{
    // The following strings are not localizable
    inline const static std::wstring defaultLogLevel = L"warn";
    inline const static std::wstring logLevelOption = L"logLevel";

    std::wstring logLevel;
    LogSettings();
};

// Get log settings from file. File with default options is created if it does not exist
LogSettings get_log_settings(std::wstring_view file_name);
