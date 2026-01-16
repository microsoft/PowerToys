#pragma once

extern UINT WM_PRIV_SHORTCUT; // Shortcut is pressed
extern UINT WM_PRIV_SETTINGS_CHANGED; // Settings have changed

void InitializeWinhookEventIds();

EXTERN_C __declspec(dllexport) UINT GetWmPrivShortcut();

EXTERN_C __declspec(dllexport) UINT GetWmPrivSettingsChanged();
