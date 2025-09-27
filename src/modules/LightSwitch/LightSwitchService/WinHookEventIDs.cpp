
#include "WinHookEventIDs.h"
#include <wtypes.h>
#include <mutex>

UINT WM_PRIV_SETTINGS_CHANGED = 0;

std::once_flag init_flag;

void InitializeWinhookEventIds()
{
    std::call_once(init_flag, [&] {
        WM_PRIV_SETTINGS_CHANGED = RegisterWindowMessage(L"{11978F7B-221A-4E65-B9A9-693F7D6E4B25}");
    });
}
