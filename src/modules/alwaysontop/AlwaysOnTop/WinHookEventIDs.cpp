#include "pch.h"

#include "WinHookEventIDs.h"

UINT WM_PRIV_SETTINGS_CHANGED;

std::once_flag init_flag;

void InitializeWinhookEventIds()
{
    std::call_once(init_flag, [&] {
        WM_PRIV_SETTINGS_CHANGED = RegisterWindowMessage(L"{11978F7B-221A-4E65-B8A8-693F7D6E4B25}");
    });
}
