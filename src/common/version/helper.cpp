#include "helper.h"

#include "../utils/string_utils.h"

#include <algorithm>
#include <sstream>

VersionHelper::VersionHelper(std::string str)
{
    // Remove whitespaces chars and a leading 'v'
    str = left_trim<char>(trim<char>(str), "v");
    // Replace '.' with spaces
    replace_chars(str, ".", ' ');

    std::istringstream ss{ str };
    ss >> major;
    ss >> minor;
    ss >> revision;
    if (ss.fail() || !ss.eof())
    {
        throw std::logic_error("VersionHelper: couldn't parse the supplied version string");
    }
}

VersionHelper::VersionHelper(const size_t major, const size_t minor, const size_t revision) :
    major{ major },
    minor{ minor },
    revision{ revision }
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

std::string VersionHelper::toString() const
{
    std::string result{ "v" };
    result += std::to_string(major);
    result += '.';
    result += std::to_string(minor);
    result += '.';
    result += std::to_string(revision);
    return result;
}
