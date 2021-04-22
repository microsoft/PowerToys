#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

SHELLEXECUTEINFOW launch_action_runner(const wchar_t* cmdline);

namespace cmdArg
{
    const inline wchar_t* UPDATE_NOW_LAUNCH_STAGE1 = L"-update_now";

    const inline wchar_t* UPDATE_NOW_LAUNCH_STAGE2 = L"-update_now_stage_2";
    const inline wchar_t* UPDATE_STAGE2_RESTART_PT = L"restart";
    const inline wchar_t* UPDATE_STAGE2_DONT_START_PT = L"dont_start";

    const inline wchar_t* UNINSTALL_MSI = L"-uninstall_msi";
    const inline wchar_t* RUN_NONELEVATED = L"-run-non-elevated";

    const inline wchar_t* UPDATE_REPORT_SUCCESS = L"-report_update_success";
}
