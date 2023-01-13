// logger.cpp : Defines the functions for the static library.
//
#include "pch.h"
#include "framework.h"
#include "logger.h"
#include <unordered_map>
#include <spdlog/sinks/daily_file_sink.h>
#include <spdlog/sinks/msvc_sink.h>
#include <spdlog/sinks/null_sink.h>
#include <spdlog/sinks/stdout_color_sinks-inl.h>
#include <iostream>

using spdlog::sinks_init_list;
using spdlog::level::level_enum;
using spdlog::sinks::daily_file_sink_mt;
using spdlog::sinks::msvc_sink_mt;
using std::make_shared;

namespace
{
    const std::unordered_map<std::wstring, level_enum> logLevelMapping = {
        { L"trace", level_enum::trace },
        { L"debug", level_enum::debug },
        { L"info", level_enum::info },
        { L"warn", level_enum::warn },
        { L"err", level_enum::err },
        { L"critical", level_enum::critical },
        { L"off", level_enum::off },
    };
}

level_enum getLogLevel(std::wstring_view logSettingsPath)
{
    auto logLevel = get_log_settings(logSettingsPath).logLevel;
    if (auto it = logLevelMapping.find(logLevel); it != logLevelMapping.end())
    {
        return it->second;
    }

    if (auto it = logLevelMapping.find(LogSettings::defaultLogLevel); it != logLevelMapping.end())
    {
        return it->second;
    }
    return level_enum::trace;
}

std::shared_ptr<spdlog::logger> Logger::logger = spdlog::null_logger_mt("null");

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
    bool newLoggerCreated = false;
    try
    {
        logger = spdlog::get(loggerName);
        if (logger == nullptr)
        {
            auto sink = make_shared<daily_file_sink_mt>(logFilePath, 0, 0, false, LogSettings::retention);
            if (IsDebuggerPresent())
            {
                auto msvc_sink = make_shared<msvc_sink_mt>();
                msvc_sink->set_pattern("[%Y-%m-%d %H:%M:%S.%f] [%n] [t-%t] [%l] %v");
                logger = make_shared<spdlog::logger>(loggerName, sinks_init_list{ sink, msvc_sink });
            }
            else
            {
                logger = make_shared<spdlog::logger>(loggerName, sink);
            }
            newLoggerCreated = true;
        }
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

    if (newLoggerCreated)
    {
        logger->set_level(logLevel);
        logger->set_pattern("[%Y-%m-%d %H:%M:%S.%f] [p-%P] [t-%t] [%l] %v");
        logger->flush_on(logLevel); // Auto flush on every log message.
        spdlog::register_logger(logger);
    }

    logger->info("{} logger is initialized", loggerName);
}

void Logger::init(std::vector<spdlog::sink_ptr> sinks)
{
    auto init_logger = std::make_shared<spdlog::logger>("", begin(sinks), end(sinks));
    if (!init_logger)
    {
        return;
    }

    Logger::logger = init_logger;
}
