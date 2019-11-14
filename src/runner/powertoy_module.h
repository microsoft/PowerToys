#pragma once
#include "powertoys_events.h"
#include "system_menu_helper.h"
#include <interface/powertoy_module_interface.h>
#include <string>
#include <memory>
#include <mutex>
#include <vector>
#include <functional>

class PowertoyModule;

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
  void operator()(HMODULE  handle) const {
    FreeLibrary(handle);
  }
};

class PowertoyModule {
public:
  PowertoyModule(PowertoyModuleIface* module, HMODULE  handle) : handle(handle), module(module) {
    if (!module) {
      throw std::runtime_error("Module not initialized");
    }
    name = module->get_name();
    auto want_signals = module->get_events();
    if (want_signals) {
      for (; *want_signals; ++want_signals) {
        powertoys_events().register_receiver(*want_signals, module);
      }
    }
  }

  const std::wstring& get_name() const {
    return name;
  }
  
  const std::wstring get_config() const {
    std::wstring result;
    int size = 0;
    module->get_config(nullptr, &size);
    wchar_t *buffer = new wchar_t[size];
    if (module->get_config(buffer, &size)) {
      result.assign(buffer);
    }
    delete[] buffer;
    return result;
  }

  void set_config(const std::wstring& config) {
    module->set_config(config.c_str());
  }
  
  void call_custom_action(const std::wstring& action) {
    module->call_custom_action(action.c_str());
  }
  
  intptr_t signal_event(const std::wstring& signal_event, intptr_t data) {
    return module->signal_event(signal_event.c_str(), data);
  }

  bool is_enabled() {
    return module->is_enabled();
  }
  
  void enable() {
    if (SystemMenuHelperInstace().HasCustomConfig(module.get())) {
      powertoys_events().register_system_menu_action(module.get());
    }
    module->enable();
  }
  
  void disable() {
    powertoys_events().unregister_system_menu_action(module.get());
    module->disable();
  }

private:
  std::unique_ptr<HMODULE, PowertoyModuleDLLDeleter> handle;
  std::unique_ptr<PowertoyModuleIface, PowertoyModuleDeleter> module;
  std::wstring name;
};

PowertoyModule load_powertoy(const std::wstring& filename);
std::unordered_map<std::wstring, PowertoyModule>& modules();
