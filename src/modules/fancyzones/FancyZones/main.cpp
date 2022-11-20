#include "pch.h"

#include <common/utils/ProcessWaiter.h>
#include <common/utils/window.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/gpo.h>

#include <FancyZonesLib/trace.h>
#include <FancyZonesLib/Generated Files/resource.h>

#include <common/utils/logger_helper.h>
#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/utils/resources.h>

#include <FancyZonesLib/FancyZones.h>
#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>
#include <FancyZonesLib/ModuleConstants.h>

#include <FancyZonesApp.h>

// Non-localizable
const std::wstring moduleName = L"FancyZones";
const std::wstring internalPath = L"";
const std::wstring instanceMutexName = L"Local\\PowerToys_FancyZones_InstanceMutex";

int WINAPI wWinMain(_In_ HINSTANCE hInstance, _In_opt_ HINSTANCE hPrevInstance, _In_ PWSTR lpCmdLine, _In_ int nCmdShow)
{
    winrt::init_apartment();
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::fancyZonesLoggerName);

    if (powertoys_gpo::getConfiguredFancyZonesEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
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
        Logger::warn(L"FancyZones instance is already running");
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

            Logger::trace(L"Exiting FancyZones");
            PostThreadMessage(mainThreadId, WM_QUIT, 0, 0);
        });
    }

    Trace::RegisterProvider();

    FancyZonesApp app(GET_RESOURCE_STRING(IDS_FANCYZONES), NonLocalizable::ModuleKey);
    app.Run();

    run_message_loop();

    Trace::UnregisterProvider();
    
    return 0;
}
