#pragma once
#include <optional>
#include <string>

enum class ESettingsWindowNames
{
    Dashboard = 0,
    Overview,
    Awake,
    ColorPicker,
    FancyZones,
    Run,
    ImageResizer,
    KBM,
    MouseUtils,
    PowerRename,
    FileExplorer,
    ShortcutGuide,
    VideoConference,
    Hosts,
    MeasureTool,
    PowerOCR,
    Workspaces,
    RegistryPreview,
    CropAndLock,
    EnvironmentVariables,
    AdvancedPaste,
    NewPlus,
    ZoomIt,
};

std::string ESettingsWindowNames_to_string(ESettingsWindowNames value);
ESettingsWindowNames ESettingsWindowNames_from_string(std::string value);

void open_settings_window(std::optional<std::wstring> settings_window, bool show_flyout, const std::optional<POINT>& flyout_position);
void close_settings_window();

void open_oobe_window();
void open_scoobe_window();
void open_flyout();
