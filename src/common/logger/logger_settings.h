#pragma once
#include <string>

struct LogSettings
{
    // The following strings are not localizable
    inline const static std::wstring defaultLogLevel = L"warn";
    inline const static std::wstring logLevelOption = L"logLevel";
    inline const static std::string runnerLoggerName = "runner";
    inline const static std::wstring runnerLogPath = L"RunnerLogs\\runner-log.txt";
    inline const static std::string launcherLoggerName = "launcher";
    inline const static std::wstring launcherLogPath = L"LogsModuleInterface\\launcher-log.txt";
    inline const static std::string shortcutGuideLoggerName = "shortcut-guide";
    inline const static std::wstring shortcutGuideLogPath = L"ShortcutGuideLogs\\shortcut-guide-log.txt";
    inline const static int retention = 30;
    std::wstring logLevel;
    LogSettings();
};

// Get log settings from file. File with default options is created if it does not exist
LogSettings get_log_settings(std::wstring_view file_name);
