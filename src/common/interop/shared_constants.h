#pragma once

#include <cstdint>

namespace CommonSharedConstants
{
    // Flag that can be set on an input event so that it is ignored by Keyboard Manager
    const uintptr_t KEYBOARDMANAGER_INJECTED_FLAG = 0x1;

    // Fake key code to represent VK_WIN.
    inline const int VK_WIN_BOTH = 0x104;

    const wchar_t APPDATA_PATH[] = L"Microsoft\\PowerToys";

    // Path to the event used by PowerLauncher
    const wchar_t POWER_LAUNCHER_SHARED_EVENT[] = L"Local\\PowerToysRunInvokeEvent-30f26ad7-d36d-4c0e-ab02-68bb5ff3c4ab";

    const wchar_t RUN_SEND_SETTINGS_TELEMETRY_EVENT[] = L"Local\\PowerToysRunInvokeEvent-638ec522-0018-4b96-837d-6bd88e06f0d6";

    const wchar_t RUN_EXIT_EVENT[] = L"Local\\PowerToysRunExitEvent-3e38e49d-a762-4ef1-88f2-fd4bc7481516";

    const wchar_t COLOR_PICKER_SEND_SETTINGS_TELEMETRY_EVENT[] = L"Local\\ColorPickerSettingsTelemetryEvent-6c7071d8-4014-46ec-b687-913bd8a422f1";

    // Path to the event used to show Color Picker
    const wchar_t SHOW_COLOR_PICKER_SHARED_EVENT[] = L"Local\\ShowColorPickerEvent-8c46be2a-3e05-4186-b56b-4ae986ef2525";

    const wchar_t SHORTCUT_GUIDE_EXIT_EVENT[] = L"Local\\ShortcutGuide-ExitEvent-35697cdd-a3d2-47d6-a246-34efcc73eac0";

    // Max DWORD for key code to disable keys.
    const int VK_DISABLED = 0x100;
}
