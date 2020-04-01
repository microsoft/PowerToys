#include "pch.h"
#include "VersionHelper.h"

#include <algorithm>
#include <sstream>

VersionHelper::VersionHelper(std::string str)
{
    std::replace(str.begin(), str.end(), '.', ' ');
    std::replace(str.begin(), str.end(), 'v', ' ');
    std::stringstream ss;

    ss << str;

    std::string temp;
    ss >> temp;
    std::stringstream(temp) >> major;
    ss >> temp;
    std::stringstream(temp) >> minor;
    ss >> temp;
    std::stringstream(temp) >> revision;
}

VersionHelper::VersionHelper(int major, int minor, int revision) :
    major(major),
    minor(minor),
    revision(revision)
{
}

bool VersionHelper::operator>(const VersionHelper& rhs)
{
    return std::tie(major, minor, revision) > std::tie(rhs.major, rhs.minor, rhs.revision);
}
