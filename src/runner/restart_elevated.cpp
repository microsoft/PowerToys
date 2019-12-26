#include "pch.h"
#include "restart_elevated.h"
#include "common/common.h"

enum State
{
    None,
    RestartAsElevated,
    RestartAsNonElevated
};
static State state = None;

void schedule_restart_as_elevated()
{
    state = RestartAsElevated;
}

void schedule_restart_as_non_elevated()
{
    state = RestartAsNonElevated;
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
        return run_elevated(exe_path.get(), {});
    case RestartAsNonElevated:
        return run_non_elevated(exe_path.get(), L"--dont-elevate");
    default:
        return false;
    }
}
