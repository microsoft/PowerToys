#include "helper.h"

#include "../utils/string_utils.h"

#include <algorithm>
#include <sstream>

VersionHelper::VersionHelper(const size_t major, const size_t minor, const size_t revision) :
    major{ major },
    minor{ minor },
    revision{ revision }
{
}

template<typename CharT>
struct Constants;

template<>
struct Constants<char>
{
    static inline const char* LOWER_V = "v";
    static inline const char* UPPER_V = "V";
    static inline const char* DOT = ".";
    static inline const char SPACE = ' ';
};

template<>
struct Constants<wchar_t>
{
    static inline const wchar_t* LOWER_V = L"v";
    static inline const wchar_t* UPPER_V = L"V";
    static inline const wchar_t* DOT = L".";
    static inline const wchar_t SPACE = L' ';
};

template<typename CharT>
std::optional<VersionHelper> fromString(std::basic_string_view<CharT> str)
{
    try
    {
        str = left_trim<CharT>(trim<CharT>(str), Constants<CharT>::LOWER_V);
        str = left_trim<CharT>(trim<CharT>(str), Constants<CharT>::UPPER_V);
        std::basic_string<CharT> spacedStr{ str };
        replace_chars<CharT>(spacedStr, Constants<CharT>::DOT, Constants<CharT>::SPACE);

        std::basic_istringstream<CharT> ss{ spacedStr };
        VersionHelper result{ 0, 0, 0 };
        ss >> result.major;
        ss >> result.minor;
        ss >> result.revision;
        if (!ss.fail() && ss.eof())
        {
            return result;
        }
    }
    catch (...)
    {
    }
    return std::nullopt;
}

std::optional<VersionHelper> VersionHelper::fromString(std::string_view s)
{
    return ::fromString(s);
}

std::optional<VersionHelper> VersionHelper::fromString(std::wstring_view s)
{
    return ::fromString(s);
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
