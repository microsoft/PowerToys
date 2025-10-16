#include "pch.h"
#include "color.h"

bool checkValidRGB(std::wstring_view hex, uint8_t* R, uint8_t* G, uint8_t* B)
{
    if (hex.length() != 7)
    {
        return false;
    }

    hex = hex.substr(1, 6); // remove #
    for (const auto& c : hex)
    {
        if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
        {
            return false;
        }
    }

    if (swscanf_s(hex.data(), L"%2hhx%2hhx%2hhx", R, G, B) != 3)
    {
        return false;
    }

    return true;
}

bool checkValidARGB(std::wstring_view hex, uint8_t* A, uint8_t* R, uint8_t* G, uint8_t* B)
{
    if (hex.length() != 9)
    {
        return false;
    }

    hex = hex.substr(1, 8); // remove #
    for (const auto& c : hex)
    {
        if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
        {
            return false;
        }
    }

    if (swscanf_s(hex.data(), L"%2hhx%2hhx%2hhx%2hhx", A, R, G, B) != 4)
    {
        return false;
    }

    return true;
}
