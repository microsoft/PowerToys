#include "pch.h"

#include <common/utils/logger_helper.h>
#include <common/utils/ProcessWaiter.h>
#include <common/utils/window.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/gpo.h>

#include <common/Telemetry/EtwTrace/EtwTrace.h>

#include "DockWindow.h"
#include "trace.h"

const std::wstring moduleName = L"Dock";
const std::wstring internalPath = L"";
const std::wstring instanceMutexName = L"Local\\PowerToys_Dock_InstanceMutex";

int WINAPI wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ PWSTR lpCmdLine, _In_ int nCmdShow)
{
    Shared::Trace::ETWTrace trace;
    trace.UpdateState(true);

    winrt::init_apartment();
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::dockLoggerName);

    if (powertoys_gpo::getConfiguredDockEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        Logger::warn(L"Tried to start with a GPO policy setting the utility to always be disabled.");
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
        return 0;
    }

    auto mainThreadId = GetCurrentThreadId();

    std::wstring pid = std::wstring(lpCmdLine);
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

            Logger::trace(L"Exiting Dock");
            PostThreadMessage(mainThreadId, WM_QUIT, 0, 0);
        });
    }

    Trace::Dock::RegisterProvider();

    DockWindow dock;
    if (!dock.Create(hInstance))
    {
        Logger::error(L"DockWindow: Failed to create dock window");
        return 1;
    }

    int result = dock.Run();

    Trace::Dock::UnregisterProvider();
    trace.Flush();

    return result;
}
