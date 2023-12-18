#pragma once

#include <common/utils/json.h>

#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/common/Shortcut.h>
#include <keyboardmanager/common/RemapShortcut.h>

using SingleKeyRemapTable = std::unordered_map<DWORD, KeyShortcutTextUnion>;
using SingleKeyToTextRemapTable = SingleKeyRemapTable;
using ShortcutRemapTable = std::map<Shortcut, RemapShortcut>;
using AppSpecificShortcutRemapTable = std::map<std::wstring, ShortcutRemapTable>;

class MappingConfiguration
{
public:
    ~MappingConfiguration() = default;

    // Load the configuration.
    bool LoadSettings();

    // Save the updated configuration.
    bool SaveSettingsToFile();

    // Function to clear the OS Level shortcut remapping table
    void ClearOSLevelShortcuts();

    // Function to clear the Keys remapping table
    void ClearSingleKeyRemaps();

    // Function to clear the Keys to text remapping table
    void ClearSingleKeyToTextRemaps();

    // Function to clear the App specific shortcut remapping table
    void ClearAppSpecificShortcuts();

    // Function to add a new single key to key remapping
    bool AddSingleKeyRemap(const DWORD& originalKey, const KeyShortcutTextUnion& newRemapKey);

    // Function to add a new single key to unicode string remapping
    bool AddSingleKeyToTextRemap(const DWORD originalKey, const std::wstring& text);

    // Function to add a new OS level shortcut remapping
    bool AddOSLevelShortcut(const Shortcut& originalSC, const KeyShortcutTextUnion& newSC);

    // Function to add a new App specific level shortcut remapping
    bool AddAppSpecificShortcut(const std::wstring& app, const Shortcut& originalSC, const KeyShortcutTextUnion& newSC);

    // The map members and their mutexes are left as public since the maps are used extensively in dllmain.cpp.
    // Maps which store the remappings for each of the features. The bool fields should be initialized to false. They are used to check the current state of the shortcut (i.e is that particular shortcut currently pressed down or not).
    // Stores single key remappings
    SingleKeyRemapTable singleKeyReMap;

    // Stores single key to text remappings
    SingleKeyToTextRemapTable singleKeyToTextReMap;

    // Stores the os level shortcut remappings
    ShortcutRemapTable osLevelShortcutReMap;
    std::vector<Shortcut> osLevelShortcutReMapSortedKeys;

    // Stores the app-specific shortcut remappings. Maps application name to the shortcut map
    AppSpecificShortcutRemapTable appSpecificShortcutReMap;
    std::map<std::wstring, std::vector<Shortcut>> appSpecificShortcutReMapSortedKeys;

    // Stores the current configuration name.
    std::wstring currentConfig = KeyboardManagerConstants::DefaultConfiguration;

private:
    bool LoadSingleKeyRemaps(const json::JsonObject& jsonData);
    bool LoadSingleKeyToTextRemaps(const json::JsonObject& jsonData);
    bool LoadShortcutRemaps(const json::JsonObject& jsonData, const std::wstring& objectName);
    bool LoadAppSpecificShortcutRemaps(const json::JsonObject& remapShortcutsData);
};