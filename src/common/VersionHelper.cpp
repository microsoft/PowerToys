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

std::wstring VersionHelper::toWstring() const
{
    std::wstring result{ L"v" };
    result += std::to_wstring(major);
    result += L'.';
    result += std::to_wstring(minor);
    result += L'.';
    result += std::to_wstring(revision);
    return result;
}
