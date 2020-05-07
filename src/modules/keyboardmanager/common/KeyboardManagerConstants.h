#pragma once
#include <string>

namespace KeyboardManagerConstants
{
    // Name of the powertoy module.
    inline const std::wstring ModuleName = L"Keyboard Manager";

    // Name of the property use to store current active configuration.
    inline const std::wstring ActiveConfigurationSettingName = L"activeConfiguration";

    // Name of the property use to store single keyremaps.
    inline const std::wstring RemapKeysSettingName = L"remapKeys";

    // Name of the property use to store single keyremaps array in case of in process approach.
    inline const std::wstring InProcessRemapKeysSettingName = L"inProcess";

    // Name of the property use to store shortcut remaps.
    inline const std::wstring RemapShortcutsSettingName = L"remapShortcuts";

    // Name of the property use to store global shortcut remaps array.
    inline const std::wstring GlobalRemapShortcutsSettingName = L"global";

    // Name of the property use to store original keys.
    inline const std::wstring OriginalKeysSettingName = L"originalKeys";

    // Name of the property use to store new remap keys.
    inline const std::wstring NewRemapKeysSettingName = L"newRemapKeys";

    // Name of the default configuration.
    inline const std::wstring DefaultConfiguration = L"default";

    // Name of the named mutex used for configuration file.
    inline const std::wstring ConfigFileMutexName = L"PowerToys.KeyboardManager.ConfigMutex";

    // Name of the dummy update file.
    inline const std::wstring DummyUpdateFileName = L"settings-updated.json";

    // Initial value for tooltip
    inline const winrt::hstring ToolTipInitialContent = L"Initialised";

    inline const long MinShortcutSize = 2;
    inline const long MaxShortcutSize = 3;

    inline const long ShortcutTableColCount = 5;
    inline const long ShortcutTableHeaderCount = 2;
    inline const long ShortcutTableOriginalColIndex = 0;
    inline const long ShortcutTableArrowColIndex = 1;
    inline const long ShortcutTableNewColIndex = 2;
    inline const long ShortcutTableRemoveColIndex = 3;
    inline const long ShortcutTableWarningColIndex = 4;
    inline const long RemapTableColCount = 5;
    inline const long RemapTableHeaderCount = 2;
    inline const long RemapTableOriginalColIndex = 0;
    inline const long RemapTableArrowColIndex = 1;
    inline const long RemapTableNewColIndex = 2;
    inline const long RemapTableRemoveColIndex = 3;
    inline const long RemapTableWarningColIndex = 4;

    inline const long RemapTableControlWidth = 100;
    inline const long ShortcutTableControlWidth = 100;
    inline const long TableDropDownHeight = 200;
}