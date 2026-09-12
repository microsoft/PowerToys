// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "CliProtocol.h"

#include <set>
#include <utility>

using namespace winrt::Windows::Data::Json;

namespace light_switch_cli
{
    namespace
    {
        void ValidateEncoding(std::wstring_view message)
        {
            if (message.empty() || message.size() > MaxMessageCharacters || message.front() == L'\xfeff')
            {
                throw RequestError(L"PROTOCOL_ERROR", L"The request must be a nonempty, BOM-less JSON object of at most 32768 characters.");
            }

            for (size_t index = 0; index < message.size(); ++index)
            {
                const auto value = static_cast<unsigned int>(message[index]);
                if (value == 0 || (value >= 0xdc00 && value <= 0xdfff))
                {
                    throw RequestError(L"PROTOCOL_ERROR", L"The request contains invalid UTF-16 data.");
                }
                if (value >= 0xd800 && value <= 0xdbff)
                {
                    if (++index == message.size() || message[index] < 0xdc00 || message[index] > 0xdfff)
                    {
                        throw RequestError(L"PROTOCOL_ERROR", L"The request contains invalid UTF-16 data.");
                    }
                }
            }
        }

        void RejectNestedValues(std::wstring_view message)
        {
            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (const auto value : message)
            {
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (value == L'\\')
                    {
                        escaped = true;
                    }
                    else if (value == L'"')
                    {
                        inString = false;
                    }
                }
                else if (value == L'"')
                {
                    inString = true;
                }
                else if (value == L'[' || (value == L'{' && ++depth > 1))
                {
                    // Every supported field is a primitive. Reject nested containers before
                    // invoking the JSON parser, including arbitrarily deep untrusted input.
                    throw RequestError(L"PROTOCOL_ERROR", L"Request properties must be primitive JSON values.");
                }
                else if (value == L'}')
                {
                    --depth;
                }
            }
        }

        // Windows.Data.Json keeps only the final value of duplicate object keys. Inspect the
        // already validated JSON text as well so duplicates cannot change a command's meaning.
        void RejectDuplicateProperties(std::wstring_view message)
        {
            std::set<std::wstring> properties;
            int depth = 0;
            bool expectingProperty = false;
            for (size_t index = 0; index < message.size(); ++index)
            {
                const auto value = message[index];
                if (value == L'"')
                {
                    const auto start = index++;
                    while (index < message.size())
                    {
                        if (message[index] == L'\\')
                        {
                            index += 2;
                        }
                        else if (message[index] == L'"')
                        {
                            break;
                        }
                        else
                        {
                            ++index;
                        }
                    }
                    if (depth == 1 && expectingProperty)
                    {
                        const auto key = JsonValue::Parse(winrt::hstring(message.substr(start, index - start + 1))).GetString();
                        if (!properties.emplace(key.c_str(), key.size()).second)
                        {
                            throw RequestError(L"PROTOCOL_ERROR", L"Duplicate request properties are not allowed.");
                        }
                        expectingProperty = false;
                    }
                }
                else if (value == L'{' || value == L'[')
                {
                    ++depth;
                    if (depth == 1)
                    {
                        expectingProperty = true;
                    }
                }
                else if (value == L'}' || value == L']')
                {
                    --depth;
                }
                else if (value == L',' && depth == 1)
                {
                    expectingProperty = true;
                }
            }
        }
    }

    RequestError::RequestError(std::wstring code, std::wstring message) :
        std::runtime_error("Invalid Light Switch CLI request"),
        m_code(std::move(code)),
        m_message(std::move(message))
    {
    }

    const std::wstring& RequestError::Code() const noexcept
    {
        return m_code;
    }

    const std::wstring& RequestError::Message() const noexcept
    {
        return m_message;
    }

    Request ParseRequest(std::wstring_view message)
    {
        ValidateEncoding(message);
        RejectNestedValues(message);

        JsonObject object;
        if (!JsonObject::TryParse(winrt::hstring(message), object))
        {
            throw RequestError(L"PROTOCOL_ERROR", L"The request must be a valid JSON object.");
        }
        RejectDuplicateProperties(message);

        for (const auto& property : object)
        {
            if (property.Key() != L"version" && property.Key() != L"command" && property.Key() != L"mode")
            {
                throw RequestError(L"INVALID_ARGUMENT", L"The request contains an unsupported property.");
            }
        }

        if (!object.HasKey(L"version") || object.GetNamedValue(L"version").ValueType() != JsonValueType::Number ||
            object.GetNamedNumber(L"version") != ProtocolVersion)
        {
            throw RequestError(L"PROTOCOL_ERROR", L"The request must specify protocol version 1.");
        }
        if (!object.HasKey(L"command") || object.GetNamedValue(L"command").ValueType() != JsonValueType::String)
        {
            throw RequestError(L"INVALID_ARGUMENT", L"The request must specify a command string.");
        }

        const auto command = object.GetNamedString(L"command");
        Request request{};
        if (command == L"status")
        {
            request.command = Command::Status;
        }
        else if (command == L"light")
        {
            request.command = Command::Light;
        }
        else if (command == L"dark")
        {
            request.command = Command::Dark;
        }
        else if (command == L"toggle")
        {
            request.command = Command::Toggle;
        }
        else if (command == L"schedule-enable")
        {
            request.command = Command::ScheduleEnable;
        }
        else if (command == L"schedule-disable")
        {
            request.command = Command::ScheduleDisable;
        }
        else
        {
            throw RequestError(L"INVALID_ARGUMENT", L"The request specifies an unsupported command.");
        }

        if (object.HasKey(L"mode"))
        {
            if (request.command != Command::ScheduleEnable || object.GetNamedValue(L"mode").ValueType() != JsonValueType::String)
            {
                throw RequestError(L"INVALID_ARGUMENT", L"Only schedule-enable accepts a mode string.");
            }
            const auto mode = object.GetNamedString(L"mode");
            if (mode != L"FixedHours" && mode != L"SunsetToSunrise" && mode != L"FollowNightLight")
            {
                throw RequestError(L"INVALID_ARGUMENT", L"Mode must be FixedHours, SunsetToSunrise, or FollowNightLight.");
            }
            request.mode = std::wstring(mode.c_str(), mode.size());
        }

        return request;
    }

    JsonObject MakeSuccess(const JsonObject& state)
    {
        JsonObject response;
        response.SetNamedValue(L"version", JsonValue::CreateNumberValue(ProtocolVersion));
        response.SetNamedValue(L"success", JsonValue::CreateBooleanValue(true));
        response.SetNamedValue(L"state", state);
        return response;
    }

    JsonObject MakeError(std::wstring_view code, std::wstring_view message, const JsonObject& state)
    {
        JsonObject error;
        error.SetNamedValue(L"code", JsonValue::CreateStringValue(winrt::hstring(code)));
        error.SetNamedValue(L"message", JsonValue::CreateStringValue(winrt::hstring(message)));
        JsonObject response;
        response.SetNamedValue(L"version", JsonValue::CreateNumberValue(ProtocolVersion));
        response.SetNamedValue(L"success", JsonValue::CreateBooleanValue(false));
        response.SetNamedValue(L"error", error);
        if (state)
        {
            response.SetNamedValue(L"state", state);
        }
        return response;
    }
}
