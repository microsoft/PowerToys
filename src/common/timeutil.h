#pragma once

#include <ctime>
#include <cinttypes>
#include <string>
#include <optional>

#include <winrt/base.h>

namespace timeutil
{
    inline std::wstring to_string(const time_t time)
    {
        return std::to_wstring(static_cast<uint64_t>(time));
    }

    inline std::optional<std::time_t> from_string(const std::wstring& s)
    {
        try
        {
            uint64_t i = std::stoull(s);
            return static_cast<std::time_t>(i);
        }
        catch (...)
        {
            return std::nullopt;
        }
    }
    inline time_t from_filetime(const FILETIME& ft)
    {
        ULARGE_INTEGER ull;
        ull.LowPart = ft.dwLowDateTime;
        ull.HighPart = ft.dwHighDateTime;
        return ull.QuadPart / 10000000ULL - 11644473600ULL;
    }

    inline std::time_t now()
    {
        return winrt::clock::to_time_t(winrt::clock::now());
    }

    namespace diff
    {
        inline int64_t in_seconds(const std::time_t to, const std::time_t from)
        {
            return static_cast<int64_t>(std::difftime(to, from));
        }

        inline int64_t in_minutes(const std::time_t to, const std::time_t from)
        {
            return static_cast<int64_t>(std::difftime(to, from) / 60);
        }

        inline int64_t in_hours(const std::time_t to, const std::time_t from)
        {
            return static_cast<int64_t>(std::difftime(to, from) / 3600);
        }

        inline int64_t in_days(const std::time_t to, const std::time_t from)
        {
            return static_cast<int64_t>(std::difftime(to, from) / (3600 * 24));
        }
    }
}
