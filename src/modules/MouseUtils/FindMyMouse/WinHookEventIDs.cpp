#include "pch.h"

#include "WinHookEventIDs.h"

UINT WM_PRIV_SHORTCUT;
UINT WM_PRIV_SETTINGS_CHANGED;

std::once_flag init_flag;

void InitializeWinhookEventIds()
{
    std::call_once(init_flag, [&] {
        WM_PRIV_SHORTCUT = RegisterWindowMessage(L"{1365FFC7-A44E-4171-9692-A3EEF378AE60}");
        WM_PRIV_SETTINGS_CHANGED = RegisterWindowMessage(L"{E63D3766-FE30-450C-A94A-5D2A48D1411A}");
    });
}

EXTERN_C __declspec(dllexport) UINT GetWmPrivShortcut()
{
    InitializeWinhookEventIds();
    return WM_PRIV_SHORTCUT;
}

EXTERN_C __declspec(dllexport) UINT GetWmPrivSettingsChanged()
{
    InitializeWinhookEventIds();
    return WM_PRIV_SETTINGS_CHANGED;
}
