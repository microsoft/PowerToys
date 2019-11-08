#pragma once

#pragma once

#include <interface/powertoy_system_menu.h>
#include <windows.h>
#include <string>
#include <vector>
#include <unordered_map>

class PowertoyModuleIface;

class SystemMenuHelper : public PowertoySystemMenuIface
{
public:
  // PowertoySystemMenuIface
  virtual void SetConfiguration(PowertoyModuleIface* module, const wchar_t* config) override;
  virtual void RegisterAction  (PowertoyModuleIface* module, HWND window, const wchar_t* name) override;

  bool Customize(PowertoyModuleIface* module, HWND window);
  void Reset    (PowertoyModuleIface* module);

  bool HasCustomConfig(PowertoyModuleIface* module);

  PowertoyModuleIface* ModuleFromItemId(const int& id);
  const std::wstring   ItemNameFromItemId(const int& id);
private:

  /* Data parsed from JSON configuration suplied by module itself. */
  struct ItemInfo {
    std::wstring name{};
    bool         enable{ false };
    bool         check{ false };
  };

  bool AddItem(PowertoyModuleIface* module, HWND window, const std::wstring& name, const bool enable);
  bool AddSeparator(PowertoyModuleIface* module, HWND window);
  void ParseConfiguration(const std::wstring& config, std::vector<ItemInfo>& out);

  /* Store processed windows to avoid handling it multiple times. */
  std::unordered_map<HWND, std::vector<PowertoyModuleIface*>>            ProcessedWindows{};
  /* 
   * Keep mappings form item id to the module who created it and item name for faster
   * processing later.
   */
  std::unordered_map<int, std::pair<PowertoyModuleIface*, std::wstring>> IdMappings{};
  /* 
   * Store configurations provided by module. This will be used to create custom
   * system menu items and to handle updates.
   */
  std::unordered_map<PowertoyModuleIface*, std::vector<ItemInfo>>        Configurations{};
};

SystemMenuHelper& SystemMenuHelperInstace();
