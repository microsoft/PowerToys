#pragma once
#include "powertoys_events.h"
#include "system_menu_helper.h"
#include <interface/powertoy_module_interface.h>
#include <string>
#include <memory>
#include <mutex>
#include <vector>
#include <functional>

#include <cpprest/json.h>

struct PowertoyModuleDeleter {
  void operator()(PowertoyModuleIface* module) const {
    if (module) {
      powertoys_events().unregister_system_menu_action(module);
      powertoys_events().unregister_receiver(module);
      module->destroy();
    }
  }
};

struct PowertoyModuleDLLDeleter {
  using pointer = HMODULE;
  void operator()(HMODULE handle) const {
    FreeLibrary(handle);
  }
};

class PowertoyModule {
public:
  PowertoyModule(PowertoyModuleIface* module, HMODULE  handle);
  inline PowertoyModuleIface * operator->() {
    return module.get();
  }

  web::json::value json_config() const;

private:
  std::unique_ptr<HMODULE, PowertoyModuleDLLDeleter> handle;
  std::unique_ptr<PowertoyModuleIface, PowertoyModuleDeleter> module;
};

PowertoyModule load_powertoy(const std::wstring& filename);
std::unordered_map<std::wstring, PowertoyModule>& modules();
