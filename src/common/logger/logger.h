#pragma once
#define SPDLOG_WCHAR_FILENAMES
#include <spdlog/spdlog.h>

class Logger
{
private:
    std::shared_ptr<spdlog::logger> logger;

public:
    Logger();
    Logger(std::string loggerName, std::wstring logFilePath, std::wstring_view logSettingsPath);
    
    template<typename FormatString, typename... Args>
    void trace(const FormatString& fmt, const Args&... args)
    {
        this->logger->trace(fmt, args...);
    }

    template<typename FormatString, typename... Args>
    void debug(const FormatString& fmt, const Args&... args)
    {
        this->logger->debug(fmt, args...);
    }

    template<typename FormatString, typename... Args>
    void info(const FormatString& fmt, const Args&... args)
    {
        this->logger->info(fmt, args...);
    }

    template<typename FormatString, typename... Args>
    void warn(const FormatString& fmt, const Args&... args)
    {
        this->logger->warn(fmt, args...);
    }

    template<typename FormatString, typename... Args>
    void error(const FormatString& fmt, const Args&... args)
    {
        this->logger->error(fmt, args...);
    }

    template<typename FormatString, typename... Args>
    void critical(const FormatString& fmt, const Args&... args)
    {
        this->logger->critical(fmt, args...);
    }

    ~Logger();
};