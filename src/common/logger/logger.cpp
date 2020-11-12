// logger.cpp : Defines the functions for the static library.
//

#include "pch.h"
#include "framework.h"
#include "logger.h"
#include "spdlog/sinks/daily_file_sink.h"
#include "spdlog/spdlog.h"
#include <spdlog\sinks\rotating_file_sink.h>
#include <map>
#include "logger_settings.h"

using namespace std;
using namespace spdlog;

map<wstring, spdlog::level::level_enum> logLevelMapping = {
    { L"debug", level::debug },
    { L"info", level::info },
    { L"warn", level::warn },
    { L"err", level::err },
    { L"critical", level::critical },
    { L"off", level::off },
};

class Logger::impl
{
    friend class Logger;
    static shared_ptr<sinks::daily_file_sink_mt> sink;
    std::shared_ptr<spdlog::logger> logger;
    impl(string loggerName, shared_ptr<sinks::daily_file_sink_mt> sink, level::level_enum logLevel)
    {
        this->logger = make_shared<spdlog::logger>(loggerName, sink);
        this->logger->set_level(spdlog::level::debug);
        spdlog::register_logger(this->logger);
        spdlog::flush_every(std::chrono::seconds(3));
    }
};

Logger::Logger()
{
}

Logger::Logger(std::filesystem::path dir, std::string loggerName, wstring_view logSettingsPath)
{
    auto logPath = dir.append(loggerName);
    logPath = logPath.concat(".txt");
    static auto sink = make_shared<sinks::daily_file_sink_mt>(logPath.string(), 0, 0, false, 5);
    
    auto logLevel = getLogSettings(logSettingsPath).logLevel;
    level::level_enum level = logLevelMapping[LogSettings::defaultLogLevel];
    if (logLevelMapping.find(logLevel) != logLevelMapping.end())
    {
        level = logLevelMapping[logLevel];
    }

    _impl = decltype(_impl){ new Logger::impl{ loggerName, sink, level }, default_delete<Logger::impl>() }; 
}

void Logger::LogInfo(std::string str)
{
    _impl->logger->info(str);
}

Logger::~Logger() = default;