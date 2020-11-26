// logger.cpp : Defines the functions for the static library.
//
#include "pch.h"
#include "framework.h"
#include "logger.h"
#include <map>
#include <spdlog/sinks/daily_file_sink.h>
#include <spdlog\sinks\stdout_color_sinks-inl.h>
#include <iostream>

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
        cerr << "Can not create file logger. Create stdout logger instead" << endl;
        logger = spdlog::stdout_color_mt("some_unique_name");
    }

    logger->set_level(logLevel);
    logger->set_pattern("[%Y-%m-%d %H:%M:%S.%f] [p-%P] [t-%t] [%l] %v");
    spdlog::register_logger(logger);
    spdlog::flush_every(std::chrono::seconds(3));
    logger->info("{} logger is initialized", loggerName);
}
