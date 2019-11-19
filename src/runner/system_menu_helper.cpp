#include "pch.h"
#include "system_menu_helper.h"

#include <interface/powertoy_module_interface.h>

namespace {
  unsigned int GenerateItemId() {
    static unsigned int generator = 0x70777479;
    return ++generator;
  }
}

SystemMenuHelper& SystemMenuHelperInstace() {
  static SystemMenuHelper instance;
  return instance;
}

void SystemMenuHelper::SetConfiguration(PowertoyModuleIface* module, const std::vector<ItemInfo>& config) {
  Reset(module);
  Configurations[module] = config;
  for (auto& [window, modules] : ProcessedModules) {
    // Unregister module. After system menu is opened again, new configuration will be applied.
    modules.erase(std::remove(std::begin(modules), std::end(modules), module), std::end(modules));
  }
}

void SystemMenuHelper::ProcessSelectedItem(PowertoyModuleIface* module, HWND window, const wchar_t* itemName) {
  for (const auto& item : Configurations[module]) {
    if (itemName == item.name && item.checkBox) {
      // Handle check/uncheck action only if specified by module configuration.
      for (const auto& [id, data] : IdMappings) {
        if (data.second == itemName) {
          if (ToggleItemState(window, id)) {
            return;
          }
        }
      }
      return;
    }
  }
  for (auto it = begin(PendingActions); it != end(PendingActions);) {
    if (it->first == itemName && it->second == window) {
      // This item already exists in PendingActions. Remove it, since it will end
      // up in original state, after two successive actions.
      it = PendingActions.erase(it);
      return;
    }
    else {
      ++it;
    }
  }
  PendingActions[std::wstring(itemName)] = window;
}

bool SystemMenuHelper::Customize(PowertoyModuleIface* module, HWND window) {
  ReEnableCustomItems(window);
  auto& modules = ProcessedModules[window];
  for (const auto& m : modules) {
    if (module == m) {
      return false;
    }
  }
  AddSeparator(module, window);
  for (const auto& info : Configurations[module]) {
    AddItem(module, window, info.name, info.enable);
  }
  modules.push_back(module);
  return true;
}

void SystemMenuHelper::Reset(PowertoyModuleIface* module) {
  for (auto& [window, modules] : ProcessedModules) {
    if (HMENU systemMenu{ GetSystemMenu(window, false) }) {
      for (auto& [id, data] : IdMappings) {
        if (data.first == module) {
          DeleteMenu(systemMenu, id, MF_BYCOMMAND);
        }
      }
    }
    modules.erase(std::remove(std::begin(modules), std::end(modules), module), std::end(modules));
  }
}

bool SystemMenuHelper::HasCustomConfig(PowertoyModuleIface* module) {
  return Configurations.find(module) != Configurations.end();
}

bool SystemMenuHelper::AddItem(PowertoyModuleIface* module, HWND window, const std::wstring& name, const bool enable) {
  if (HMENU systemMenu{ GetSystemMenu(window, false) }) {

    unsigned int id = GenerateItemId();
    if (AppendMenu(systemMenu, MF_STRING | MF_DISABLED | MF_UNCHECKED, id, const_cast<WCHAR*>(name.c_str()))) {
      IdMappings[id] = { module, name };
      if (enable) {
        EnableMenuItem(systemMenu, id, MF_BYCOMMAND | MF_ENABLED);
      }
      ProcessPendingActions(window, name, id);
      return true;
    }
  }
  return false;
}

bool SystemMenuHelper::AddSeparator(PowertoyModuleIface* module, HWND window) {
  if (HMENU systemMenu{ GetSystemMenu(window, false) }) {
    unsigned int id = GenerateItemId();

    if (AppendMenu(systemMenu, MF_SEPARATOR, id, L"separator_dummy_name")) {
      IdMappings[id] = { module, L"separator_dummy_name" };
      return true;
    }
  }
  return false;
}

void SystemMenuHelper::ReEnableCustomItems(HWND window)
{
  // Some apps disables newly added menu items (e.g. Telegram, Hangouts),
  // so re-enable custom menus every time system meny is opened.
  for (const auto& [id, info] : IdMappings) {
    for (const auto& config : Configurations[info.first]) {
      // Enable only if specified by configuration.
      if (config.name == info.second && config.enable) {
        EnableMenuItem(GetSystemMenu(window, false), id, MF_BYCOMMAND | MF_ENABLED);
      }
    }
  }
}

void SystemMenuHelper::ProcessPendingActions(HWND window, const std::wstring& name, const int& id)
{
  for (auto it = begin(PendingActions); it != end(PendingActions);) {
    if (it->first == name && it->second == window) {
      ToggleItemState(window, id);
      it = PendingActions.erase(it);
    }
    else {
      ++it;
    }
  }
}

bool SystemMenuHelper::ToggleItemState(HWND window, const int& id)
{
  HMENU systemMenu = GetSystemMenu(window, false);
  int state = -1;
  if (systemMenu && ((state = GetMenuState(systemMenu, id, MF_BYCOMMAND)) != -1)) {
    state = (state == MF_CHECKED) ? MF_UNCHECKED : MF_CHECKED;
    CheckMenuItem(systemMenu, id, MF_BYCOMMAND | state);
    return true;
  }
  return false;
}

PowertoyModuleIface* SystemMenuHelper::ModuleFromItemId(const int& id) {
  auto it = IdMappings.find(id);
  if (it != IdMappings.end()) {
    return it->second.first;
  }
  return nullptr;
}

const std::wstring SystemMenuHelper::ItemNameFromItemId(const int& id) {
  auto itemIt = IdMappings.find(id);
  if (itemIt != IdMappings.end()) {
    return itemIt->second.second;
  }
  return std::wstring{};
}
