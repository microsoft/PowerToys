#pragma once

#include <string_view>
#include <string>
#include <algorithm>

template<typename CharT>
struct default_trim_arg
{
};

template<>
struct default_trim_arg<char>
{
    static inline constexpr std::string_view value = " \t\r\n";
};

template<>
struct default_trim_arg<wchar_t>
{
    static inline constexpr std::wstring_view value = L" \t\r\n";
};

template<typename CharT>
inline std::basic_string_view<CharT> left_trim(std::basic_string_view<CharT> s,
                                               const std::basic_string_view<CharT> chars_to_trim = default_trim_arg<CharT>::value)
{
    s.remove_prefix(std::min<size_t>(s.find_first_not_of(chars_to_trim), size(s)));
    return s;
}

template<typename CharT>
inline std::basic_string_view<CharT> right_trim(std::basic_string_view<CharT> s,
                                                const std::basic_string_view<CharT> chars_to_trim = default_trim_arg<CharT>::value)
{
    s.remove_suffix(std::min<size_t>(size(s) - s.find_last_not_of(chars_to_trim) - 1, size(s)));
    return s;
}

template<typename CharT>
inline std::basic_string_view<CharT> trim(std::basic_string_view<CharT> s,
                                          const std::basic_string_view<CharT> chars_to_trim = default_trim_arg<CharT>::value)
{
    return left_trim(right_trim(s, chars_to_trim), chars_to_trim);
}

template<typename CharT>
inline void replace_chars(std::basic_string<CharT>& s,
                          const std::basic_string_view<CharT> chars_to_replace,
                          const CharT replacement_char)
{
    for (const CharT c : chars_to_replace)
    {
        std::replace(begin(s), end(s), c, replacement_char);
    }
}

inline std::string unwide(const std::wstring& wide)
{
    std::string result(wide.length(), 0);
    std::transform(begin(wide), end(wide), result.begin(), [](const wchar_t c) {
        return static_cast<char>(c);
    });
    return result;
}
