#pragma once
#include <spdlog/spdlog.h>
#include "logger_settings.h"

class Logger
{
private:
    inline const static std::wstring logFailedShown = L"logFailedShown";
    static std::shared_ptr<spdlog::logger> logger;
    static bool wasLogFailedShown();

public:
    Logger() = delete;

    static void init(std::string loggerName, std::wstring logFilePath, std::wstring_view logSettingsPath);
    static void init(std::vector<spdlog::sink_ptr> sinks);

    // log message should not be localized
    template<typename FormatString, typename... Args>
    static void trace(const FormatString& fmt, const Args&... args)
    {
        logger->trace(fmt, args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    static void debug(const FormatString& fmt, const Args&... args)
    {
        logger->debug(fmt, args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    static void info(const FormatString& fmt, const Args&... args)
    {
        logger->info(fmt, args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    static void warn(const FormatString& fmt, const Args&... args)
    {
        logger->warn(fmt, args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    static void error(const FormatString& fmt, const Args&... args)
    {
        logger->error(fmt, args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    static void critical(const FormatString& fmt, const Args&... args)
    {
        logger->critical(fmt, args...);
    }

    static void flush()
    {
        logger->flush();
    }
};
