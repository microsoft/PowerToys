#pragma once

// disabling warning 4458 - declaration of 'identifier' hides class member
// to avoid warnings from GDI files - can't add winRT directory to external code
// in the Cpp.Build.props
#pragma warning(push)
#pragma warning(disable : 4458)
#include "gdiplus.h"
#pragma warning(pop)

namespace std
{
    template<>
    struct hash<GUID>
    {
        size_t operator()(const GUID& Value) const
        {
            RPC_STATUS status = RPC_S_OK;
            return ::UuidHash(&const_cast<GUID&>(Value), &status);
        }
    };
}

inline bool operator<(const GUID& guid1, const GUID& guid2)
{
    if (guid1.Data1 != guid2.Data1)
    {
        return guid1.Data1 < guid2.Data1;
    }
    if (guid1.Data2 != guid2.Data2)
    {
        return guid1.Data2 < guid2.Data2;
    }
    if (guid1.Data3 != guid2.Data3)
    {
        return guid1.Data3 < guid2.Data3;
    }
    for (int i = 0; i < 8; i++)
    {
        if (guid1.Data4[i] != guid2.Data4[i])
        {
            return guid1.Data4[i] < guid2.Data4[i];
        }
    }
    return false;
}
