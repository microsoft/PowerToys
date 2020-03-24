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
                file.seekg(0, file.end);
                auto length = file.tellg();
                file.seekg(0, file.beg);
                std::string obj_str;
                obj_str.resize(length);
                file.read(obj_str.data(), length);
                file.close();
                return JsonValue::Parse(winrt::to_hstring(obj_str)).GetObjectW();
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
        std::ofstream file{ file_name.data(), std::ios::binary };

        if(file.is_open())
        {
            std::string obj_str{winrt::to_string(obj.Stringify())};
            file.write(obj_str.c_str(), obj_str.size());
            file.close();
        }
    }
}
