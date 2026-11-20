// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

// CharUpperBuffW, which Find My Mouse and this module use on the process path too. The unit tests
// compile this file under their own precompiled header, which has no Windows headers. It comes
// before the C++/WinRT headers so its GetObject macro renames their declarations too.
#include <Windows.h>

#include "FullScreenSettings.h"
#include "ModuleConstants.h"

#include <algorithm>

using winrt::Windows::Data::Json::IJsonValue;
using winrt::Windows::Data::Json::JsonObject;
using winrt::Windows::Data::Json::JsonValueType;

namespace
{
    const wchar_t JSON_KEY_FULLSCREEN_BEHAVIOR[] = L"fullscreen_behavior";
    const wchar_t JSON_KEY_EXCLUDED_APPS[] = L"excluded_apps";
    const wchar_t LINE_BREAKS[] = L"\r\n";
    const wchar_t WHITESPACE[] = L" \t\r\n";
    const wchar_t SEPARATORS[] = L"\\/ \t";

    // The `value` of the named property, when the property is an object that has one.
    std::optional<IJsonValue> ReadValue(const JsonObject& properties, const wchar_t* key)
    {
        if (!properties.HasKey(key))
        {
            return std::nullopt;
        }
        const IJsonValue property = properties.GetNamedValue(key);
        if (property.ValueType() != JsonValueType::Object)
        {
            return std::nullopt;
        }
        const JsonObject object = property.GetObject();
        if (!object.HasKey(cursorbrake::JSON_KEY_VALUE))
        {
            return std::nullopt;
        }
        return object.GetNamedValue(cursorbrake::JSON_KEY_VALUE);
    }
}

namespace cursorbrake
{
    std::optional<FullScreenBehavior> ReadFullScreenBehavior(const JsonObject& properties)
    {
        const std::optional<IJsonValue> value = ReadValue(properties, JSON_KEY_FULLSCREEN_BEHAVIOR);
        if (!value || value->ValueType() != JsonValueType::Number)
        {
            return std::nullopt;
        }
        return ChoiceOf<FullScreenBehavior>(value->GetNumber(), FULLSCREEN_BEHAVIOR_COUNT);
    }

    std::vector<std::wstring> ParseExcludedApps(std::wstring_view text)
    {
        std::vector<std::wstring> apps;
        while (!text.empty())
        {
            const size_t end = (std::min)(text.find_first_of(LINE_BREAKS), text.size());
            std::wstring_view line = text.substr(0, end);
            text.remove_prefix(end);
            if (!text.empty())
            {
                text.remove_prefix(1);
            }

            const size_t first = line.find_first_not_of(WHITESPACE);
            if (first == std::wstring_view::npos)
            {
                continue;
            }
            line = line.substr(first, line.find_last_not_of(WHITESPACE) - first + 1);

            // A line of separators alone matches every process path.
            if (line.find_first_not_of(SEPARATORS) == std::wstring_view::npos)
            {
                continue;
            }

            std::wstring app{ line };
            CharUpperBuffW(app.data(), static_cast<DWORD>(app.size()));
            apps.push_back(std::move(app));
        }
        return apps;
    }

    std::optional<std::vector<std::wstring>> ReadExcludedApps(const JsonObject& properties)
    {
        const std::optional<IJsonValue> value = ReadValue(properties, JSON_KEY_EXCLUDED_APPS);
        if (!value || value->ValueType() != JsonValueType::String)
        {
            return std::nullopt;
        }
        return ParseExcludedApps(value->GetString());
    }
}
