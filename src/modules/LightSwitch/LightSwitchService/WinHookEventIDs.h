#pragma once
#include <Windows.h>

extern UINT WM_PRIV_SETTINGS_CHANGED; // Scheduled when a watched settings file is updated

void InitializeWinhookEventIds();