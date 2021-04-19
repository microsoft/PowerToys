#include "pch.h"
#include <common/utils/window.h>
#include <common/utils/process_waiter.h>
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <keyboardmanager/common/trace.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/KeyboardManagerEngineLibrary/KeyboardManager.h>
#include <common/utils/UnhandledExceptionHandlerX64.h>

using namespace winrt;
using namespace Windows::Foundation;

const std::wstring instanceMutexName = L"Local\\PowerToys_KBMEngine_InstanceMutex";

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PWSTR lpCmdLine, int nCmdShow)
{
    init_apartment();
    LoggerHelpers::init_logger(KeyboardManagerConstants::ModuleName, L"Engine", LogSettings::keyboardManagerLoggerName);
    StackTraceInit();
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

    Trace::RegisterProvider();
    std::wstring pid = std::wstring(lpCmdLine);
    if (!pid.empty())
    {
        auto mainThreadId = GetCurrentThreadId();
        on_process_terminate(pid, [mainThreadId](int err) {
            if (err != ERROR_SUCCESS)
            {
                Logger::error(L"Failed to wait for parent process exit. {}", get_last_error_or_default(err));
            }
            else
            {
                Logger::info(L"PowerToys runner exited.");
            }

            Logger::info(L"Exiting KeyboardManager engine");
            PostThreadMessage(mainThreadId, WM_QUIT, 0, 0);
        });
    }

    auto kbm = KeyboardManager();
    kbm.start_lowlevel_keyboard_hook();
    run_message_loop();
    kbm.stop_lowlevel_keyboard_hook();
    Trace::UnregisterProvider();
    return 0;
}
