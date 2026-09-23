#include "pch.h"

#include <WorkspacesLib/WorkspaceLaunchSession.h>
#include <WorkspacesLib/utils.h>
#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <WindowArranger.h>

const std::wstring moduleName = L"Workspaces\\WorkspacesWindowArranger";
const std::wstring internalPath = L"";

int APIENTRY WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::workspacesWindowArrangerLoggerName);
    InitUnhandledExceptionHandler();
    if (powertoys_gpo::getConfiguredWorkspacesEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled) return 0;
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    if (FAILED(CoInitializeEx(nullptr, COINIT_MULTITHREADED))) return 1;
    try
    {
        const auto args = split(GetCommandLineW(), L" ");
        // OTS administrators never connect to an arbitrary owner's storage service.
        // Placement is read-only and bound to the actual launcher and this process.
        auto project = Workspaces::LaunchSession::Receive(args.plan);
        WindowArranger arranger(project);
    }
    catch (...)
    {
        Logger::error("Rejected Workspaces placement session");
        CoUninitialize();
        return 1;
    }
    CoUninitialize();
    return 0;
}
