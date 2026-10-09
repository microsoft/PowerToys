#include "helper.h"

#include "../utils/string_utils.h"

#include <algorithm>

VersionHelper::VersionHelper(const size_t major, const size_t minor, const size_t revision, const size_t build) :
    major{ major },
    minor{ minor },
    revision{ revision },
    build{ build }
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
        if (const auto suffixPos = str.find(static_cast<CharT>('-')); suffixPos != std::basic_string_view<CharT>::npos)
        {
            str = str.substr(0, suffixPos);
        }

        size_t parts[4]{};
        size_t partCount = 0;
        size_t start = 0;
        while (start <= str.size() && partCount < std::size(parts))
        {
            const auto dot = str.find(Constants<CharT>::DOT[0], start);
            const auto end = dot == std::basic_string_view<CharT>::npos ? str.size() : dot;
            const auto part = str.substr(start, end - start);
            if (part.empty() || !std::all_of(part.begin(), part.end(), [](const CharT c) { return c >= static_cast<CharT>('0') && c <= static_cast<CharT>('9'); }))
            {
                return std::nullopt;
            }

            parts[partCount++] = static_cast<size_t>(std::stoull(std::basic_string<CharT>{ part }));

            if (dot == std::basic_string_view<CharT>::npos)
            {
                start = str.size() + 1;
                break;
            }
            start = dot + 1;
        }

        if (partCount == 3 && start > str.size())
        {
            return VersionHelper{ parts[0], parts[1], parts[2] };
        }

        if (partCount == 4 && start > str.size())
        {
            return VersionHelper{ parts[0], parts[1], parts[2], parts[3] };
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
    if (build != 0)
    {
        result += L'.';
        result += std::to_wstring(build);
    }
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
    if (build != 0)
    {
        result += '.';
        result += std::to_string(build);
    }
    return result;
}
