#pragma once

#include <string>

struct VersionHelper
{
    VersionHelper(std::string str);
    VersionHelper(int major, int minor, int revision);

    bool operator>(const VersionHelper& rhs);

    int major;
    int minor;
    int revision;
};
