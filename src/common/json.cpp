#include "pch.h"
#include "json.h"

#include <fstream>

namespace json
{
    std::optional<JsonObject> from_file(std::wstring_view file_name)
    {
        try
        {
            std::wifstream file(file_name.data(), std::ios::binary);
            using isbi = std::istreambuf_iterator<wchar_t>;
            return JsonValue::Parse(std::wstring{ isbi{ file }, isbi{} }).GetObjectW();
        }
        catch (...)
        {
            return std::nullopt;
        }
    }

    void to_file(std::wstring_view file_name, const JsonObject& obj)
    {
        std::wofstream{ file_name.data(), std::ios::binary } << obj.Stringify().c_str();
    }
}
