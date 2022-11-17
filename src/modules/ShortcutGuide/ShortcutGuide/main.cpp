#include "pch.h"
#include <Windows.h>
#include <common/utils/window.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/ProcessWaiter.h>
#include <common/utils/winapi_error.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/logger_helper.h>
#include <common/utils/EventWaiter.h>
#include <common/utils/gpo.h>

#include "shortcut_guide.h"
#include "target_state.h"
#include "ShortcutGuideConstants.h"
#include "trace.h"

const std::wstring instanceMutexName = L"Local\\PowerToys_ShortcutGuide_InstanceMutex";

// set current path to the executable path
bool SetCurrentPath()
{
    TCHAR buffer[MAX_PATH] = { 0 };
    if (!GetModuleFileName(NULL, buffer, MAX_PATH))
    {
        Logger::error(L"Failed to get module path. {}", get_last_error_or_default(GetLastError()));
        return false;
    }

    if (!PathRemoveFileSpec(buffer))
    {
        Logger::error(L"Failed to remove file from module path. {}", get_last_error_or_default(GetLastError()));
        return false;
    }

    std::error_code err;
    std::filesystem::current_path(buffer, err);
    if (err.value())
    {
        Logger::error("Failed to set current path. {}", err.message());
        return false;
    }

    return true;
}

int WINAPI wWinMain(_In_ HINSTANCE /*hInstance*/, _In_opt_ HINSTANCE /*hPrevInstance*/, _In_ PWSTR lpCmdLine, _In_ int /*nCmdShow*/)
{
    winrt::init_apartment();
    LoggerHelpers::init_logger(ShortcutGuideConstants::ModuleKey, L"ShortcutGuide", LogSettings::shortcutGuideLoggerName);

    if (powertoys_gpo::getConfiguredShortcutGuideEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        Logger::warn(L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        return 0;
    }

    InitUnhandledExceptionHandler();
    Logger::trace("Starting Shortcut Guide");

    if (!SetCurrentPath())
    {
        return false;
    }

    Trace::RegisterProvider();
    if (std::wstring(lpCmdLine).find(L' ') != std::wstring::npos)
    {
        Logger::trace("Sending settings telemetry");
        auto settings = OverlayWindow::GetSettings();
        Trace::SendSettings(settings);
        Trace::UnregisterProvider();
        return 0;
    }

    auto mutex = CreateMutex(nullptr, true, instanceMutexName.c_str());
    if (mutex == nullptr)
    {
        Logger::error(L"Failed to create mutex. {}", get_last_error_or_default(GetLastError()));
    }

    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        Logger::warn(L"Shortcut Guide instance is already running");
        Trace::UnregisterProvider();
        return 0;
    }

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
    }

    auto hwnd = GetForegroundWindow();
    auto window = OverlayWindow(hwnd);
    EventWaiter exitEventWaiter;
    if (window.IsDisabled())
    {
        Logger::trace("SG is disabled for the current foreground app. Exiting SG");
        Trace::UnregisterProvider();
        return 0;
    }
    else
    {
        auto mainThreadId = GetCurrentThreadId();
        exitEventWaiter = EventWaiter(CommonSharedConstants::SHORTCUT_GUIDE_EXIT_EVENT, [mainThreadId, &window](int err) {
            if (err != ERROR_SUCCESS)
            {
                Logger::error(L"Failed to wait for {} event. {}", CommonSharedConstants::SHORTCUT_GUIDE_EXIT_EVENT, get_last_error_or_default(err));
            }
            else
            {
                Logger::trace(L"{} event was signaled", CommonSharedConstants::SHORTCUT_GUIDE_EXIT_EVENT);
            }

            window.CloseWindow(HideWindowType::THE_SHORTCUT_PRESSED, mainThreadId);
        });
    }

    window.ShowWindow();
    run_message_loop();
    Trace::UnregisterProvider();
    return 0;
}
