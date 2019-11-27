#include "pch.h"
#include "restart_elevated.h"
#include "common/common.h"

enum State {
  None,
  RestartAsElevated,
  RestartAsNonElevated
};
static State state = None;

void schedule_restart_as_elevated() {
  state = RestartAsElevated;
}

void schedule_restart_as_non_elevated() {
  state = RestartAsNonElevated;
}

bool is_restart_scheduled() {
  return state != None;
}

bool restart_if_scheduled() {
  std::array<wchar_t, 0xFFFF> exe_path;
  GetModuleFileNameW(nullptr, exe_path.data(), exe_path.size());
  switch (state) {
  case RestartAsElevated:
    return run_elevated(exe_path.data(), {});
  case RestartAsNonElevated:
    return run_non_elevated(exe_path.data(), L"--dont-elevate");
  default:
    return false;
  }
}
