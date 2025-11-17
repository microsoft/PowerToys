#include "pch.h"

#include "FancyZonesWinHookEventIDs.h"

UINT WM_PRIV_MOVESIZESTART;
UINT WM_PRIV_MOVESIZEEND;
UINT WM_PRIV_LOCATIONCHANGE;
UINT WM_PRIV_NAMECHANGE;
UINT WM_PRIV_WINDOWCREATED;
UINT WM_PRIV_INIT;
UINT WM_PRIV_VD_SWITCH;
UINT WM_PRIV_EDITOR;
UINT WM_PRIV_LAYOUT_HOTKEYS_FILE_UPDATE;
UINT WM_PRIV_LAYOUT_TEMPLATES_FILE_UPDATE;
UINT WM_PRIV_CUSTOM_LAYOUTS_FILE_UPDATE;
UINT WM_PRIV_APPLIED_LAYOUTS_FILE_UPDATE;
UINT WM_PRIV_DEFAULT_LAYOUTS_FILE_UPDATE;
UINT WM_PRIV_SNAP_HOTKEY;
UINT WM_PRIV_QUICK_LAYOUT_KEY;
UINT WM_PRIV_SETTINGS_CHANGED;

std::once_flag init_flag;

void InitializeWinhookEventIds()
{
    std::call_once(init_flag, [&] {
        WM_PRIV_MOVESIZESTART = RegisterWindowMessage(L"{f48def23-df42-4c0f-a13d-3eb4a9e204d4}");
        WM_PRIV_MOVESIZEEND = RegisterWindowMessage(L"{805d643c-804d-4728-b533-907d760ebaf0}");
        WM_PRIV_LOCATIONCHANGE = RegisterWindowMessage(L"{d56c5ee7-58e5-481c-8c4f-8844cf4d0347}");
        WM_PRIV_NAMECHANGE = RegisterWindowMessage(L"{b7b30c61-bfa0-4d95-bcde-fc4f2cbf6d76}");
        WM_PRIV_WINDOWCREATED = RegisterWindowMessage(L"{bdb10669-75da-480a-9ec4-eeebf09a02d7}");
        WM_PRIV_INIT = RegisterWindowMessage(L"{469818a8-00fa-4069-b867-a1da484fcd9a}");
        WM_PRIV_VD_SWITCH = RegisterWindowMessage(L"{128c2cb0-6bdf-493e-abbe-f8705e04aa95}");
        WM_PRIV_EDITOR = RegisterWindowMessage(L"{87543824-7080-4e91-9d9c-0404642fc7b6}");
        WM_PRIV_LAYOUT_HOTKEYS_FILE_UPDATE = RegisterWindowMessage(L"{07229b7e-4f22-4357-b136-33c289be2295}");
        WM_PRIV_LAYOUT_TEMPLATES_FILE_UPDATE = RegisterWindowMessage(L"{4686f019-5d3d-4c5c-9051-b7cbbccca77d}");
        WM_PRIV_CUSTOM_LAYOUTS_FILE_UPDATE = RegisterWindowMessage(L"{0972787e-cdab-4e16-b228-91acdc38f40f}");
        WM_PRIV_APPLIED_LAYOUTS_FILE_UPDATE = RegisterWindowMessage(L"{2ef2c8a7-e0d5-4f31-9ede-52aade2d284d}");
        WM_PRIV_DEFAULT_LAYOUTS_FILE_UPDATE = RegisterWindowMessage(L"{61fd2afb-e909-41b2-b6f3-b9f546f2ae3f}");
        WM_PRIV_SNAP_HOTKEY = RegisterWindowMessage(L"{72f4fd8e-23f1-43ab-bbbc-029363df9a84}");
        WM_PRIV_QUICK_LAYOUT_KEY = RegisterWindowMessage(L"{15baab3d-c67b-4a15-aFF0-13610e05e947}");
        WM_PRIV_SETTINGS_CHANGED = RegisterWindowMessage(L"{89ca3Daa-bf2d-4e73-9f3f-c60716364e27}");
    });
}
