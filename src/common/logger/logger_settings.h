#pragma once
#include <string>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <Windows.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Data.Json.h>
// #include <common/json.h>

struct LogSettings
{
    std::string logLevel;
    winrt::Windows::Data::Json::JsonObject obj;
};