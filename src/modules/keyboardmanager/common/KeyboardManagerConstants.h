#pragma once
#include <string>
#include <winrt/base.h>
#include <common/utils/resources.h>
#include "keyboardmanager/dll/Generated Files/resource.h"

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

    // Name of the property use to store app specific shortcut remaps array.
    inline const std::wstring AppSpecificRemapShortcutsSettingName = L"appSpecific";

    // Name of the property use to store original keys.
    inline const std::wstring OriginalKeysSettingName = L"originalKeys";

    // Name of the property use to store new remap keys.
    inline const std::wstring NewRemapKeysSettingName = L"newRemapKeys";

    // Name of the property use to store the target application.
    inline const std::wstring TargetAppSettingName = L"targetApp";

    // Name of the default configuration.
    inline const std::wstring DefaultConfiguration = L"default";

    // Name of the named mutex used for configuration file.
    inline const std::wstring ConfigFileMutexName = L"PowerToys.KeyboardManager.ConfigMutex";

    // Name of the dummy update file.
    inline const std::wstring DummyUpdateFileName = L"settings-updated.json";

    // Minimum and maximum size of a shortcut
    inline const long MinShortcutSize = 2;
    inline const long MaxShortcutSize = 3;

    // Default window sizes
    inline const int DefaultEditKeyboardWindowWidth = 800;
    inline const int DefaultEditKeyboardWindowHeight = 600;
    inline const int MinimumEditKeyboardWindowWidth = 500;
    inline const int MinimumEditKeyboardWindowHeight = 450;
    inline const int EditKeyboardTableMinWidth = 700;
    inline const int DefaultEditShortcutsWindowWidth = 1050;
    inline const int DefaultEditShortcutsWindowHeight = 600;
    inline const int MinimumEditShortcutsWindowWidth = 500;
    inline const int MinimumEditShortcutsWindowHeight = 500;
    inline const int EditShortcutsTableMinWidth = 1000;

    // Key Remap table constants
    inline const long RemapTableColCount = 4;
    inline const long RemapTableHeaderCount = 2;
    inline const long RemapTableOriginalColIndex = 0;
    inline const long RemapTableArrowColIndex = 1;
    inline const long RemapTableNewColIndex = 2;
    inline const long RemapTableRemoveColIndex = 3;
    inline const DWORD64 RemapTableDropDownWidth = 110;

    // Shortcut table constants
    inline const long ShortcutTableColCount = 5;
    inline const long ShortcutTableHeaderCount = 3;
    inline const long ShortcutTableOriginalColIndex = 0;
    inline const long ShortcutTableArrowColIndex = 1;
    inline const long ShortcutTableNewColIndex = 2;
    inline const long ShortcutTableTargetAppColIndex = 3;
    inline const long ShortcutTableRemoveColIndex = 4;
    inline const long ShortcutArrowColumnWidth = 90;
    inline const DWORD64 ShortcutTableDropDownWidth = 110;
    inline const DWORD64 ShortcutTableDropDownSpacing = 10;
    inline const long ShortcutOriginColumnWidth = 3 * ShortcutTableDropDownWidth + 2 * ShortcutTableDropDownSpacing;
    inline const long ShortcutTargetColumnWidth = 3 * ShortcutTableDropDownWidth + 2 * ShortcutTableDropDownSpacing + 25;

    // Drop down height used for both Edit Keyboard and Edit Shortcuts
    inline const DWORD64 TableDropDownHeight = 200;
    inline const DWORD64 TableArrowColWidth = 230;
    inline const DWORD64 TableRemoveColWidth = 20;
    inline const DWORD64 TableWarningColWidth = 20;
    inline const DWORD64 TableTargetAppColWidth = ShortcutTableDropDownWidth + TableRemoveColWidth * 2;

    // Shared style constants for both Remap Table and Shortcut Table
    inline const DWORD64 HeaderButtonWidth = 100;

    // Flags used for distinguishing key events sent by Keyboard Manager
    inline const ULONG_PTR KEYBOARDMANAGER_SINGLEKEY_FLAG = 0x11; // Single key remaps
    inline const ULONG_PTR KEYBOARDMANAGER_SHORTCUT_FLAG = 0x101; // Shortcut remaps
    inline const ULONG_PTR KEYBOARDMANAGER_SUPPRESS_FLAG = 0x111; // Key events which must be suppressed

    // Dummy key event used in between key up and down events to prevent certain global events from happening
    inline const DWORD DUMMY_KEY = 0xFF;

    // Number of key messages required while sending a dummy key event
    inline const size_t DUMMY_KEY_EVENT_SIZE = 2;

    // String constant for the default app name in Remap shortcuts
    inline const std::wstring DefaultAppName = GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_ALLAPPS);

    // String constant to represent no activated application in app-specific shortcuts
    inline const std::wstring NoActivatedApp = L"";
}