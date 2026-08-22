#include "pch.h"
#include "restart_elevated.h"

#include <common/utils/elevation.h>

enum State
{
    None,
    RestartAsElevated,
    RestartAsElevatedOpenSettings,
    RestartAsNonElevated,
    RestartAsNonElevatedOpenSettings
};
static State state = None;

void schedule_restart_as_elevated(bool openSettings)
{
    state = openSettings ? RestartAsElevatedOpenSettings : RestartAsElevated;
}

void schedule_restart_as_non_elevated()
{
    state = RestartAsNonElevated;
}

void schedule_restart_as_non_elevated(bool openSettings)
{
    state = openSettings ? RestartAsNonElevatedOpenSettings : RestartAsNonElevated;
}

bool is_restart_scheduled()
{
    return state != None;
}

bool restart_if_scheduled()
{
    // Make sure we have enough room, even for the long (\\?\) paths
    constexpr DWORD exe_path_size = 0xFFFF;
    auto exe_path = std::make_unique<wchar_t[]>(exe_path_size);
    GetModuleFileNameW(nullptr, exe_path.get(), exe_path_size);
    switch (state)
    {
    case RestartAsElevated:
        return run_elevated(exe_path.get(), L"--restartedElevated");
    case RestartAsElevatedOpenSettings:
        return run_elevated(exe_path.get(), L"--restartedElevated --open-settings");
    case RestartAsNonElevatedOpenSettings:
        return run_non_elevated(exe_path.get(), L"--open-settings", NULL);
    case RestartAsNonElevated:
        return run_non_elevated(exe_path.get(), L"", NULL);
    default:
        return false;
    }
}

bool restart_same_elevation()
{
    constexpr DWORD exe_path_size = 0xFFFF;
    auto exe_path = std::make_unique<wchar_t[]>(exe_path_size);
    GetModuleFileNameW(nullptr, exe_path.get(), exe_path_size);
    return run_same_elevation(exe_path.get(), L"", nullptr);
}
