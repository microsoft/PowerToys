#pragma once

#include <string>

class PowertoyModuleIface;

class PowertoySystemMenuIface
{
public:
  /* 
   * Set configuration of system menu items for specific powertoy module. Configuration
   * parameters such as item name (and hotkey), item status at creation (enabled/disabled)
   * and whether check box will appear next to item name when action is taken, are passed
   * as JSON formatted string.
   */
  virtual void SetConfiguration(PowertoyModuleIface* module, const wchar_t* config) = 0;
  /* Register action on specific system menu item. */
  virtual void RegisterAction(PowertoyModuleIface* module, HWND window, const wchar_t* name) = 0;
};