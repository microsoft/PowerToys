#pragma once

#include <string_view>
#include <optional>
#include <filesystem>

class RcResource
{
public:
    const std::byte* _memory = nullptr;
    size_t _size = 0;

    static std::optional<RcResource> create(int resource_id, const std::wstring_view resource_class);
    bool saveAsFile(const std::filesystem::path destination);

private:
    RcResource() = delete;
    RcResource(const std::byte* memory, size_t size) :
        _memory{ memory }, _size{ size }
    {
    }
};
