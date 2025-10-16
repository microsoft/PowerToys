#include "pch.h"
#include "json.h"

namespace json
{
    std::optional<JsonObject> from_file(std::wstring_view file_name)
    {
        try
        {
            std::ifstream file(file_name.data(), std::ios::binary);
            if (file.is_open())
            {
                using iterator = std::istreambuf_iterator<char>;
                std::string objStr{ iterator{ file }, iterator{} };
                return JsonValue::Parse(winrt::to_hstring(objStr)).GetObjectW();
            }
            return std::nullopt;
        }
        catch (...)
        {
            return std::nullopt;
        }
    }

    void to_file(std::wstring_view file_name, const JsonObject& obj)
    {
        std::wstring objStr{ obj.Stringify().c_str() };
        std::ofstream{ file_name.data(), std::ios::binary } << winrt::to_string(objStr);
    }

    bool has(const json::JsonObject& o, std::wstring_view name, const json::JsonValueType type)
    {
        return o.HasKey(name) && o.GetNamedValue(name).ValueType() == type;
    }

    JsonValue value(const bool boolean)
    {
        return json::JsonValue::CreateBooleanValue(boolean);
    }

    JsonValue value(JsonObject valueObject)
    {
        return valueObject.as<JsonValue>();
    }

    JsonValue value(JsonValue valueObject)
    {
        return valueObject; // identity function overload for convenience
    }
}
