#include "pch.h"
#include "powertoy_module.h"
#include "lowlevel_keyboard_event.h"
#include <algorithm>

std::unordered_map<std::wstring, PowertoyModule>& modules() {
  static std::unordered_map<std::wstring, PowertoyModule> modules;
  return modules;
}

PowertoyModule load_powertoy(const std::wstring& filename) {
  auto handle = winrt::check_pointer(LoadLibraryW(filename.c_str()));
  auto create = reinterpret_cast<powertoy_create_func>(GetProcAddress(handle, "powertoy_create"));
  if (!create) {
    FreeLibrary(handle);
    winrt::throw_last_error();
  }
  auto module = create();
  if (!module) {
    FreeLibrary(handle);
    winrt::throw_last_error();
  }
  return PowertoyModule(module, handle);
}
