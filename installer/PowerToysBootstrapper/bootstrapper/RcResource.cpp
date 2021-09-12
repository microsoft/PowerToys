#include "pch.h"
#include "RcResource.h"

#include <fstream>

std::optional<RcResource> RcResource::create(int resource_id, const std::wstring_view resource_class)
{
    const HRSRC resHandle = FindResourceW(nullptr, MAKEINTRESOURCEW(resource_id), resource_class.data());
    if (!resHandle)
    {
        return std::nullopt;
    }

    const HGLOBAL memHandle = LoadResource(nullptr, resHandle);
    if (!memHandle)
    {
        return std::nullopt;
    }
    
    const size_t resSize = SizeofResource(nullptr, resHandle);
    if (!resSize)
    {
        return std::nullopt;
    }
    
    auto res = static_cast<const std::byte*>(LockResource(memHandle));
    if (!res)
    {
        return std::nullopt;
    }
    
    return RcResource{ res, resSize };
}

bool RcResource::saveAsFile(const std::filesystem::path destination)
{
    std::fstream installerFile{ destination, std::ios_base::binary | std::ios_base::out | std::ios_base::trunc };
    if (!installerFile.is_open())
    {
        return false;
    }
    
    installerFile.write(reinterpret_cast<const char*>(_memory), _size);
    return true;
}
