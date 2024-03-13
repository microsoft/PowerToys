#pragma once

namespace KeyboardManagerConstants
{
    // Event name for signaling settings changes
    inline const std::wstring SettingsEventName = L"PowerToys_KeyboardManager_Event_Settings";

    inline const std::wstring EditorWindowEventName = L"PowerToys_KeyboardManager_Event_EditorWindow";

    // Name of the powertoy module.
    inline const std::wstring ModuleName = L"Keyboard Manager";

    // Name of the property use to store current active configuration.
    inline const std::wstring ActiveConfigurationSettingName = L"activeConfiguration";

    // Name of the property use to store single keyremaps.
    inline const std::wstring RemapKeysSettingName = L"remapKeys";

    // Name of the property use to store single to text keyremaps.
    inline const std::wstring RemapKeysToTextSettingName = L"remapKeysToText";

    // Name of the property use to store single keyremaps array in case of in process approach.
    inline const std::wstring InProcessRemapKeysSettingName = L"inProcess";

    // Name of the property use to store shortcut remaps.
    inline const std::wstring RemapShortcutsSettingName = L"remapShortcuts";

    // Name of the property use to store shortcut to text remaps.
    inline const std::wstring RemapShortcutsToTextSettingName = L"remapShortcutsToText";

    // Name of the property use to store shortcut to run-program remaps.
    inline const std::wstring RemapShortcutsToRunProgramSettingName = L"remapShortcutsToRunProgram";

    // Name of the property use to store global shortcut remaps array.
    inline const std::wstring GlobalRemapShortcutsSettingName = L"global";

    // Name of the property use to store app specific shortcut remaps array.
    inline const std::wstring AppSpecificRemapShortcutsSettingName = L"appSpecific";

    // Name of the property use to store original keys.
    inline const std::wstring OriginalKeysSettingName = L"originalKeys";

    // Name of the property use to store new remap keys.
    inline const std::wstring NewRemapKeysSettingName = L"newRemapKeys";

    // Name of the property use to store new remapped string.
    inline const std::wstring NewTextSettingName = L"unicodeText";

    // Name of the property use to store runProgramStartInDir.
    inline const std::wstring RunProgramStartInDirSettingName = L"runProgramStartInDir";

    // Name of the property use to store runProgramStartInDir.
    inline const std::wstring RunProgramElevationLevelSettingName = L"runProgramElevationLevel";

    // Name of the property use to store runProgramAlreadyRunningAction.
    inline const std::wstring RunProgramAlreadyRunningAction = L"runProgramAlreadyRunningAction";

    // Name of the property use to store runProgramStartWindowType.
    inline const std::wstring RunProgramStartWindowType = L"runProgramStartWindowType";

    // Name of the property use to store runProgramArgs.
    inline const std::wstring RunProgramArgsSettingName = L"runProgramArgs";

    // Name of the property use to store runProgramFilePath.
    inline const std::wstring RunProgramFilePathSettingName = L"runProgramFilePath";

    // Name of the property use to store secondKeyOfChord.
    inline const std::wstring ShortcutSecondKeyOfChordSettingName = L"secondKeyOfChord";

    // Name of the property use to store openUri.
    inline const std::wstring ShortcutOpenURI = L"openUri";

    // Name of the property use to store shortcutOperationType.
    inline const std::wstring ShortcutOperationType = L"operationType";

    // Name of the property use to store the target application.
    inline const std::wstring TargetAppSettingName = L"targetApp";

    // Name of the default configuration.
    inline const std::wstring DefaultConfiguration = L"default";

    // monitors with different DPI scaling factor
    inline const int MinimumEditKeyboardWindowWidth = 200;
    inline const int MinimumEditKeyboardWindowHeight = 200;

    // Flags used for distinguishing key events sent by Keyboard Manager
    inline const ULONG_PTR KEYBOARDMANAGER_SINGLEKEY_FLAG = 0x11; // Single key remaps
    inline const ULONG_PTR KEYBOARDMANAGER_SHORTCUT_FLAG = 0x101; // Shortcut remaps
    inline const ULONG_PTR KEYBOARDMANAGER_SUPPRESS_FLAG = 0x111; // Key events which must be suppressed

    // Dummy key event used in between key up and down events to prevent certain global events from happening
    inline const DWORD DUMMY_KEY = 0xFF;

    // Number of key messages required while sending a dummy key event
    inline const size_t DUMMY_KEY_EVENT_SIZE = 2;

    // String constant to represent no activated application in app-specific shortcuts
    inline const std::wstring NoActivatedApp = L"";
}