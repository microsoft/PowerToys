#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

SHELLEXECUTEINFOW launch_action_runner(const wchar_t* cmdline);