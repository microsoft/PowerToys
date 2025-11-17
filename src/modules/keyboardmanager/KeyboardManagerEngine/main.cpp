#include "pch.h"
#include <common/Telemetry/EtwTrace/EtwTrace.h>
#include <common/utils/EventWaiter.h>
#include <common/utils/window.h>
#include <common/utils/ProcessWaiter.h>
#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/gpo.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/KeyboardManagerEngineLibrary/KeyboardManager.h>
#include <keyboardmanager/KeyboardManagerEngineLibrary/trace.h>
#include <common/interop/shared_constants.h>

const std::wstring instanceMutexName = L"Local\\PowerToys_KBMEngine_InstanceMutex";

int WINAPI wWinMain(_In_ HINSTANCE /*hInstance*/,
                    _In_opt_ HINSTANCE /*hPrevInstance*/,
                    _In_ PWSTR lpCmdLine,
                    _In_ int /*nCmdShow*/)
{
    winrt::init_apartment();
    LoggerHelpers::init_logger(KeyboardManagerConstants::ModuleName, L"Engine", LogSettings::keyboardManagerLoggerName);

    Shared::Trace::ETWTrace trace;
    trace.UpdateState(true);

    if (powertoys_gpo::getConfiguredKeyboardManagerEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        Logger::warn(L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        return 0;
    }

    InitUnhandledExceptionHandler();

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

    auto mainThreadId = GetCurrentThreadId();

    EventWaiter ev = EventWaiter(CommonSharedConstants::TERMINATE_KBM_SHARED_EVENT, [&](int) {
        PostThreadMessage(mainThreadId, WM_QUIT, 0, 0);
    });

    if (!pid.empty())
    {
        ProcessWaiter::OnProcessTerminate(pid, [mainThreadId](int err) {
            if (err != ERROR_SUCCESS)
            {
                Logger::error(L"Failed to wait for parent process exit. {}", get_last_error_or_default(err));
            }
            else
            {
                Logger::trace(L"PowerToys runner exited.");
            }

            Logger::trace(L"Exiting KeyboardManager engine");
            PostThreadMessage(mainThreadId, WM_QUIT, 0, 0);
        });
    }

    auto kbm = KeyboardManager();
    if (kbm.HasRegisteredRemappings())
        kbm.StartLowlevelKeyboardHook();

    auto StartHookFunc = [&kbm]() {
        kbm.StartLowlevelKeyboardHook();
    };

    run_message_loop({}, {}, { { KeyboardManager::StartHookMessageID, StartHookFunc } });

    kbm.StopLowlevelKeyboardHook();
    Trace::UnregisterProvider();

    trace.Flush();

    return 0;
}
