#pragma once
#include <optional>
#include <string>
#include <common/SettingsAPI/settings_objects.h>

// Start the Tray Icon
void start_tray_icon(bool isProcessElevated);
// Change the Tray Icon visibility
void set_tray_icon_visible(bool shouldIconBeVisible);
// Stop the Tray Icon
void stop_tray_icon();
// Open the Settings Window
void open_settings_window(std::optional<std::wstring> settings_window);
// Update Quick Access Hotkey
void update_quick_access_hotkey(bool enabled, PowerToysSettings::HotkeyObject hotkey);
// Callback type to be called by the tray icon loop
typedef void (*main_loop_callback_function)(PVOID);
// Calls a callback in _callback
bool dispatch_run_on_main_ui_thread(main_loop_callback_function _callback, PVOID data);

// Must be the same as: settings-ui/Settings.UI/Views/ShellPage.xaml.cs -> ExitPTItem_Tapped() -> const string ptTrayIconWindowClass
const inline wchar_t* pt_tray_icon_window_class = L"PToyTrayIconWindow";