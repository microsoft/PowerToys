#pragma once

#include <string_view>
#include <optional>
#include <span>

class RcResource
{
public:
    std::span<const std::byte> _memory;

    static std::optional<RcResource> create(int resource_id, const std::wstring_view resource_class);

private:
    RcResource() = delete;
    RcResource(std::span<const std::byte> memory) :
        _memory{ std::move(memory) }
    {
    }
};
