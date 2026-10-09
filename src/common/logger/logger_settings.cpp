#include "pch.h"
#include "logger_settings.h"
#include <fstream>
#include <Windows.h>
#include <winrt/Windows.Data.Json.h>
#include <iostream>

using namespace winrt::Windows::Data::Json;

LogSettings::LogSettings()
{
    logLevel = defaultLogLevel;
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
    try
    {
        std::ofstream{ file_name.data(), std::ios::binary } << winrt::to_string(obj_str);
    }
    catch (...)
    {
    }
}

JsonObject to_json(LogSettings settings)
{
    JsonObject result;
    result.SetNamedValue(LogSettings::logLevelOption, JsonValue::CreateStringValue(settings.logLevel));

    return result;
}

LogSettings to_settings(JsonObject jobject)
{
    LogSettings result;
    try
    {
        result.logLevel = jobject.GetNamedString(LogSettings::logLevelOption);
    }
    catch (...)
    {
        result.logLevel = LogSettings::defaultLogLevel;
    }
    
    return result;
}

// Get log settings from file. File with default options is created if it does not exist
LogSettings get_log_settings(std::wstring_view file_name)
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
