#pragma once

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Data.Json.h>

#include <optional>
#include <fstream>

namespace json
{
    using namespace winrt::Windows::Data::Json;

    std::optional<JsonObject> from_file(std::wstring_view file_name);

    void to_file(std::wstring_view file_name, const JsonObject& obj);

    bool has(const json::JsonObject& o, std::wstring_view name, const json::JsonValueType type = JsonValueType::Object);

    template<typename T>
    inline std::enable_if_t<std::is_arithmetic_v<T>, JsonValue> value(const T arithmetic)
    {
        return json::JsonValue::CreateNumberValue(arithmetic);
    }

    template<typename T>
    inline std::enable_if_t<!std::is_arithmetic_v<T>, JsonValue> value(T s)
    {
        return json::JsonValue::CreateStringValue(s);
    }

    JsonValue value(const bool boolean);

    JsonValue value(JsonObject value);

    JsonValue value(JsonValue value);

    template<typename T, typename D = std::optional<T>>
        requires std::constructible_from<std::optional<T>, D>
    void get(const json::JsonObject& o, const wchar_t* name, T& destination, D default_value = std::nullopt)
    {
        try
        {
            if constexpr (std::is_same_v<T, bool>)
            {
                destination = o.GetNamedBoolean(name);
            }
            else if constexpr (std::is_arithmetic_v<T>)
            {
                destination = static_cast<T>(o.GetNamedNumber(name));
            }
            else if constexpr (std::is_same_v<T, std::wstring>)
            {
                destination = o.GetNamedString(name);
            }
            else if constexpr (std::is_same_v<T, json::JsonObject>)
            {
                destination = o.GetNamedObject(name);
            }
            else
            {
                static_assert(std::bool_constant<std::is_same_v<T, T&>>::value, "Unsupported type");
            }
        }
        catch (...)
        {
            std::optional<T> maybe_default{ std::move(default_value) };
            if (maybe_default.has_value())
                destination = std::move(*maybe_default);
        }
    }

}
