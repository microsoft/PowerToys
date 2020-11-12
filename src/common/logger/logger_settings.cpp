#include "pch.h"
#include "logger_settings.h"
#include <fstream>
//#include <winrt/base.h>
//#include <winrt/Windows.Foundation.Collections.h>
#include <Windows.h>
//#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Data.Json.h>

using namespace winrt::Windows::Data::Json;

LogSettings::LogSettings()
{
    this->logLevel = LogSettings::defaultLogLevel;
}

std::optional<JsonObject> from_file(std::wstring_view file_name)
{
    try
    {
        std::ifstream file(file_name.data(), std::ios::binary);
        if (file.is_open())
        {
            using isbi = std::istreambuf_iterator<char>;
            std::string obj_str{ isbi{ file }, isbi{} };
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
    std::wstring obj_str{ obj.Stringify().c_str() };
    std::ofstream{ file_name.data(), std::ios::binary } << winrt::to_string(obj_str);
}

JsonObject to_json(LogSettings settings)
{
    JsonObject result;
    result.SetNamedValue(L"logLevel", JsonValue::CreateStringValue(settings.logLevel));

    return result;
}

LogSettings to_settings(JsonObject jobject)
{
    LogSettings result;
    result.logLevel = jobject.GetNamedString(L"logLevel");
    return result;
}

LogSettings getLogSettings(std::wstring_view file_name)
{
    auto jobject = from_file(file_name);
    if (!jobject.has_value())
    {
        auto json = to_json(LogSettings());
        to_file(file_name, json);
        return to_settings(json);
    }

    return to_settings(jobject.value());
}