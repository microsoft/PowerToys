#pragma once

#include <string>
#include <compare>

struct VersionHelper
{
    VersionHelper(std::string str);
    VersionHelper(const size_t major, const size_t minor, const size_t revision);

    auto operator<=>(const VersionHelper&) const = default;

    size_t major;
    size_t minor;
    size_t revision;

    std::wstring toWstring() const;
    std::string toString() const;
};
