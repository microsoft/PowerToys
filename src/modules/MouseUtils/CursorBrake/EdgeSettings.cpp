// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "EdgeSettings.h"
#include "ModuleConstants.h"

#include <cmath>
#include <string>

using winrt::Windows::Data::Json::IJsonValue;
using winrt::Windows::Data::Json::JsonArray;
using winrt::Windows::Data::Json::JsonObject;
using winrt::Windows::Data::Json::JsonValueType;

namespace
{
    const wchar_t JSON_KEY_EDGES[] = L"edges";
    const wchar_t JSON_KEY_MONITORS[] = L"monitors";
    const wchar_t JSON_KEY_MODE[] = L"mode";
    const wchar_t JSON_KEY_ONE_WAY[] = L"one_way";
    const wchar_t JSON_KEY_PUSH_DISTANCE[] = L"push_distance";

    std::optional<std::wstring> ReadId(const IJsonValue& value)
    {
        if (value.ValueType() != JsonValueType::String)
        {
            return std::nullopt;
        }
        std::wstring id{ value.GetString() };
        if (id.empty() || id.size() > cursorbrake::MAX_MONITOR_ID_LENGTH)
        {
            return std::nullopt;
        }
        return id;
    }

    std::optional<cursorbrake::EdgeOverride> ReadEntry(const IJsonValue& value)
    {
        if (value.ValueType() != JsonValueType::Object)
        {
            return std::nullopt;
        }
        const JsonObject entry = value.GetObject();

        if (!entry.HasKey(JSON_KEY_MONITORS) || entry.GetNamedValue(JSON_KEY_MONITORS).ValueType() != JsonValueType::Array)
        {
            return std::nullopt;
        }
        const JsonArray monitors = entry.GetNamedArray(JSON_KEY_MONITORS);
        if (monitors.Size() != 2)
        {
            return std::nullopt;
        }
        const std::optional<std::wstring> first = ReadId(monitors.GetAt(0));
        const std::optional<std::wstring> second = ReadId(monitors.GetAt(1));
        if (!first || !second || cursorbrake::SameMonitorId(*first, *second))
        {
            return std::nullopt;
        }

        if (!entry.HasKey(JSON_KEY_MODE) || entry.GetNamedValue(JSON_KEY_MODE).ValueType() != JsonValueType::Number)
        {
            return std::nullopt;
        }
        const std::optional<cursorbrake::EdgeMode> mode = cursorbrake::ChoiceOf<cursorbrake::EdgeMode>(entry.GetNamedNumber(JSON_KEY_MODE), cursorbrake::EDGE_MODE_COUNT);
        if (!mode)
        {
            return std::nullopt;
        }

        cursorbrake::EdgeOverride result;
        result.first = *first;
        result.second = *second;
        result.mode = *mode;

        if (entry.HasKey(JSON_KEY_ONE_WAY))
        {
            const IJsonValue oneWay = entry.GetNamedValue(JSON_KEY_ONE_WAY);
            if (oneWay.ValueType() != JsonValueType::Boolean)
            {
                return std::nullopt;
            }
            result.oneWay = oneWay.GetBoolean();
        }

        if (entry.HasKey(JSON_KEY_PUSH_DISTANCE))
        {
            const IJsonValue distance = entry.GetNamedValue(JSON_KEY_PUSH_DISTANCE);
            if (distance.ValueType() != JsonValueType::Number || !std::isfinite(distance.GetNumber()))
            {
                return std::nullopt;
            }
            result.pushDistance = cursorbrake::ClampPushDistance(distance.GetNumber());
        }
        return result;
    }
}

namespace cursorbrake
{
    std::vector<EdgeOverride> ParseEdgeOverrides(const JsonArray& entries)
    {
        std::vector<EdgeOverride> overrides;
        const uint32_t count = entries.Size() < MAX_EDGE_ENTRIES ? entries.Size() : static_cast<uint32_t>(MAX_EDGE_ENTRIES);
        for (uint32_t i = 0; i < count; ++i)
        {
            if (std::optional<EdgeOverride> entry = ReadEntry(entries.GetAt(i)))
            {
                overrides.push_back(std::move(*entry));
            }
        }
        return overrides;
    }

    std::optional<std::vector<EdgeOverride>> ReadEdgesSetting(const JsonObject& properties)
    {
        if (!properties.HasKey(JSON_KEY_EDGES))
        {
            return std::vector<EdgeOverride>{};
        }
        const IJsonValue property = properties.GetNamedValue(JSON_KEY_EDGES);
        if (property.ValueType() != JsonValueType::Object)
        {
            return std::nullopt;
        }
        const JsonObject object = property.GetObject();
        if (!object.HasKey(JSON_KEY_VALUE) || object.GetNamedValue(JSON_KEY_VALUE).ValueType() != JsonValueType::Array)
        {
            return std::nullopt;
        }
        return ParseEdgeOverrides(object.GetNamedArray(JSON_KEY_VALUE));
    }
}
