#include "pch.h"
#include <common/utils/window.h>
#include <common/utils/process_waiter.h>
#include<keyboardmanager/KeyboardManagerEngineLibrary/KeyboardManager.h>

using namespace winrt;
using namespace Windows::Foundation;

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PWSTR lpCmdLine, int nCmdShow)
{
    init_apartment();

    std::filesystem::path logFilePath(PTSettingsHelper::get_module_save_folder_location(KeyboardManagerConstants::ModuleName));
    logFilePath.append(LogSettings::keyboardManagerEngineLogPath);
    Logger::init(LogSettings::keyboardManagerLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

    std::wstring pid = std::wstring(lpCmdLine);
    if (!pid.empty())
    {
        on_process_terminate(pid, [](int err) {
            if (err != ERROR_SUCCESS)
            {
                Logger::error(L"Failed to wait for parent process exit. {}", get_last_error_or_default(err));
            }
            else
            {
                Logger::info("PowerToys runner exited.");
            }

            Logger::info("Exiting KeyboardManager engine");
            ExitProcess(0);
        });
    }

    auto kbm = KeyboardManager();
    kbm.start_lowlevel_keyboard_hook();
    run_message_loop();
    kbm.stop_lowlevel_keyboard_hook();
}
