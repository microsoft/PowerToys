#pragma once

#include <string>
#include <algorithm>
#include <cwctype>

namespace StringUtils
{
    bool CaseInsensitiveEquals(const std::wstring& str1, const std::wstring& str2)
    {
        if (str1.size() != str2.size())
        {
            return false;
        }

        return std::equal(str1.begin(), str1.end(), str2.begin(), [](wchar_t ch1, wchar_t ch2) {
            return towupper(ch1) == towupper(ch2);
        });
    }
}
