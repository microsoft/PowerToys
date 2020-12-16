#pragma once

#include <cstdint>

namespace CommonSharedConstants
{
    // Flag that can be set on an input event so that it is ignored by Keyboard Manager
    const uintptr_t KEYBOARDMANAGER_INJECTED_FLAG = 0x1;

    // Fake key code to represent VK_WIN.
    inline const int VK_WIN_BOTH = 0x104;

    // Path to the event used by PowerLauncher
    const wchar_t POWER_LAUNCHER_SHARED_EVENT[] = L"Local\\PowerToysRunInvokeEvent-30f26ad7-d36d-4c0e-ab02-68bb5ff3c4ab";

    // Max DWORD for key code to disable keys.
    const int VK_DISABLED = 0x100;
}