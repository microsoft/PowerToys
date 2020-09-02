#pragma once

#include <string_view>
#include <string>
#include <algorithm>

template<typename CharT>
inline constexpr std::basic_string_view<CharT> default_trim_arg()
{
    return reinterpret_cast<CharT*>(" \t\r\n");
}

template <typename CharT>
inline std::basic_string_view<CharT> left_trim(std::basic_string_view<CharT> s, const std::basic_string_view<CharT> chars_to_trim = default_trim_arg<CharT>())
{
    s.remove_prefix(std::min<size_t>(s.find_first_not_of(chars_to_trim), size(s)));
    return s;
}

template<typename CharT>
inline std::basic_string_view<CharT> right_trim(std::basic_string_view<CharT> s, const std::basic_string_view<CharT> chars_to_trim = default_trim_arg<CharT>())
{
    s.remove_suffix(std::min<size_t>(size(s) - s.find_last_not_of(chars_to_trim) - 1, size(s)));
    return s;
}

template<typename CharT>
inline std::basic_string_view<CharT> trim(std::basic_string_view<CharT> s, const std::basic_string_view<CharT> chars_to_trim = default_trim_arg<CharT>())
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
