#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

SHELLEXECUTEINFOW launch_action_runner(const wchar_t* cmdline);

const inline wchar_t* UPDATE_NOW_LAUNCH_STAGE1_START_PT_CMDARG = L"-update_now_and_start_pt";
const inline wchar_t* UPDATE_NOW_LAUNCH_STAGE1_CMDARG = L"-update_now";

const inline wchar_t* UPDATE_NOW_LAUNCH_STAGE2_CMDARG = L"-update_now_stage_2";
const inline wchar_t* UPDATE_STAGE2_RESTART_PT_CMDARG = L"restart";
const inline wchar_t* UPDATE_STAGE2_DONT_START_PT_CMDARG = L"dont_start";

const inline wchar_t* UPDATE_REPORT_SUCCESS = L"-report_update_success";
