#pragma once

#include <interface/powertoy_system_menu.h>
#include <windows.h>
#include <string>
#include <vector>
#include <unordered_map>

class PowertoyModuleIface;

class SystemMenuHelper : public PowertoySystemMenuIface {
public:
  // PowertoySystemMenuIface
  virtual void SetConfiguration(PowertoyModuleIface* module, const std::vector<ItemInfo>& config) override;
  virtual void ProcessSelectedItem(PowertoyModuleIface* module, HWND window, const wchar_t* itemName) override;

  bool Customize(PowertoyModuleIface* module, HWND window);
  void Reset(PowertoyModuleIface* module);

  bool HasCustomConfig(PowertoyModuleIface* module);

  PowertoyModuleIface* ModuleFromItemId(const int& id);
  const std::wstring ItemNameFromItemId(const int& id);

private:
  bool AddItem(PowertoyModuleIface* module, HWND window, const std::wstring& name, const bool enable);
  bool AddSeparator(PowertoyModuleIface* module, HWND window);

  void ReEnableCustomItems(HWND window);
  void ProcessPendingActions(HWND window, const std::wstring& name, const int& id);
  bool ToggleItemState(HWND window, const int& id);

  // Store processed modules per window to avoid handling it multiple times.
  std::unordered_map<HWND, std::vector<PowertoyModuleIface*>> ProcessedModules{};

  // Keep mappings form item id to the module who created it and item name for faster processing later.
  std::unordered_map<int, std::pair<PowertoyModuleIface*, std::wstring>> IdMappings{};

  // Store configurations provided by module.
  // This will be used to create custom system menu items and to handle updates.
  std::unordered_map<PowertoyModuleIface*, std::vector<ItemInfo>> Configurations{};

  // Keep track of pending actions (check/uncheck) on menu items, so they can be applied
  // when menu items is actually created.
  std::unordered_map<std::wstring, HWND> PendingActions{};
};

SystemMenuHelper& SystemMenuHelperInstace();
