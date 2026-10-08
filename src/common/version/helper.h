#pragma once

#include <string>
#include <optional>
#include <compare>

struct VersionHelper
{
    VersionHelper(const size_t major, const size_t minor, const size_t revision, const size_t build = 0);

    auto operator<=>(const VersionHelper&) const = default;
    
    static std::optional<VersionHelper> fromString(std::string_view s);
    static std::optional<VersionHelper> fromString(std::wstring_view s);

    size_t major;
    size_t minor;
    size_t revision;
    size_t build;

    std::wstring toWstring() const;
    std::string toString() const;
};
