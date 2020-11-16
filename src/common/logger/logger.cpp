// logger.cpp : Defines the functions for the static library.
//
#include "pch.h"
#include "framework.h"
#include "logger.h"
#include "logger_settings.h"
#include <map>
#include <spdlog/sinks/daily_file_sink.h>

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
    auto logLevel = getLogSettings(logSettingsPath).logLevel;
    level::level_enum result = logLevelMapping[LogSettings::defaultLogLevel];
    if (logLevelMapping.find(logLevel) != logLevelMapping.end())
    {
        result = logLevelMapping[logLevel];
    }

    return result;
}

Logger::Logger()
{
}

Logger::Logger(std::string loggerName, std::wstring logFilePath, std::wstring_view logSettingsPath)
{
    auto sink = make_shared<sinks::daily_file_sink_mt>(logFilePath, 0, 0, false, 5);
    auto logLevel = getLogLevel(logSettingsPath);
    this->logger = make_shared<spdlog::logger>(loggerName, sink);
    this->logger->set_level(logLevel);
    spdlog::register_logger(this->logger);
    spdlog::flush_every(std::chrono::seconds(3));
}

Logger::~Logger() = default;