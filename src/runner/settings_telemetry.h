#pragma once
namespace settings_telemetry
{
    static std::wstring send_info_file = L"settings-telemetry.json";
    static std::wstring last_send_option = L"last_send_time";
    void init();
}