#include "pch.h"
#include <Windows.h>
#include <common/utils/window.h>
#include <common/SettingsAPI/settings_helpers.h>

#include "shortcut_guide.h"
#include "target_state.h"
#include "ShortcutGuideConstants.h"

int WINAPI wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ PWSTR lpCmdLine, _In_ int nCmdShow)
{
    winrt::init_apartment();

    std::filesystem::path logFilePath(PTSettingsHelper::get_module_save_folder_location(ShortcutGuideConstants::ModuleKey));
    logFilePath.append(LogSettings::shortcutGuideLogPath);
    Logger::init(LogSettings::shortcutGuideLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

    instance = new OverlayWindow();
    instance->enable();

    run_message_loop();
    instance->disable();

    if (instance)
    {
        delete instance;
    }
}
