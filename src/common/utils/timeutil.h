#pragma once

#include <ctime>
#include <cinttypes>
#include <string>
#include <optional>

#include <winrt/base.h>

namespace timeutil
{
    std::string format_as_local(const char* format_string, const time_t time);

    std::wstring to_string(const time_t time);

    std::optional<std::time_t> from_string(const std::wstring& s);

    std::time_t now();

    namespace diff
    {
        int64_t in_seconds(std::time_t to, std::time_t from);

        int64_t in_minutes(std::time_t to, std::time_t from);

        int64_t in_hours(std::time_t to, std::time_t from);

        int64_t in_days(std::time_t to, std::time_t from);
    }
}
