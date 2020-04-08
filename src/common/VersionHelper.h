#pragma once

#include <string>
#include <compare>

struct VersionHelper
{
    VersionHelper(std::string str);
    VersionHelper(int major, int minor, int revision);

    auto operator<=>(const VersionHelper&) const = default;

    int major;
    int minor;
    int revision;
};
