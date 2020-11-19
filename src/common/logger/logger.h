#pragma once
#include <spdlog/spdlog.h>
#include "logger_settings.h"

class Logger
{
private:
    std::shared_ptr<spdlog::logger> logger;

public:
    Logger();
    Logger(std::string loggerName, std::wstring logFilePath, std::wstring_view logSettingsPath);

    // log message should not be localized
    template<typename FormatString, typename... Args>
    void trace(const FormatString& fmt, const Args&... args)
    {
        this->logger->trace(fmt, args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    void debug(const FormatString& fmt, const Args&... args)
    {
        this->logger->debug(fmt, args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    void info(const FormatString& fmt, const Args&... args)
    {
        this->logger->info(fmt, args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    void warn(const FormatString& fmt, const Args&... args)
    {
        this->logger->warn(fmt, args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    void error(const FormatString& fmt, const Args&... args)
    {
        this->logger->error(fmt, args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    void critical(const FormatString& fmt, const Args&... args)
    {
        this->logger->critical(fmt, args...);
    }

    ~Logger();
};
