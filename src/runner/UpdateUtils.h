#pragma once

#include <common/updating/updating.h>

void PeriodicUpdateWorker();
void CheckForUpdatesCallback();

namespace cmdArg
{
    // Starts first stage of the PowerToys auto-update process, which involves copying action runner to a temp path and
    // restarting it from there, so it doesn't interfere with the installation process.
    const inline wchar_t* UPDATE_NOW_LAUNCH_STAGE1 = L"-update_now";
    // Stage 2 consists of starting the installer and optionally launching newly installed PowerToys binary.
    const inline wchar_t* UPDATE_NOW_LAUNCH_STAGE2 = L"-update_now_stage_2";

    const inline wchar_t* UPDATE_REPORT_SUCCESS = L"-report_update_success";
}

SHELLEXECUTEINFOW LaunchPowerToysUpdate(const wchar_t* cmdline);