#include "pch.h"
#include "string_utils.h"

std::string unwide(const std::wstring& wide)
{
    std::string result(wide.length(), 0);
    std::transform(begin(wide), end(wide), result.begin(), [](const wchar_t c) {
        return static_cast<char>(c);
    });
    return result;
}
