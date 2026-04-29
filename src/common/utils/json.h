#pragma once

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Data.Json.h>

#include <optional>
#include <fstream>

namespace json
{
    using namespace winrt::Windows::Data::Json;

    // Escapes all non-ASCII characters (code points > U+007F) in a wide string to their
    // JSON \uXXXX escape sequence equivalents. This prevents Windows.Data.Json from
    // mishandling characters such as U+FFFD (REPLACEMENT CHARACTER) during Stringify()
    // or Parse(), which can silently corrupt or drop those characters.
    inline std::wstring escape_non_ascii(const std::wstring& input)
    {
        // Each non-ASCII character may expand to up to 6 wide chars (e.g. \uFFFD) or
        // 12 wide chars for a surrogate pair (e.g. \uD83D\uDE00), so reserve generously.
        std::wstring result;
        result.reserve(input.size() * 6);
        for (size_t i = 0; i < input.size(); ++i)
        {
            wchar_t c = input[i];
            if (c > 0x7F)
            {
                // Handle UTF-16 surrogate pairs for code points outside the BMP.
                if (c >= 0xD800 && c <= 0xDBFF && i + 1 < input.size())
                {
                    const wchar_t low = input[i + 1];
                    if (low >= 0xDC00 && low <= 0xDFFF)
                    {
                        // Buffer: 2 x (backslash + 'u' + 4 hex digits) + null = 13 wchar_t.
                        wchar_t buf[13];
                        swprintf_s(buf, 13, L"\\u%04X\\u%04X",
                                   static_cast<unsigned int>(c),
                                   static_cast<unsigned int>(low));
                        result += buf;
                        ++i; // consume the low surrogate
                        continue;
                    }
                }
                // Buffer: backslash + 'u' + 4 hex digits + null = 7 wchar_t.
                wchar_t buf[7];
                swprintf_s(buf, 7, L"\\u%04X", static_cast<unsigned int>(c));
                result += buf;
            }
            else
            {
                result += c;
            }
        }
        return result;
    }

    inline std::optional<JsonObject> from_file(std::wstring_view file_name)
    {
        try
        {
            std::ifstream file(file_name.data(), std::ios::binary);
            if (file.is_open())
            {
                using isbi = std::istreambuf_iterator<char>;
                std::string obj_str{ isbi{ file }, isbi{} };
                // Convert the UTF-8 file contents to UTF-16.
                std::wstring json_wstr{ winrt::to_hstring(obj_str).c_str() };
                // Strip a leading UTF-8/UTF-16 BOM (U+FEFF) if one is present.
                if (!json_wstr.empty() && json_wstr[0] == L'\uFEFF')
                {
                    json_wstr.erase(0, 1);
                }
                // Pre-process: escape any non-ASCII characters (e.g. U+FFFD) before
                // handing the JSON text to Windows.Data.Json, which may not round-trip
                // them correctly when they appear as literal UTF-16 code units.
                return JsonValue::Parse(escape_non_ascii(json_wstr)).GetObjectW();
            }
            return std::nullopt;
        }
        catch (...)
        {
            return std::nullopt;
        }
    }

    inline void to_file(std::wstring_view file_name, const JsonObject& obj)
    {
        // Post-process: escape any non-ASCII characters that Stringify() emitted as
        // literal UTF-16 code units (e.g. U+FFFD) so the file contains only ASCII-safe
        // JSON escape sequences that any conformant parser can round-trip correctly.
        const std::wstring obj_str = escape_non_ascii(std::wstring{ obj.Stringify().c_str() });
        std::ofstream{ file_name.data(), std::ios::binary } << winrt::to_string(obj_str);
    }

    inline bool has(
        const json::JsonObject& o,
        std::wstring_view name,
        const json::JsonValueType type = JsonValueType::Object)
    {
        return o.HasKey(name) && o.GetNamedValue(name).ValueType() == type;
    }

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

    inline JsonValue value(const bool boolean)
    {
        return json::JsonValue::CreateBooleanValue(boolean);
    }

    inline JsonValue value(JsonObject value)
    {
        return value.as<JsonValue>();
    }

    inline JsonValue value(JsonValue value)
    {
        return value; // identity function overload for convenience
    }

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
