#pragma once

#include <string>
#include <algorithm>
#include <cwctype>

namespace StringUtils
{
    bool CaseInsensitiveEquals(const std::wstring& str1, const std::wstring& str2);
}
