#pragma once

#include "Generated Files\version_gen.h"

#include <string>

enum class version_architecture
{
    x64,
    arm
};

version_architecture get_current_architecture();
const wchar_t* get_architecture_string(const version_architecture);

inline std::wstring get_product_version(bool includeV = true)
{
    static std::wstring version = (includeV ? L"v" : L"") + std::to_wstring(VERSION_MAJOR) +
                                  L"." + std::to_wstring(VERSION_MINOR) +
                                  L"." + std::to_wstring(VERSION_REVISION);

    return version;
}

inline std::wstring get_std_product_version(bool includeV = true)
{
    static std::wstring version = (includeV ? L"v" : L"") + std::to_wstring(VERSION_MAJOR) +
                                  L"." + std::to_wstring(VERSION_MINOR) +
                                  L"." + std::to_wstring(VERSION_REVISION) + L".0";

    return version;
}
