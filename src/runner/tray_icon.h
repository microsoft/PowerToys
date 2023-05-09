#pragma once
#include <optional>
#include <string>

// Start the Tray Icon
void start_tray_icon();
// Stop the Tray Icon
void stop_tray_icon();
// Open the Settings Window
void open_settings_window(std::optional<std::wstring> settings_window, bool show_flyout, const std::optional<POINT>& flyout_position = std::nullopt);
// Callback type to be called by the tray icon loop
typedef void (*main_loop_callback_function)(PVOID);
// Calls a callback in _callback
bool dispatch_run_on_main_ui_thread(main_loop_callback_function _callback, PVOID data);

const inline wchar_t* pt_tray_icon_window_class = L"PToyTrayIconWindow";