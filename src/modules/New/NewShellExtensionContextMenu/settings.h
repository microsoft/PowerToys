#pragma once

#include "pch.h"

class NewSettings
{
public:
    NewSettings();

    bool GetEnabled();
    bool GetHideFileExtension() const;
    void SetHideFileExtension(const bool hide_file_extension);
    std::wstring GetTemplateLocation() const;
    void SetTemplateLocation(const std::wstring template_location);

    void Save();
    void Load();

private:
    struct Settings
    {
        bool enabled{ true };
        bool hide_file_extension{ true };
        std::wstring template_location;
    };

    void RefreshEnabledState();
    void InitializeWithDefaultSettings();
    std::wstring GetTemplateLocationDefaultPath();

    void Reload();
    void ParseJson();

    Settings new_settings;
    std::wstring general_settings_json_file_path;
    std::wstring new_settings_json_file_path;
    FILETIME general_settings_last_loaded_timestamp{};
    FILETIME new_settings_last_loaded_timestamp{};
};

NewSettings& NewSettingsInstance();