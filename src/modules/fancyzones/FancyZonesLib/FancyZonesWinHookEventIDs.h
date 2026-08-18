#pragma once

extern UINT WM_PRIV_MOVESIZESTART;
extern UINT WM_PRIV_MOVESIZEEND;
extern UINT WM_PRIV_LOCATIONCHANGE;
extern UINT WM_PRIV_NAMECHANGE;
extern UINT WM_PRIV_WINDOWCREATED;
extern UINT WM_PRIV_WINDOWDESTROYED;
extern UINT WM_PRIV_INIT; // Scheduled when FancyZones is initialized
extern UINT WM_PRIV_VD_SWITCH; // Scheduled when virtual desktop switch occurs
extern UINT WM_PRIV_EDITOR; // Scheduled when the editor exits
extern UINT WM_PRIV_LAYOUT_HOTKEYS_FILE_UPDATE; // Scheduled when the watched layout-hotkeys.json file is updated
extern UINT WM_PRIV_LAYOUT_TEMPLATES_FILE_UPDATE; // Scheduled when the watched layout-templates.json file is updated
extern UINT WM_PRIV_CUSTOM_LAYOUTS_FILE_UPDATE; // Scheduled when the watched custom-layouts.json file is updated
extern UINT WM_PRIV_APPLIED_LAYOUTS_FILE_UPDATE; // Scheduled when the watched applied-layouts.json file is updated
extern UINT WM_PRIV_DEFAULT_LAYOUTS_FILE_UPDATE; // Scheduled when the watched default-layouts.json file is updated
extern UINT WM_PRIV_SNAP_HOTKEY; // Scheduled when we receive a snap hotkey key down press
extern UINT WM_PRIV_QUICK_LAYOUT_KEY; // Scheduled when we receive a key down press to quickly apply a layout
extern UINT WM_PRIV_SETTINGS_CHANGED; // Scheduled when a watched settings file is updated
extern UINT WM_PRIV_SAVE_EDITOR_PARAMETERS; // Scheduled to request saving editor-parameters.json
extern UINT WM_PRIV_MONITOR_ROTATION_PREVIEW_SHOW; // Scheduled when the monitor rotation preview should be shown
extern UINT WM_PRIV_MONITOR_ROTATION_PREVIEW_HIDE; // Scheduled when the monitor rotation preview should be hidden
extern UINT WM_PRIV_MONITOR_ROTATION_PREVIEW_ROTATE; // Scheduled when monitor rotation preview should rotate windows

void InitializeWinhookEventIds();
