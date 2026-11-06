// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <cwchar>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <string>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Foundation.Collections.h>

namespace diagnostic_privacy
{
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

    inline bool redact_security_keys(std::string& contents)
    {
        if (contents.find('\0') != std::string::npos)
        {
            return false;
        }

        const bool hasBom = contents.starts_with("\xef\xbb\xbf");
        winrt::Windows::Data::Json::JsonValue json{ nullptr };
        if (!winrt::Windows::Data::Json::JsonValue::TryParse(winrt::to_hstring(contents.substr(hasBom ? 3 : 0)), json))
        {
            return false;
        }
        if (redact_json_security_keys(json))
        {
            contents = (hasBom ? "\xef\xbb\xbf" : "") + winrt::to_string(json.Stringify());
        }
        return true;
    }

    inline bool is_sensitive_log_path(const std::filesystem::path& relativePath)
    {
        for (auto directory = relativePath.parent_path(); !directory.empty(); directory = directory.parent_path())
        {
            const auto name = directory.filename().wstring();
            if (_wcsicmp(name.c_str(), L"RunnerLogs") == 0)
            {
                return true;
            }
            const auto moduleName = directory.parent_path().filename().wstring();
            if (_wcsicmp(name.c_str(), L"Logs") == 0 &&
                (_wcsicmp(moduleName.c_str(), L"MouseWithoutBorders") == 0 ||
                 _wcsicmp(moduleName.c_str(), L"MouseWithoutBorders.backup") == 0))
            {
                return true;
            }
        }
        return false;
    }

    inline void sanitize_report(const std::filesystem::path& reportDirectory)
    {
        for (const auto& entry : std::filesystem::recursive_directory_iterator{ reportDirectory })
        {
            if (!entry.is_regular_file())
            {
                continue;
            }

            // Exclude historical log sources without attempting to recover embedded or malformed payloads.
            if (is_sensitive_log_path(entry.path().lexically_relative(reportDirectory)))
            {
                std::filesystem::remove(entry.path());
                continue;
            }

            const auto extension = entry.path().extension().wstring();
            if (_wcsicmp(extension.c_str(), L".json") != 0)
            {
                continue;
            }

            std::ifstream input{ entry.path(), std::ios::binary };
            input.exceptions(std::ios::failbit | std::ios::badbit);
            std::string contents{ std::istreambuf_iterator<char>{ input }, std::istreambuf_iterator<char>{} };
            input.close();

            // Unsupported encodings and incomplete sensitive values must not be exported unchanged.
            if (!redact_security_keys(contents))
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
