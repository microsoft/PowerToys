#include "pch.h"
#include "json.h"

#include <fstream>

namespace json
{
    std::optional<JsonObject> from_file(std::wstring_view file_name)
    {
        try
        {
            std::ifstream file(file_name.data(), std::ios::binary);
            if (file.is_open())
            {
                using isbi = std::istreambuf_iterator<char>;
                std::string objStr{ isbi{ file }, isbi{} };
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
}
