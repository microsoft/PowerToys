#include "pch.h"
#include "RcResource.h"

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
    return RcResource{ { res, resSize } };
}
