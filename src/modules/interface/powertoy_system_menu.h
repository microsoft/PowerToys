#pragma once

#include <string>

class PowertoyModuleIface;

class PowertoySystemMenuIface
{
public:
  struct ItemInfo {
    std::wstring name{};
    bool enable{ false };
    bool checkBox{ false };
  };
  /* 
   * Set configuration of system menu items for specific powertoy module. Configuration
   * parameters such as item name (and hotkey), item status at creation (enabled/disabled)
   * and whether check box will appear next to item name when action is taken, are passed
   * as JSON formatted string.
   */
  virtual void SetConfiguration(PowertoyModuleIface* module, std::vector<ItemInfo>& config) = 0;
  // Process action on specific system menu item.
  virtual void ProcessSelectedItem(PowertoyModuleIface* module, HWND window, const wchar_t* itemName) = 0;
};