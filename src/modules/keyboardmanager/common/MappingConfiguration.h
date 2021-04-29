#pragma once

#include <common/utils/json.h>

#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/common/Shortcut.h>
#include <keyboardmanager/common/RemapShortcut.h>

using SingleKeyRemapTable = std::unordered_map<DWORD, KeyShortcutUnion>;
using ShortcutRemapTable = std::map<Shortcut, RemapShortcut>;
using AppSpecificShortcutRemapTable = std::map<std::wstring, ShortcutRemapTable>;

class MappingConfiguration
{
public:
    MappingConfiguration();

    ~MappingConfiguration() = default;

    // Load the configuration.
    bool LoadSettings();

    // Save the updated configuration.
    bool SaveSettingsToFile();

    // Function to clear the OS Level shortcut remapping table
    void ClearOSLevelShortcuts();

    // Function to clear the Keys remapping table
    void ClearSingleKeyRemaps();

    // Function to clear the App specific shortcut remapping table
    void ClearAppSpecificShortcuts();

    // Function to add a new single key to key remapping
    bool AddSingleKeyRemap(const DWORD& originalKey, const KeyShortcutUnion& newRemapKey);

    // Function to add a new OS level shortcut remapping
    bool AddOSLevelShortcut(const Shortcut& originalSC, const KeyShortcutUnion& newSC);

    // Function to add a new App specific level shortcut remapping
    bool AddAppSpecificShortcut(const std::wstring& app, const Shortcut& originalSC, const KeyShortcutUnion& newSC);

    // The map members and their mutexes are left as public since the maps are used extensively in dllmain.cpp.
    // Maps which store the remappings for each of the features. The bool fields should be initialized to false. They are used to check the current state of the shortcut (i.e is that particular shortcut currently pressed down or not).
    // Stores single key remappings
    std::unordered_map<DWORD, KeyShortcutUnion> singleKeyReMap;

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
    bool LoadShortcutRemaps(const json::JsonObject& jsonData);
    bool LoadAppSpecificShortcutRemaps(const json::JsonObject& remapShortcutsData);
};