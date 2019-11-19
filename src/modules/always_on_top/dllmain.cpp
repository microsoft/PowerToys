#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <interface/powertoy_system_menu.h>
#include <common/settings_objects.h>
#include <set>
#include "trace.h"
#include "resource.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD  ul_reason_for_call, LPVOID lpReserved) {
  switch (ul_reason_for_call) {
  case DLL_PROCESS_ATTACH:
    Trace::RegisterProvider();
    break;
  case DLL_THREAD_ATTACH:
  case DLL_THREAD_DETACH:
    break;
  case DLL_PROCESS_DETACH:
    Trace::UnregisterProvider();
    break;
  }
  return TRUE;
}

const static wchar_t* MODULE_NAME = L"Always On Top";
const static wchar_t* MODULE_DESC = L"Adds a command to the title bar context menu to set a window on top of all others.";
const static wchar_t* HOTKEY_NAME = L"AlwaysOnTop_HotKey";
const static wchar_t* HOTKEY_WINDOW_CLASS_NAME = L"HotkeyHandleWindowClass";
const static wchar_t* OVERVIEW_LINK = L"https://github.com/microsoft/PowerToys/blob/master/src/modules/always_on_top/README.md";

struct AlwaysOnTopSettings {
  // Default hotkey CTRL + ALT + T
  PowerToysSettings::HotkeyObject editorHotkey =
    PowerToysSettings::HotkeyObject::from_settings(false, true, true, false, 0x54, L"T"); // ASCII code for T 0x54
};

class AlwaysOnTop : public PowertoyModuleIface {
private:

  bool                     enabled{ false };
  HWND                     hotKeyHandleWindow{ nullptr };
  HWND                     currentlyOnTop{ nullptr };
  AlwaysOnTopSettings      moduleSettings;
  std::wstring             itemName{ MODULE_NAME };
  PowertoySystemMenuIface* systemMenuHelper{ nullptr };

  void ProcessCommand(HWND window);
  bool SetWindowOnTop(HWND window);
  void ResetCurrentOnTop();
  void CleanUp();

  void LoadSettings(PCWSTR config, bool fromFile);
  void SaveSettings();

  std::vector<PowertoySystemMenuIface::ItemInfo> CustomItems();

  LRESULT WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;

protected:

  static LRESULT CALLBACK WndProc_Helper(HWND, UINT, WPARAM, LPARAM) noexcept;

public:

  AlwaysOnTop()
  {
    LoadSettings(MODULE_NAME, true);
  }

  virtual void destroy() override
  {
    CleanUp();
    delete this;
  }

  virtual const wchar_t* get_name() override
  {
    return MODULE_NAME;
  }

  virtual const wchar_t** get_events() override
  {
    static const wchar_t* events[] = { nullptr };
    return events;
  }

  virtual bool get_config(wchar_t* buffer, int* buffer_size) override
  {
    HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

    PowerToysSettings::Settings settings(hinstance, get_name());
    settings.set_description(MODULE_DESC);
    settings.add_hotkey(HOTKEY_NAME, IDS_SETTING_ALWAYS_ON_TOP_HOTKEY, moduleSettings.editorHotkey);
    settings.set_overview_link(OVERVIEW_LINK);

    return settings.serialize_to_buffer(buffer, buffer_size);
  }

  virtual void set_config(const wchar_t* config) override
  {
    LoadSettings(config, false);
    SaveSettings();

    PowerToysSettings::PowerToyValues values =
      PowerToysSettings::PowerToyValues::from_json_string(config);
    if (values.is_object_value(HOTKEY_NAME)) {
      moduleSettings.editorHotkey = PowerToysSettings::HotkeyObject::from_json(values.get_json(HOTKEY_NAME));

      // Hotkey updated. Register new hotkey and trigger system menu update.
      UnregisterHotKey(hotKeyHandleWindow, 1);
      RegisterHotKey(hotKeyHandleWindow, 1, moduleSettings.editorHotkey.get_modifiers(), moduleSettings.editorHotkey.get_code());

      systemMenuHelper->SetConfiguration(this, CustomItems());
    }
  }

  virtual void call_custom_action(const wchar_t* action) override
  {

  }

  virtual void enable()
  {
    HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = WndProc_Helper;
    wcex.hInstance = hinstance;
    wcex.lpszClassName = HOTKEY_WINDOW_CLASS_NAME;
    RegisterClassExW(&wcex);

    hotKeyHandleWindow = CreateWindowExW(WS_EX_TOOLWINDOW, HOTKEY_WINDOW_CLASS_NAME, L"", WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, hinstance, this);
    if (!hotKeyHandleWindow) {
      return;
    }

    RegisterHotKey(hotKeyHandleWindow, 1, moduleSettings.editorHotkey.get_modifiers(), moduleSettings.editorHotkey.get_code());

    enabled = true;
  }

  virtual void disable()
  {
    CleanUp();
    enabled = false;
  }

  virtual bool is_enabled() override
  {
    return enabled;
  }

  virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override
  {
    return 0;
  }

  virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override
  {
    systemMenuHelper = helper;
    systemMenuHelper->SetConfiguration(this, CustomItems());
  }

  virtual void signal_system_menu_action(const wchar_t* name) override
  {
    if (name == itemName) {
      ProcessCommand(GetForegroundWindow());
    }
  }
};

void AlwaysOnTop::ProcessCommand(HWND window)
{
  bool alreadyOnTop = (currentlyOnTop == window);
  ResetCurrentOnTop();
  if (!alreadyOnTop) {
    (void)SetWindowOnTop(window);
  }
}

bool AlwaysOnTop::SetWindowOnTop(HWND window)
{
  if (SetWindowPos(window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE)) {
    currentlyOnTop = window;
    systemMenuHelper->ProcessSelectedItem(this, currentlyOnTop, itemName.c_str());
    return true;
  }
  return false;
}

void AlwaysOnTop::ResetCurrentOnTop()
{
  if (currentlyOnTop &&
    SetWindowPos(currentlyOnTop, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE)) {
    systemMenuHelper->ProcessSelectedItem(this, currentlyOnTop, itemName.c_str());
  }
  currentlyOnTop = nullptr;
}

void AlwaysOnTop::CleanUp()
{
  ResetCurrentOnTop();
  if (hotKeyHandleWindow) {
    DestroyWindow(hotKeyHandleWindow);
    hotKeyHandleWindow = nullptr;
  }
  UnregisterClass(HOTKEY_WINDOW_CLASS_NAME, reinterpret_cast<HINSTANCE>(&__ImageBase));
}

void AlwaysOnTop::LoadSettings(PCWSTR config, bool fromFile)
{
  try {
    PowerToysSettings::PowerToyValues values = fromFile ?
      PowerToysSettings::PowerToyValues::load_from_settings_file(get_name()) :
      PowerToysSettings::PowerToyValues::from_json_string(config);

    if (values.is_object_value(HOTKEY_NAME))
    {
      moduleSettings.editorHotkey = PowerToysSettings::HotkeyObject::from_json(values.get_json(HOTKEY_NAME));
      itemName = std::wstring(MODULE_NAME) + L"\t" + moduleSettings.editorHotkey.to_string();
    }
  }
  catch (std::exception&) {}
}

void AlwaysOnTop::SaveSettings()
{
  PowerToysSettings::PowerToyValues values(get_name());
  values.add_property(HOTKEY_NAME, moduleSettings.editorHotkey);
  try {
    values.save_to_settings_file();
  }
  catch (std::exception&) {}
}

std::vector<PowertoySystemMenuIface::ItemInfo> AlwaysOnTop::CustomItems()
{
  PowertoySystemMenuIface::ItemInfo info{ itemName, true, true };
  return std::vector<PowertoySystemMenuIface::ItemInfo>(1, info);
}

LRESULT AlwaysOnTop::WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
  if (message == WM_HOTKEY) {
    if (HWND fw{ GetForegroundWindow() }) {
      ProcessCommand(fw);
    }
  }
  return 0;
}

LRESULT CALLBACK AlwaysOnTop::WndProc_Helper(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
  auto thisRef = reinterpret_cast<AlwaysOnTop*>(GetWindowLongPtr(window, GWLP_USERDATA));

  if (!thisRef && (message == WM_CREATE))
  {
    const auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
    thisRef = reinterpret_cast<AlwaysOnTop*>(createStruct->lpCreateParams);
    SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
  }

  return thisRef ? thisRef->WndProc(window, message, wparam, lparam) :
    DefWindowProc(window, message, wparam, lparam);
}

extern "C" __declspec(dllexport) PowertoyModuleIface * __cdecl powertoy_create()
{
  return new AlwaysOnTop();
}
