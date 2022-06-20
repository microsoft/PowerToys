#pragma once

#include <optional>
#include <string>
#include <system_error>
#include <Windows.h>

inline std::optional<std::wstring> get_last_error_message(const DWORD dw)
{
    std::optional<std::wstring> message;
    try
    {
        const auto msg = std::system_category().message(dw);
        message.emplace(begin(msg), end(msg));
    }
    catch (...)
    {
    }
    return message;
}

inline std::wstring get_last_error_or_default(const DWORD dw)
{
    auto message = get_last_error_message(dw);
    return message.has_value() ? message.value() : L"";
}
