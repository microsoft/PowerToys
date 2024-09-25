#pragma once

#include <vector>
#include <string>

std::vector<std::wstring> split(std::wstring s, const std::wstring& delimiter)
{
    std::vector<std::wstring> tokens;
    size_t pos = 0;
    std::wstring token;
    while ((pos = s.find(delimiter)) != std::wstring::npos)
    {
        token = s.substr(0, pos);
        tokens.push_back(token);
        s.erase(0, pos + delimiter.length());
    }
    tokens.push_back(s);

    return tokens;
}
