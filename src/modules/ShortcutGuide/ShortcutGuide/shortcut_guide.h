#pragma once
#include "../interface/powertoy_module_interface.h"
//#include <interface/powertoy_module_interface.h>
#include "overlay_window.h"
#include "native_event_waiter.h"

#include "Generated Files/resource.h"

#include <common/hooks/LowlevelKeyboardEvent.h>

// We support only one instance of the overlay
extern class OverlayWindow* instance;

class TargetState;

class OverlayWindow
{
public:
    OverlayWindow(HWND activeWindow);
    void ShowWindow();
    bool IsDisabled();
    bool get_config(wchar_t* buffer, int* buffer_size);

    void set_config(const wchar_t* config);

    void on_held();
    void on_held_press(DWORD vkCode);
    void quick_hide();
    void was_hidden();

    bool overlay_visible() const;

    bool is_disabled_app(wchar_t* exePath);

    void get_exe_path(HWND window, wchar_t* exePath);
    ~OverlayWindow();
private:
    std::wstring app_name;
    //contains the non localized key of the powertoy
    std::wstring app_key;
    std::unique_ptr<TargetState> target_state;
    std::unique_ptr<D2DOverlayWindow> winkey_popup;
    std::unique_ptr<NativeEventWaiter> event_waiter;
    std::vector<std::wstring> disabled_apps_array;
    void init_settings();
    void update_disabled_apps();
    HWND activeWindow;

    struct OverlayOpacity
    {
        PCWSTR name = L"overlay_opacity";
        int value = 90; // percent
        int resourceId = IDS_SETTING_DESCRIPTION_OVERLAY_OPACITY;
    } overlayOpacity;

    struct Theme
    {
        PCWSTR name = L"theme";
        std::wstring value = L"system";
        int resourceId = IDS_SETTING_DESCRIPTION_THEME;
        std::vector<std::pair<std::wstring, UINT>> keys_and_texts = {
            { L"system", IDS_SETTING_DESCRIPTION_THEME_SYSTEM },
            { L"light", IDS_SETTING_DESCRIPTION_THEME_LIGHT },
            { L"dark", IDS_SETTING_DESCRIPTION_THEME_DARK }
        };
    } theme;

    struct DisabledApps
    {
        PCWSTR name = L"disabled_apps";
        std::wstring value = L"";
    } disabledApps;
};
