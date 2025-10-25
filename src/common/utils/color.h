#pragma once

#include <cstdint>
#include <string_view>

// helper function to get the RGB from a #FFFFFF string.
bool checkValidRGB(std::wstring_view hex, uint8_t* R, uint8_t* G, uint8_t* B);

// helper function to get the ARGB from a #FFFFFFFF string.
bool checkValidARGB(std::wstring_view hex, uint8_t* A, uint8_t* R, uint8_t* G, uint8_t* B);
