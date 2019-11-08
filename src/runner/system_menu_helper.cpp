#include "pch.h"
#include "system_menu_helper.h"

#include <interface/powertoy_module_interface.h>

namespace {
  constexpr int KSeparatorPos = 1;
  constexpr int KNewItemPos = 2;

  int GenerateItemId() {
    static int generator = 0xDEADBEEF;
    return ++generator;
  }
}

SystemMenuHelper& SystemMenuHelperInstace()
{
  static SystemMenuHelper instance;
  return instance;
}

void SystemMenuHelper::SetConfiguration(PowertoyModuleIface* module, const wchar_t* config)
{
  Reset(module);
  std::vector<ItemInfo> info;
  ParseConfiguration(std::wstring(config), info);
  Configurations[module] = info;
  for (auto& [window, modules] : ProcessedWindows) {
    // Unregister module. After system menu is openned again, new configuration will be applied.
    modules.erase(std::remove(std::begin(modules), std::end(modules), module), std::end(modules));
  }
}

void SystemMenuHelper::RegisterAction(PowertoyModuleIface* module, HWND window, const wchar_t* name)
{
  for (auto& item : Configurations[module]) {
    if (!wcscmp(name, item.name.c_str()) && item.check) {
      // Handle check/uncheck action only if specified by module configuration.
      for (auto& [id, data] : IdMappings) {
        if (data.second == name) {
          HMENU systemMenu = GetSystemMenu(window, false);
          int state = (GetMenuState(systemMenu, id, MF_BYCOMMAND) == MF_CHECKED) ? MF_UNCHECKED : MF_CHECKED;
          CheckMenuItem(systemMenu, id, MF_BYCOMMAND | state);
          break;
        }
      }
      break;
    }
  }
}

bool SystemMenuHelper::Customize(PowertoyModuleIface* module, HWND window)
{
  for (const auto& m : ProcessedWindows[window]) {
    if (module == m) {
      return false;
    }
  }
  AddSeparator(module, window);
  for (const auto& info : Configurations[module]) {
    AddItem(module, window, info.name, info.enable);
  }
  ProcessedWindows[window].push_back(module);
  return true;
}

void SystemMenuHelper::Reset(PowertoyModuleIface* module)
{
  for (auto& [window, modules] : ProcessedWindows) {
    for (auto& [id, data] : IdMappings) {
      HMENU sysMenu{ nullptr };
      if (data.first == module && (sysMenu = GetSystemMenu(window, false))) {
        DeleteMenu(GetSystemMenu(window, false), id, MF_BYCOMMAND);
      }
    }
  }
}

bool SystemMenuHelper::HasCustomConfig(PowertoyModuleIface* module)
{
  return (Configurations.find(module) != Configurations.end());
}

bool SystemMenuHelper::AddItem(PowertoyModuleIface* module, HWND window, const std::wstring& name, const bool enable)
{
  if (HMENU systemMenu{ GetSystemMenu(window, false) }) {
    MENUITEMINFO item;
    item.cbSize     = sizeof(item);
    item.fMask      = MIIM_ID | MIIM_STRING | MIIM_STATE;
    item.fState     = MF_UNCHECKED | MF_DISABLED; // Item is disabled by default.
    item.wID        = GenerateItemId();
    item.dwTypeData = const_cast<WCHAR*>(name.c_str());
    item.cch        = name.size() + 1;

    if (InsertMenuItem(systemMenu, GetMenuItemCount(systemMenu) - KNewItemPos, true, &item)) {
      IdMappings[item.wID] = { module, name };
      if (enable) {
        EnableMenuItem(systemMenu, item.wID, MF_BYCOMMAND | MF_ENABLED);
      }
      return true;
    }
  }
  return false;
}

bool SystemMenuHelper::AddSeparator(PowertoyModuleIface* module, HWND window)
{
  if (HMENU systemMenu{ GetSystemMenu(window, false) }) {
    MENUITEMINFO separator;
    separator.cbSize = sizeof(separator);
    separator.fMask  = MIIM_ID | MIIM_FTYPE;
    separator.fType  = MFT_SEPARATOR;
    separator.wID    = GenerateItemId();

    if (InsertMenuItem(systemMenu, GetMenuItemCount(systemMenu) - KSeparatorPos, true, &separator)) {
      IdMappings[separator.wID] = { module, L"sepparator_dummy_name" };
      return true;
    }
  }
  return false;
}

void SystemMenuHelper::ParseConfiguration(const std::wstring& config, std::vector<SystemMenuHelper::ItemInfo>& out)
{
  using namespace web::json;
  if (!config.empty()) {
    value json_config = value::parse(config);
    array json_array = json_config.at(U("custom_items")).as_array();
    for (auto item : json_array) {
      ItemInfo info{};
      info.name   = item[L"name"].as_string();
      info.enable = item[L"enable"].as_bool();
      info.check  = item[L"check"].as_bool();
      out.push_back(info);
    }
  }
}

PowertoyModuleIface* SystemMenuHelper::ModuleFromItemId(const int& id)
{
  auto it = IdMappings.find(id);
  if (it != IdMappings.end()) {
    return it->second.first;
  }
  return nullptr;
}

const std::wstring SystemMenuHelper::ItemNameFromItemId(const int& id)
{
  auto itemIt = IdMappings.find(id);
  if (itemIt != IdMappings.end()) {
    return itemIt->second.second;
  }
  return std::wstring{};
}
