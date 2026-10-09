#pragma once

#include <string_view>
#include <optional>
#include <filesystem>
#include <fstream>

#include <Windows.h>

class RcResource
{
public:
    const std::byte* _memory = nullptr;
    size_t _size = 0;

    static inline std::optional<RcResource> create(int resource_id, const std::wstring_view resource_class, const HINSTANCE handle = nullptr)
    {
        const HRSRC resHandle = FindResourceW(handle, MAKEINTRESOURCEW(resource_id), resource_class.data());
        if (!resHandle)
        {
            return std::nullopt;
        }

        const HGLOBAL memHandle = LoadResource(handle, resHandle);
        if (!memHandle)
        {
            return std::nullopt;
        }

        const size_t resSize = SizeofResource(handle, resHandle);
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

    inline bool saveAsFile(const std::filesystem::path destination)
    {
        std::fstream installerFile{ destination, std::ios_base::binary | std::ios_base::out | std::ios_base::trunc };
        if (!installerFile.is_open())
        {
            return false;
        }

        installerFile.write(reinterpret_cast<const char*>(_memory), _size);
        return true;
    }

private:
    RcResource() = delete;
    RcResource(const std::byte* memory, size_t size) :
        _memory{ memory }, _size{ size }
    {
    }
};
