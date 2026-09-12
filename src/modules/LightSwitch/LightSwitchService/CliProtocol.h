// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <cstddef>
#include <optional>
#include <stdexcept>
#include <string>
#include <string_view>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Data.Json.h>

namespace light_switch_cli
{
    inline constexpr int ProtocolVersion = 1;
    inline constexpr size_t MaxMessageCharacters = 32768;

    enum class Command
    {
        Status,
        Light,
        Dark,
        Toggle,
        ScheduleEnable,
        ScheduleDisable,
    };

    struct Request
    {
        Command command;
        std::optional<std::wstring> mode;
    };

    class RequestError : public std::runtime_error
    {
    public:
        RequestError(std::wstring code, std::wstring message);

        const std::wstring& Code() const noexcept;
        const std::wstring& Message() const noexcept;

    private:
        std::wstring m_code;
        std::wstring m_message;
    };

    // Accepts the JSON payload without its UTF-16LE newline frame delimiter.
    // Malformed protocol data throws RequestError; no command is executed while parsing.
    Request ParseRequest(std::wstring_view message);

    winrt::Windows::Data::Json::JsonObject MakeSuccess(const winrt::Windows::Data::Json::JsonObject& state);
    winrt::Windows::Data::Json::JsonObject MakeError(
        std::wstring_view code,
        std::wstring_view message,
        const winrt::Windows::Data::Json::JsonObject& state = nullptr);
}
