#pragma once
#include <spdlog/spdlog.h>
#include <type_traits>
#include "logger_settings.h"

// fmt 9+ no longer auto-formats enums. Provide a generic formatter that
// converts any scoped or unscoped enum to its underlying integer type so
// existing Logger::xxx(L"... {} ...", someEnum) call sites keep working
// after the spdlog 1.17 / fmt 12 upgrade.
namespace fmt
{
    template <typename E, typename Char>
    struct formatter<E, Char, std::enable_if_t<std::is_enum_v<E>>>
        : formatter<std::underlying_type_t<E>, Char>
    {
        template <typename FormatContext>
        auto format(E value, FormatContext& ctx) const
        {
            return formatter<std::underlying_type_t<E>, Char>::format(
                static_cast<std::underlying_type_t<E>>(value), ctx);
        }
    };
}

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
    static void trace(const FormatString& formatString, const Args&... args)
    {
        logger->trace(fmt::runtime(formatString), args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    static void debug(const FormatString& formatString, const Args&... args)
    {
        logger->debug(fmt::runtime(formatString), args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    static void info(const FormatString& formatString, const Args&... args)
    {
        logger->info(fmt::runtime(formatString), args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    static void warn(const FormatString& formatString, const Args&... args)
    {
        logger->warn(fmt::runtime(formatString), args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    static void error(const FormatString& formatString, const Args&... args)
    {
        logger->error(fmt::runtime(formatString), args...);
    }

    // log message should not be localized
    template<typename FormatString, typename... Args>
    static void critical(const FormatString& formatString, const Args&... args)
    {
        logger->critical(fmt::runtime(formatString), args...);
    }

    static void flush()
    {
        logger->flush();
    }
};
