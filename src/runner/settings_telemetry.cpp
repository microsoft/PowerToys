#pragma once
#include "pch.h"
#include "settings_telemetry.h"
#include <Windows.h>
#include <thread>
#include <common/logger/logger.h>
#include <common/utils/timeutil.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <filesystem>
#include "powertoy_module.h"

using JsonObject = winrt::Windows::Data::Json::JsonObject;
using JsonValue = winrt::Windows::Data::Json::JsonValue;

std::wstring get_info_file_path()
{
    std::filesystem::path settingsFilePath(PTSettingsHelper::get_root_save_folder_location());
    settingsFilePath = settingsFilePath.append(settings_telemetry::send_info_file);
    return settingsFilePath.wstring();
}

std::optional<time_t> get_last_send_time()
{
    auto settings = json::from_file(get_info_file_path());
    if (!settings.has_value() || !settings.value().HasKey(settings_telemetry::last_send_option))
    {
        return std::nullopt;
    }

    auto stringTime = (std::wstring)settings.value().GetNamedString(settings_telemetry::last_send_option);
    return timeutil::from_string(stringTime);
}

void update_last_send_time(time_t time)
{
    auto settings = JsonObject();
    settings.SetNamedValue(settings_telemetry::last_send_option, JsonValue::CreateStringValue(timeutil::to_string(time)));
    json::to_file(get_info_file_path(), settings);
}

void send()
{
    for (auto& [name, powertoy] : modules())
    {
        if (powertoy->is_enabled())
        {
            try
            {
                powertoy->send_settings_telemetry();
            }
            catch (...)
            {
                Logger::error(L"Failed to send telemetry for {} module", name);
            }
        }
    }
}

void run_interval()
{
    auto time = get_last_send_time();
    long long wait_time = 24 * 3600;
    long long left_to_wait = 0;
    if (time.has_value())
    {
        left_to_wait = max(0, wait_time - timeutil::diff::in_seconds(timeutil::now(), time.value()));
    }

    Sleep(static_cast<DWORD>(left_to_wait * 1000));
    send();
    update_last_send_time(timeutil::now());

    while (true)
    {
        Sleep(static_cast<DWORD>(wait_time * 1000));
        send();
        update_last_send_time(timeutil::now());
    }
}

void settings_telemetry::init()
{
    std::thread([]() {
        try
        {
            run_interval();
        }
        catch (...)
        {
            Logger::error("Failed to send settings telemetry");
        }
    }).detach();
}
