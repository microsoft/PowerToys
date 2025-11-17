#pragma once

#include <shlobj.h>

inline std::optional<GUID> GuidFromString(const std::wstring& str) noexcept
{
    GUID id;
    if (SUCCEEDED(CLSIDFromString(str.c_str(), &id)))
    {
        return id;
    }

    return std::nullopt;
}

inline std::wstring GuidToString(const GUID& guid) noexcept
{
    OLECHAR* guidString;
    StringFromCLSID(guid, &guidString);

    std::wstring guidWString(guidString);
    ::CoTaskMemFree(guidString);

    return guidWString;
}

inline std::wstring CreateGuidString()
{
    GUID guid;
    if (CoCreateGuid(&guid) == S_OK)
    {
        return GuidToString(guid);
    }

    return L"";
}
