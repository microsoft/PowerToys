#include "pch.h"

#include "WinHookEventIDs.h"

UINT WM_PRIV_SHORTCUT;

std::once_flag init_flag;

void InitializeWinhookEventIds()
{
    std::call_once(init_flag, [&] {
        WM_PRIV_SHORTCUT = RegisterWindowMessage(L"{1365FFC7-A44E-4171-9692-A3EEF378AE60}");
    });
}
