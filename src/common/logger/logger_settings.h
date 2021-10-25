#pragma once
#include <string>

struct LogSettings
{
    // The following strings are not localizable
    inline const static std::wstring defaultLogLevel = L"trace";
    inline const static std::wstring logLevelOption = L"logLevel";
    inline const static std::string runnerLoggerName = "runner";
    inline const static std::wstring logPath = L"Logs\\";
    inline const static std::wstring runnerLogPath = L"RunnerLogs\\runner-log.txt";
    inline const static std::string actionRunnerLoggerName = "action-runner";
    inline const static std::wstring actionRunnerLogPath = L"RunnerLogs\\action-runner-log.txt";
    inline const static std::string updateLoggerName = "update";
    inline const static std::wstring updateLogPath = L"UpdateLogs\\update-log.txt";
    inline const static std::string launcherLoggerName = "launcher";
    inline const static std::wstring launcherLogPath = L"LogsModuleInterface\\launcher-log.txt";
    inline const static std::wstring awakeLogPath = L"Logs\\awake-log.txt";
    inline const static std::string fancyZonesLoggerName = "fancyzones";
    inline const static std::wstring fancyZonesLogPath = L"fancyzones-log.txt";
    inline const static std::wstring fancyZonesOldLogPath = L"FancyZonesLogs\\"; // needed to clean up old logs
    inline const static std::string shortcutGuideLoggerName = "shortcut-guide";
    inline const static std::wstring shortcutGuideLogPath = L"ShortcutGuideLogs\\shortcut-guide-log.txt";
    inline const static std::string keyboardManagerLoggerName = "keyboard-manager";
    inline const static std::wstring keyboardManagerLogPath = L"Logs\\keyboard-manager-log.txt";
    inline const static std::string findMyMouseLoggerName = "find-my-mouse";
    inline const static int retention = 30;
    std::wstring logLevel;
    LogSettings();
};

// Get log settings from file. File with default options is created if it does not exist
LogSettings get_log_settings(std::wstring_view file_name);
