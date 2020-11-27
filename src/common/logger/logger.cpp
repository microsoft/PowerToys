// logger.cpp : Defines the functions for the static library.
//
#include "pch.h"
#include "framework.h"
#include "logger.h"
#include <map>
#include <spdlog/sinks/daily_file_sink.h>
#include <spdlog\sinks\stdout_color_sinks-inl.h>
#include <iostream>
#include <spdlog\sinks\null_sink.h>

using namespace std;
using namespace spdlog;

map<wstring, spdlog::level::level_enum> logLevelMapping = {
    { L"trace", level::trace },
    { L"debug", level::debug },
    { L"info", level::info },
    { L"warn", level::warn },
    { L"err", level::err },
    { L"critical", level::critical },
    { L"off", level::off },
};

level::level_enum getLogLevel(std::wstring_view logSettingsPath)
{
    auto logLevel = get_log_settings(logSettingsPath).logLevel;
    level::level_enum result = logLevelMapping[LogSettings::defaultLogLevel];
    if (logLevelMapping.find(logLevel) != logLevelMapping.end())
    {
        result = logLevelMapping[logLevel];
    }

    return result;
}

std::shared_ptr<spdlog::logger> Logger::logger;

bool Logger::wasLogFailedShown()
{
    wchar_t* pValue;
    size_t len;
    _wdupenv_s(&pValue, &len, logFailedShown.c_str());
    delete[] pValue;
    return len;
}

void Logger::init(std::string loggerName, std::wstring logFilePath, std::wstring_view logSettingsPath)
{
    auto logLevel = getLogLevel(logSettingsPath);
    try
    {
        auto sink = make_shared<sinks::daily_file_sink_mt>(logFilePath, 0, 0, false, LogSettings::retention);
        logger = make_shared<spdlog::logger>(loggerName, sink);
    }
    catch (...)
    {
        logger = spdlog::null_logger_mt(loggerName);
        if (!wasLogFailedShown())
        {
            // todo: that message should be shown from init caller and strings should be localized 
            MessageBoxW(NULL,
                        L"Logger can not be initialized",
                        L"PowerToys",
                        MB_OK | MB_ICONERROR);

            SetEnvironmentVariable(logFailedShown.c_str(), L"yes");
        }

        return;
    }

    logger->set_level(logLevel);
    logger->set_pattern("[%Y-%m-%d %H:%M:%S.%f] [p-%P] [t-%t] [%l] %v");
    spdlog::register_logger(logger);
    spdlog::flush_every(std::chrono::seconds(3));
    logger->info("{} logger is initialized", loggerName);
}
