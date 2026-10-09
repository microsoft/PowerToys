#pragma once

#define STRINGIZE2(s) #s
#define STRINGIZE(s) STRINGIZE2(s)

#include "Generated Files\version_gen.h"

#define FILE_VERSION VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION, VERSION_BUILD
#define FILE_VERSION_STRING  \
    STRINGIZE(VERSION_MAJOR) \
    "." STRINGIZE(VERSION_MINOR) "." STRINGIZE(VERSION_REVISION) "." STRINGIZE(VERSION_BUILD)
#define PRODUCT_VERSION FILE_VERSION
#define PRODUCT_VERSION_STRING FILE_VERSION_STRING

#define COMPANY_NAME "Microsoft Corporation"
#define COPYRIGHT_NOTE "Copyright (C) Microsoft Corporation. All rights reserved."
#define PRODUCT_NAME "PowerToys"

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
    std::wstring version = includeV ? L"v" : L"";
    version += std::to_wstring(VERSION_MAJOR);
    version += L".";
    version += std::to_wstring(VERSION_MINOR);
    version += L".";
    version += std::to_wstring(VERSION_REVISION);
    if constexpr (VERSION_BUILD != 0)
    {
        version += L".";
        version += std::to_wstring(VERSION_BUILD);
    }

    return version;
}

inline std::wstring get_std_product_version(bool includeV = true)
{
    std::wstring version = includeV ? L"v" : L"";
    version += std::to_wstring(VERSION_MAJOR);
    version += L".";
    version += std::to_wstring(VERSION_MINOR);
    version += L".";
    version += std::to_wstring(VERSION_REVISION);
    version += L".";
    version += std::to_wstring(VERSION_BUILD);

    return version;
}

inline std::wstring get_product_version_channel()
{
    return VERSION_CHANNEL;
}

inline std::wstring get_product_version_source_commit()
{
    return VERSION_SOURCE_COMMIT;
}
