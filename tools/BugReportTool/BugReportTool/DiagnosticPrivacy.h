// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <cwchar>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <optional>
#include <string>
#include <string_view>
#include <utility>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>

namespace diagnostic_privacy
{
    inline bool might_contain_security_key_name(std::string_view contents)
    {
        constexpr std::string_view name = "SecurityKey";
        constexpr std::string_view hexDigits = "0123456789abcdefABCDEF";
        size_t matched = 0;
        for (size_t index = 0; index < contents.size();)
        {
            auto character = contents[index++];
            if (character == '\\')
            {
                while (index < contents.size() && contents[index] == '\\')
                {
                    ++index;
                }
                if (contents.size() - index >= 5 && (contents[index] == 'u' || contents[index] == 'U'))
                {
                    unsigned int code = 0;
                    bool valid = true;
                    for (size_t digit = 1; digit <= 4; ++digit)
                    {
                        const auto value = hexDigits.find(contents[index + digit]);
                        if (value == std::string_view::npos)
                        {
                            valid = false;
                            break;
                        }
                        code = code * 16 + static_cast<unsigned int>(value < 16 ? value : value - 6);
                    }
                    if (valid)
                    {
                        // An escaped backslash can introduce another JSON escaping layer.
                        if (code == '\\')
                        {
                            return true;
                        }
                        character = code <= 0x7f ? static_cast<char>(code) : '\0';
                        index += 5;
                    }
                }
            }
            matched = character == name[matched] ? matched + 1 : (character == name.front() ? 1 : 0);
            if (matched == name.size())
            {
                return true;
            }
        }
        return false;
    }

    inline bool redact_json_security_keys(const winrt::Windows::Data::Json::IJsonValue& value)
    {
        using namespace winrt::Windows::Data::Json;
        bool changed = false;
        if (value.ValueType() == JsonValueType::Object)
        {
            const auto object = value.GetObjectW();
            for (const auto& property : object)
            {
                if (property.Key() != L"SecurityKey")
                {
                    changed = redact_json_security_keys(property.Value()) || changed;
                }
            }
            if (object.HasKey(L"SecurityKey"))
            {
                object.SetNamedValue(L"SecurityKey", JsonValue::CreateStringValue(L"<private_data>"));
                changed = true;
            }
        }
        else if (value.ValueType() == JsonValueType::Array)
        {
            for (const auto& element : value.GetArray())
            {
                changed = redact_json_security_keys(element) || changed;
            }
        }
        return changed;
    }

    inline std::optional<size_t> json_value_end(const std::string& contents, size_t start)
    {
        if (start == contents.size() || (contents[start] != '"' && contents[start] != '{'))
        {
            return std::nullopt;
        }
        int depth = 0;
        bool inString = false;
        for (auto end = start; end < contents.size(); ++end)
        {
            const auto ch = contents[end];
            if (inString && ch == '\\')
            {
                ++end;
                continue;
            }
            if (ch == '"')
            {
                inString = !inString;
            }
            else if (!inString && ch == '{')
            {
                ++depth;
            }
            else if (!inString && ch == '}')
            {
                --depth;
            }
            if (!inString && depth == 0)
            {
                return end + 1;
            }
        }
        return std::nullopt;
    }

    inline bool redact_security_keys(std::string& contents)
    {
        // Older MWB logs wrote the key as unstructured text.
        if (contents.find("GETSECKEY:") != std::string::npos)
        {
            return false;
        }

        const bool hasBom = contents.starts_with("\xef\xbb\xbf");
        winrt::Windows::Data::Json::JsonValue json{ nullptr };
        if (winrt::Windows::Data::Json::JsonValue::TryParse(winrt::to_hstring(contents.substr(hasBom ? 3 : 0)), json))
        {
            if (redact_json_security_keys(json))
            {
                contents = (hasBom ? "\xef\xbb\xbf" : "") + winrt::to_string(json.Stringify());
            }
        }

        std::string redacted;
        size_t cursor = 0;

        for (auto position = contents.find('"'); position != std::string::npos;)
        {
            const auto nameEnd = json_value_end(contents, position);
            if (!nameEnd)
            {
                return false;
            }
            winrt::Windows::Data::Json::JsonValue name{ nullptr };
            if (!winrt::Windows::Data::Json::JsonValue::TryParse(winrt::to_hstring(contents.substr(position, *nameEnd - position)), name) ||
                name.GetString() != L"SecurityKey")
            {
                position = contents.find('"', *nameEnd);
                continue;
            }
            auto valueStart = *nameEnd;
            const auto skipWhitespace = [&]() {
                while (valueStart < contents.size() && std::string_view{ " \t\r\n" }.find(contents[valueStart]) != std::string_view::npos)
                {
                    ++valueStart;
                }
            };
            skipWhitespace();
            if (valueStart == contents.size() || contents[valueStart++] != ':')
            {
                return false;
            }
            skipWhitespace();
            const auto valueEnd = json_value_end(contents, valueStart);
            winrt::Windows::Data::Json::JsonValue value{ nullptr };
            if (!valueEnd || !winrt::Windows::Data::Json::JsonValue::TryParse(winrt::to_hstring(contents.substr(valueStart, *valueEnd - valueStart)), value))
            {
                return false;
            }

            const auto prefix = contents.substr(cursor, position - cursor);
            if (might_contain_security_key_name(prefix))
            {
                return false;
            }
            redacted += prefix;
            redacted += "\"SecurityKey\":\"<private_data>\"";
            cursor = *valueEnd;
            position = contents.find('"', cursor);
        }
        if (might_contain_security_key_name(std::string_view{ contents }.substr(cursor)))
        {
            return false;
        }

        redacted.append(contents, cursor, std::string::npos);
        contents = std::move(redacted);
        return true;
    }

    inline void sanitize_report(const std::filesystem::path& reportDirectory)
    {
        for (const auto& entry : std::filesystem::recursive_directory_iterator{ reportDirectory })
        {
            const auto extension = entry.path().extension().wstring();
            if (!entry.is_regular_file() || (_wcsicmp(extension.c_str(), L".json") != 0 && _wcsicmp(extension.c_str(), L".log") != 0))
            {
                continue;
            }

            std::ifstream input{ entry.path(), std::ios::binary };
            input.exceptions(std::ios::failbit | std::ios::badbit);
            std::string contents{ std::istreambuf_iterator<char>{ input }, std::istreambuf_iterator<char>{} };
            input.close();

            if (contents.find('\0') == std::string::npos && !might_contain_security_key_name(contents) &&
                contents.find("GETSECKEY:") == std::string::npos)
            {
                continue;
            }

            // Unsupported encodings and incomplete sensitive values must not be exported unchanged.
            if (contents.find('\0') != std::string::npos || !redact_security_keys(contents))
            {
                std::filesystem::remove(entry.path());
                continue;
            }

            std::ofstream output{ entry.path(), std::ios::binary | std::ios::trunc };
            output.exceptions(std::ios::failbit | std::ios::badbit);
            output << contents;
            output.close();
        }
    }
}
