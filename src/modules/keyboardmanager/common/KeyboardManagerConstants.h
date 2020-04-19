#pragma once
#include <string>

namespace KeyboardManagerConstants
{
    // Name of the powertoy module.
    inline const std::wstring ModuleName = L"Keyboard Manager";

    // Name of the property use to store current active configuration.
    inline const std::wstring ActiveConfigurationSettingName = L"activeConfiguration";

    // Name of the property use to store single keyremaps array.
    inline const std::wstring RemapKeysSettingName = L"remapKeys";

    // Name of the property use to store shortcut remaps array.
    inline const std::wstring RemapShortcutsSettingName = L"remapShortcuts";

    // Name of the property use to store global shortcut remaps array.
    inline const std::wstring GlobalRemapShortcutsSettingName = L"global";

    // Name of the property use to store original keys.
    inline const std::wstring OriginalKeysSettingName = L"originalKeys";

    // Name of the property use to store new remap keys.
    inline const std::wstring NewRemapKeysSettingName = L"newRemapKeys";

    // Name of the default configuration.
    inline const std::wstring DefaultConfiguration = L"default";

    // Name of the dummy update file.
    inline const std::wstring DummyUpdateFileName = L"settings-updated.json";

    // Fake key code to represent VK_WIN.
    inline const DWORD VK_WIN_BOTH = 0x104;
}