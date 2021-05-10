#include "pch.h"
#include "shortcut_guide.h"

#include <common/SettingsAPI/settings_helpers.h>

#include "trace.h"

// TODO: refactor singleton
OverlayWindow* instance = nullptr;

OverlayWindow::OverlayWindow()
{
    app_name = GET_RESOURCE_STRING(IDS_SHORTCUT_GUIDE);
    app_key = L"Shortcut Guide";
    std::filesystem::path logFilePath(PTSettingsHelper::get_module_save_folder_location(app_key));
    logFilePath.append(LogSettings::shortcutGuideLogPath);
    Logger::init(LogSettings::shortcutGuideLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());
    Logger::info("Overlay Window is creating");
}

// Return the localized display name of the powertoy
const wchar_t* OverlayWindow::get_name()
{
    return app_name.c_str();
}

// Return the non localized key of the powertoy, this will be cached by the runner
const wchar_t* OverlayWindow::get_key()
{
    return app_key.c_str();
}

bool OverlayWindow::get_config(wchar_t* buffer, int* buffer_size)
{
    // Use events to update settings
    return true;
}

void OverlayWindow::set_config(const wchar_t* config)
{
    // Use events to update settings
}

constexpr int alternative_switch_hotkey_id = 0x2;
constexpr UINT alternative_switch_modifier_mask = MOD_WIN | MOD_SHIFT;
constexpr UINT alternative_switch_vk_code = VK_OEM_2;

void OverlayWindow::enable()
{
    Logger::info("Shortcut Guide is enabling");

    if (!_enabled)
    {
        Trace::EnableShortcutGuide(true);

        // todo: start Shortcut Guide process
        _enabled = true;
    }
}

void OverlayWindow::disable(bool trace_event)
{
    Logger::info("Shortcut Guide is disabling");

    if (_enabled)
    {
        _enabled = false;
        if (trace_event)
        {
            Trace::EnableShortcutGuide(false);
        }

        // todo: stop Shortcut guide process
    }
}

void OverlayWindow::disable()
{
    this->disable(true);
}

bool OverlayWindow::is_enabled()
{
    return _enabled;
}

void OverlayWindow::destroy()
{
    this->disable(false);
    delete this;
}
