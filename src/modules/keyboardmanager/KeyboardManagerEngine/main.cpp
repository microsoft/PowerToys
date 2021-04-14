#include "pch.h"
#include <common/utils/window.h>
#include <common/utils/process_waiter.h>
#include<keyboardmanager/KeyboardManagerEngineLibrary/KeyboardManager.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <common/utils/winapi_error.h>

using namespace winrt;
using namespace Windows::Foundation;

const std::wstring instanceMutexName = L"Local\\PowerToys_KBMEngine_InstanceMutex";

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PWSTR lpCmdLine, int nCmdShow)
{
    init_apartment();

    std::filesystem::path logFilePath(PTSettingsHelper::get_module_save_folder_location(KeyboardManagerConstants::ModuleName));
    logFilePath.append(LogSettings::keyboardManagerEngineLogPath);
    Logger::init(LogSettings::keyboardManagerLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());
    auto mutex = CreateMutex(nullptr, true, instanceMutexName.c_str());
    if (mutex == nullptr)
    {
        Logger::error(L"Failed to create mutex. {}", get_last_error_or_default(GetLastError()));
    }

    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        Logger::warn(L"KBM engine instance is already running");
        return 0;
    }

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
                Logger::info(L"PowerToys runner exited.");
            }

            Logger::info(L"Exiting KeyboardManager engine");
            ExitProcess(0);
        });
    }

    auto kbm = KeyboardManager();
    kbm.start_lowlevel_keyboard_hook();
    run_message_loop();
    kbm.stop_lowlevel_keyboard_hook();
    return 0;
}
