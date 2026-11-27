// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "DetectedEdges.h"
#include "EdgeSettings.h"

using winrt::Windows::Data::Json::IJsonValue;
using winrt::Windows::Data::Json::JsonArray;
using winrt::Windows::Data::Json::JsonObject;
using winrt::Windows::Data::Json::JsonValue;
using winrt::Windows::Data::Json::JsonValueType;

namespace
{
    const wchar_t JSON_KEY_VERSION[] = L"version";
    const wchar_t JSON_KEY_IDENTIFIED[] = L"identified";
    const wchar_t JSON_KEY_MONITORS[] = L"monitors";
    const wchar_t JSON_KEY_EDGES[] = L"edges";
    const wchar_t JSON_KEY_SEEN[] = L"seen";
    const wchar_t JSON_KEY_EXPIRED[] = L"expired";
    const wchar_t JSON_KEY_ID[] = L"id";
    const wchar_t JSON_KEY_NAME[] = L"name";
    const wchar_t JSON_KEY_RECT[] = L"rect";
    const wchar_t JSON_KEY_DPI[] = L"dpi";
    const wchar_t JSON_KEY_PRIMARY[] = L"primary";
    const wchar_t JSON_KEY_NUMBER[] = L"number";
    const wchar_t JSON_KEY_BUILT_IN[] = L"built_in";
    const wchar_t JSON_KEY_AXIS[] = L"axis";
    const wchar_t JSON_KEY_AT[] = L"at";
    const wchar_t JSON_KEY_FROM[] = L"from";
    const wchar_t JSON_KEY_TO[] = L"to";
    const wchar_t JSON_KEY_LAST_SEEN_UTC[] = L"last_seen_utc";
    const wchar_t AXIS_VERTICAL[] = L"vertical";
    const wchar_t AXIS_HORIZONTAL[] = L"horizontal";

    JsonArray Numbers(std::initializer_list<double> values)
    {
        JsonArray list;
        for (const double value : values)
        {
            list.Append(JsonValue::CreateNumberValue(value));
        }
        return list;
    }

    JsonArray Ids(const std::wstring& first, const std::wstring& second)
    {
        JsonArray ids;
        ids.Append(JsonValue::CreateStringValue(first));
        ids.Append(JsonValue::CreateStringValue(second));
        return ids;
    }

    // The objects among the first MAX_SEEN_ENTRIES elements of the list `key` names, or none when
    // the file has no such list.
    std::vector<JsonObject> Entries(const JsonObject& file, const wchar_t* key)
    {
        std::vector<JsonObject> entries;
        if (!file.HasKey(key) || file.GetNamedValue(key).ValueType() != JsonValueType::Array)
        {
            return entries;
        }
        const JsonArray list = file.GetNamedArray(key);
        const uint32_t count = list.Size() < cursorbrake::MAX_SEEN_ENTRIES ? list.Size() : static_cast<uint32_t>(cursorbrake::MAX_SEEN_ENTRIES);
        for (uint32_t i = 0; i < count; ++i)
        {
            const IJsonValue value = list.GetAt(i);
            if (value.ValueType() == JsonValueType::Object)
            {
                entries.push_back(value.GetObject());
            }
        }
        return entries;
    }

    // An entry's `monitors`: two strings of at most MAX_MONITOR_ID_LENGTH characters, or none.
    std::optional<std::pair<std::wstring, std::wstring>> ReadIds(const JsonObject& entry)
    {
        if (!entry.HasKey(JSON_KEY_MONITORS) || entry.GetNamedValue(JSON_KEY_MONITORS).ValueType() != JsonValueType::Array)
        {
            return std::nullopt;
        }
        const JsonArray ids = entry.GetNamedArray(JSON_KEY_MONITORS);
        if (ids.Size() != 2 || ids.GetAt(0).ValueType() != JsonValueType::String || ids.GetAt(1).ValueType() != JsonValueType::String)
        {
            return std::nullopt;
        }
        std::wstring first{ ids.GetStringAt(0) };
        std::wstring second{ ids.GetStringAt(1) };
        if (first.size() > cursorbrake::MAX_MONITOR_ID_LENGTH || second.size() > cursorbrake::MAX_MONITOR_ID_LENGTH)
        {
            return std::nullopt;
        }
        return std::pair{ std::move(first), std::move(second) };
    }
}

namespace cursorbrake
{
    JsonObject BuildDetectedEdges(const std::vector<DetectedMonitor>& monitors, const std::vector<SeenPair>& seen, const std::vector<std::pair<std::wstring, std::wstring>>& expired)
    {
        JsonObject file;
        file.SetNamedValue(JSON_KEY_VERSION, JsonValue::CreateNumberValue(DETECTED_EDGES_VERSION));

        bool identified = true;
        Layout layout;
        JsonArray monitorList;
        for (const DetectedMonitor& monitor : monitors)
        {
            identified = identified && !monitor.id.empty();
            layout.monitors.push_back(Monitor{ monitor.rect, monitor.dpi });

            JsonObject entry;
            entry.SetNamedValue(JSON_KEY_ID, JsonValue::CreateStringValue(monitor.id));
            entry.SetNamedValue(JSON_KEY_NAME, JsonValue::CreateStringValue(monitor.name));
            entry.SetNamedValue(JSON_KEY_RECT, Numbers({ static_cast<double>(monitor.rect.left), static_cast<double>(monitor.rect.top), static_cast<double>(monitor.rect.right), static_cast<double>(monitor.rect.bottom) }));
            entry.SetNamedValue(JSON_KEY_DPI, JsonValue::CreateNumberValue(monitor.dpi));
            entry.SetNamedValue(JSON_KEY_PRIMARY, JsonValue::CreateBooleanValue(monitor.primary));
            entry.SetNamedValue(JSON_KEY_NUMBER, JsonValue::CreateNumberValue(monitor.number));
            entry.SetNamedValue(JSON_KEY_BUILT_IN, JsonValue::CreateBooleanValue(monitor.builtIn));
            monitorList.Append(entry);
        }
        file.SetNamedValue(JSON_KEY_IDENTIFIED, JsonValue::CreateBooleanValue(identified));
        file.SetNamedValue(JSON_KEY_MONITORS, monitorList);

        JsonArray edgeList;
        for (const InnerEdge& edge : InnerEdges(layout))
        {
            JsonObject entry;
            entry.SetNamedValue(JSON_KEY_MONITORS, Numbers({ static_cast<double>(edge.first), static_cast<double>(edge.second) }));
            entry.SetNamedValue(JSON_KEY_AXIS, JsonValue::CreateStringValue(edge.sideBySide ? AXIS_VERTICAL : AXIS_HORIZONTAL));
            entry.SetNamedValue(JSON_KEY_AT, JsonValue::CreateNumberValue(edge.at));
            entry.SetNamedValue(JSON_KEY_FROM, JsonValue::CreateNumberValue(edge.from));
            entry.SetNamedValue(JSON_KEY_TO, JsonValue::CreateNumberValue(edge.to));
            edgeList.Append(entry);
        }
        file.SetNamedValue(JSON_KEY_EDGES, edgeList);

        JsonArray seenList;
        for (const SeenPair& pair : seen)
        {
            JsonObject entry;
            entry.SetNamedValue(JSON_KEY_MONITORS, Ids(pair.first, pair.second));
            entry.SetNamedValue(JSON_KEY_LAST_SEEN_UTC, JsonValue::CreateStringValue(FormatUtc(pair.lastSeen)));
            seenList.Append(entry);
        }
        file.SetNamedValue(JSON_KEY_SEEN, seenList);

        JsonArray expiredList;
        for (const auto& [first, second] : expired)
        {
            JsonObject entry;
            entry.SetNamedValue(JSON_KEY_MONITORS, Ids(first, second));
            expiredList.Append(entry);
        }
        file.SetNamedValue(JSON_KEY_EXPIRED, expiredList);
        return file;
    }

    std::vector<SeenPair> ReadSeen(const JsonObject& file)
    {
        std::vector<SeenPair> seen;
        for (const JsonObject& entry : Entries(file, JSON_KEY_SEEN))
        {
            const std::optional<std::pair<std::wstring, std::wstring>> ids = ReadIds(entry);
            if (!ids || !entry.HasKey(JSON_KEY_LAST_SEEN_UTC) || entry.GetNamedValue(JSON_KEY_LAST_SEEN_UTC).ValueType() != JsonValueType::String)
            {
                continue;
            }
            const std::optional<int64_t> lastSeen = ParseUtc(entry.GetNamedString(JSON_KEY_LAST_SEEN_UTC));
            if (!lastSeen || *lastSeen >= SEEN_TIME_LIMIT)
            {
                continue;
            }
            seen.push_back(SeenPair{ ids->first, ids->second, *lastSeen });
        }
        return seen;
    }

    std::vector<std::pair<std::wstring, std::wstring>> ReadExpired(const JsonObject& file)
    {
        std::vector<std::pair<std::wstring, std::wstring>> expired;
        for (const JsonObject& entry : Entries(file, JSON_KEY_EXPIRED))
        {
            if (std::optional<std::pair<std::wstring, std::wstring>> ids = ReadIds(entry))
            {
                expired.push_back(std::move(*ids));
            }
        }
        return expired;
    }
}
