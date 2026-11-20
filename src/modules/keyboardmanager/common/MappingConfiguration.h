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

    // How a LoadSettings call ended, for callers that load into a fresh (staged) configuration and
    // must tell "the active profile has no config file" apart from "the file could not be read now".
    enum class LoadOutcome
    {
        // The active profile's config file was read; the tables hold its remaps (some entries may
        // have been skipped as malformed, in which case LoadSettings returns false).
        Loaded,

        // The active profile's config file is confirmed absent; every remap table was cleared.
        ConfirmedAbsent,

        // settings.json or the profile's config file could not be read right now, or the profile name
        // was not resolved; the tables were left as they were.
        Failed,
    };

    // Load the configuration. If `outcome` is not null, it receives how the load ended.
    bool LoadSettings(LoadOutcome* outcome = nullptr);

    // Exchange every remap table, the derived scan-code map and the configuration name with `other`
    // (container swaps only, no copies). Runtime state that is not part of a profile, such as
    // numpadKeyPressed, is not exchanged.
    void SwapMappings(MappingConfiguration& other) noexcept;

    bool IsConfigurationNameResolved() const;

    // Save the updated configuration.
    bool SaveSettingsToFile();

    // Function to clear the OS Level shortcut remapping table
    void ClearOSLevelShortcuts();

    // Function to clear the Keys remapping table
    void ClearSingleKeyRemaps();

    // Function to clear the Keys to text remapping table
    void ClearSingleKeyToTextRemaps();

    // Function to clear the "Alone" single key remapping table (dual-key: tap-alone action)
    void ClearSingleKeyAloneRemaps();

    // Function to clear the App specific shortcut remapping table
    void ClearAppSpecificShortcuts();

    // Function to add a new single key to key remapping
    bool AddSingleKeyRemap(const DWORD& originalKey, const KeyShortcutTextUnion& newRemapKey);

    // Function to add a new single key to unicode string remapping
    bool AddSingleKeyToTextRemap(const DWORD originalKey, const std::wstring& text);

    // Function to add a new "Alone" single key remapping (dual-key / Karabiner to_if_alone):
    // the action applied only when originalKey is tapped alone; in combination the original key passes through.
    bool AddSingleKeyAloneRemap(const DWORD& originalKey, const KeyShortcutTextUnion& aloneRemapKey);

    // Function to add a new OS level shortcut remapping
    bool AddOSLevelShortcut(const Shortcut& originalSC, const KeyShortcutTextUnion& newSC);

    // Function to add a new App specific level shortcut remapping
    bool AddAppSpecificShortcut(const std::wstring& app, const Shortcut& originalSC, const KeyShortcutTextUnion& newSC);

    // The map members and their mutexes are left as public since the maps are used extensively in dllmain.cpp.
    // Maps which store the remappings for each of the features. The bool fields should be initialized to false. They are used to check the current state of the shortcut (i.e is that particular shortcut currently pressed down or not).
    // Stores single key remappings
    SingleKeyRemapTable singleKeyReMap;

    // Stores "Alone" single key remappings (dual-key tap-alone action). Same source key may also
    // exist in singleKeyReMap in a fuller implementation; for phase 1 an entry here means
    // "tap alone -> this action, in combination pass the original key through".
    SingleKeyRemapTable aloneSingleKeyReMap;

    std::unordered_map<DWORD, DWORD> scanMap;

    std::unordered_map<DWORD, bool> numpadKeyPressed;

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

    // Parses the single-key remap section of a settings JSON object into singleKeyReMap /
    // aloneSingleKeyReMap, routing by the optional per-entry "condition" field. Public so the
    // dual-key (tap-alone) condition round-trip can be unit tested without touching disk.
    bool LoadSingleKeyRemaps(const json::JsonObject& jsonData);

private:
    bool configurationNameResolved = false;

    bool LoadSingleKeyToTextRemaps(const json::JsonObject& jsonData);
    bool LoadShortcutRemaps(const json::JsonObject& jsonData, const std::wstring& objectName);
    bool LoadAppSpecificShortcutRemaps(const json::JsonObject& remapShortcutsData);
};