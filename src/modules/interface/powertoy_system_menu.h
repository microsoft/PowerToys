#pragma once

#include <string>
#include <vector>

class PowertoyModuleIface;

class PowertoySystemMenuIface {
public:
  struct ItemInfo {
    std::wstring name{};
    bool enable{ false };
    bool checkBox{ false };
  };
  /* 
   * Set configuration of system menu items for specific powertoy module. Configuration
   * parameters include item name (and hotkey), item status at creation (enabled/disabled)
   * and whether check box will appear next to item name when action is taken.
   */
  virtual void SetConfiguration(PowertoyModuleIface* module, const std::vector<ItemInfo>& config) = 0;
  /* Process action on specific system menu item. */
  virtual void ProcessSelectedItem(PowertoyModuleIface* module, HWND window, const wchar_t* itemName) = 0;
};
