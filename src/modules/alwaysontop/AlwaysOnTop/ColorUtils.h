#pragma once

#include <Windows.h>

#include <cwctype>
#include <string_view>

namespace AlwaysOnTopColorUtils
{
    inline int HexDigitValue(wchar_t value) noexcept
    {
        if (value >= L'0' && value <= L'9')
        {
            return value - L'0';
        }

        if (value >= L'a' && value <= L'f')
        {
            return value - L'a' + 10;
        }

        if (value >= L'A' && value <= L'F')
        {
            return value - L'A' + 10;
        }

        return -1;
    }

    inline COLORREF HexToRGB(std::wstring_view hex, const COLORREF fallbackColor = RGB(255, 255, 255)) noexcept
    {
        while (!hex.empty() && std::iswspace(hex.front()) != 0)
        {
            hex.remove_prefix(1);
        }

        while (!hex.empty() && std::iswspace(hex.back()) != 0)
        {
            hex.remove_suffix(1);
        }

        if (!hex.empty() && hex.front() == L'#')
        {
            hex.remove_prefix(1);
        }

        if (hex.length() != 6)
        {
            return fallbackColor;
        }

        int values[6]{};
        for (size_t index = 0; index < hex.length(); ++index)
        {
            values[index] = HexDigitValue(hex[index]);
            if (values[index] < 0)
            {
                return fallbackColor;
            }
        }

        return RGB((values[0] << 4) | values[1], (values[2] << 4) | values[3], (values[4] << 4) | values[5]);
    }
}
