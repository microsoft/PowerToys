#include "pch.h"
#include <Windows.h>
#include <common/utils/window.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/ProcessWaiter.h>
#include <common/utils/winapi_error.h>
#include <common/utils/UnhandledExceptionHandler_x64.h>
#include <common/utils/logger_helper.h>

#include "shortcut_guide.h"
#include "target_state.h"
#include "ShortcutGuideConstants.h"
#include "trace.h" 

const std::wstring instanceMutexName = L"Local\\PowerToys_ShortcutGuide_InstanceMutex";

int WINAPI wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ PWSTR lpCmdLine, _In_ int nCmdShow)
{
    winrt::init_apartment();
    LoggerHelpers::init_logger(ShortcutGuideConstants::ModuleKey, L"ShortcutGuide", LogSettings::shortcutGuideLoggerName);
    InitUnhandledExceptionHandler_x64();
    Logger::trace("Starting Shortcut Guide with pid={}", GetCurrentProcessId());

    auto mutex = CreateMutex(nullptr, true, instanceMutexName.c_str());
    if (mutex == nullptr)
    {
        Logger::error(L"Failed to create mutex. {}", get_last_error_or_default(GetLastError()));
    }

    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        Logger::warn(L"Shortcut Guide instance is already running");
        return 0;
    }

    Trace::RegisterProvider();

    std::wstring pid = std::wstring(lpCmdLine);
    if (!pid.empty())
    {
        auto mainThreadId = GetCurrentThreadId();
        ProcessWaiter::OnProcessTerminate(pid, [mainThreadId](int err) {
            if (err != ERROR_SUCCESS)
            {
                Logger::error(L"Failed to wait for parent process exit. {}", get_last_error_or_default(err));
            }
            else
            {
                Logger::trace(L"PowerToys runner exited.");
            }

            Logger::trace(L"Exiting Shortcut Guide");
            PostThreadMessage(mainThreadId, WM_QUIT, 0, 0);
        });

        instance = new OverlayWindow(true);
    }
    else
    {
        instance = new OverlayWindow(false);
    }

    run_message_loop();

    if (instance)
    {
        delete instance;
    }

    return 0;
}
