#pragma once

#include <string_view>
#include <string>
#include <algorithm>

constexpr inline std::string_view default_trim_arg = " \t\r\n";

inline std::string_view left_trim(std::string_view s, const std::string_view chars_to_trim = default_trim_arg)
{
    s.remove_prefix(std::min(s.find_first_not_of(chars_to_trim), size(s)));
    return s;
}

inline std::string_view right_trim(std::string_view s, const std::string_view chars_to_trim = default_trim_arg)
{
    s.remove_suffix(std::min(size(s) - s.find_last_not_of(chars_to_trim) - 1, size(s)));
    return s;
}

inline std::string_view trim(std::string_view s, const std::string_view chars_to_trim = default_trim_arg)
{
    return left_trim(right_trim(s, chars_to_trim), chars_to_trim);
}

inline void replace_chars(std::string& s, const std::string_view chars_to_replace, const char replacement_char)
{
    for (const char c : chars_to_replace)
    {
        std::replace(begin(s), end(s), c, replacement_char);
    }
}
