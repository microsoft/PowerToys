#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

SHELLEXECUTEINFOW launch_action_runner(const wchar_t* cmdline);

namespace cmdArg
{
    // Starts first stage of the PowerToys auto-update process, which involves copying action runner to a temp path and
    // restarting it from there, so it doesn't interfere with the installation process.
    const inline wchar_t* UPDATE_NOW_LAUNCH_STAGE1 = L"-update_now";
    // Stage 2 consists of starting the installer and optionally launching newly installed PowerToys binary.
    // That's indicated by the following 2 flags.
    const inline wchar_t* UPDATE_NOW_LAUNCH_STAGE2 = L"-update_now_stage_2";
    const inline wchar_t* UPDATE_STAGE2_RESTART_PT = L"restart";
    const inline wchar_t* UPDATE_STAGE2_DONT_START_PT = L"dont_start";

    const inline wchar_t* UNINSTALL_MSI = L"-uninstall_msi";
    const inline wchar_t* RUN_NONELEVATED = L"-run-non-elevated";

    const inline wchar_t* UPDATE_REPORT_SUCCESS = L"-report_update_success";
}
