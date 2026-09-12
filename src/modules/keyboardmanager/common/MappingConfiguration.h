#pragma once

#include <common/utils/json.h>

#include <functional>
#include <string_view>
#include <vector>

#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/common/Shortcut.h>
#include <keyboardmanager/common/RemapShortcut.h>

using SingleKeyRemapTable = std::unordered_map<DWORD, KeyShortcutTextUnion>;
using SingleKeyToTextRemapTable = SingleKeyRemapTable;
using ShortcutRemapTable = std::map<Shortcut, RemapShortcut>;
using AppSpecificShortcutRemapTable = std::map<std::wstring, ShortcutRemapTable>;

struct TextExpansionRule
{
    std::wstring id;
    std::wstring sourceText;
    Shortcut activation;
    std::wstring replacementText;
    bool enabled = true;
};

using TextExpansionTable = std::vector<TextExpansionRule>;

enum class MappingConfigurationLoadResult
{
    Success = 0,
    // The parsed profile was applied, but one or more invalid entries were skipped.
    Partial = 1,
    Failure = 2,
};

struct MappingConfigurationSaveResult
{
    bool settingsCommitted = false;
    bool reloadNotified = false;
};

class MappingConfiguration
{
public:
    using SettingsWriter = std::function<bool(const std::wstring&, const json::JsonObject&)>;
    using SettingsReloadNotifier = std::function<bool()>;
    using SettingsPathProvider = std::function<std::wstring(const std::wstring&)>;

    explicit MappingConfiguration(SettingsWriter settingsWriter = {}, SettingsReloadNotifier settingsReloadNotifier = {}, SettingsPathProvider settingsPathProvider = {});
    ~MappingConfiguration() = default;

    // Load the configuration.
    bool LoadSettings();

    // Load while distinguishing rejected entries from a file-level failure.
    MappingConfigurationLoadResult LoadSettingsWithResult();

    // Load an already parsed profile. Valid entries are applied, while rejected entries are reported as Partial.
    MappingConfigurationLoadResult LoadSettingsFromJson(const json::JsonObject& configFile);

    // Load a named profile file. A missing profile is a valid empty snapshot; a partial profile applies its valid entries.
    MappingConfigurationLoadResult LoadSettingsFromFile(const std::wstring& configurationName, const std::wstring& filePath);

    bool IsConfigurationNameResolved() const;

    // Save the updated configuration.
    bool SaveSettingsToFile();

    // Save with separate persistence and live-reload notification outcomes.
    MappingConfigurationSaveResult SaveSettingsToFileWithResult();

    // Function to clear the OS Level shortcut remapping table
    void ClearOSLevelShortcuts();

    // Function to clear the Keys remapping table
    void ClearSingleKeyRemaps();

    // Function to clear the Keys to text remapping table
    void ClearSingleKeyToTextRemaps();

    // Function to clear text expansion rules.
    void ClearTextExpansions();

    // Function to clear the "Alone" single key remapping table (dual-key: tap-alone action)
    void ClearSingleKeyAloneRemaps();

    // Function to clear the App specific shortcut remapping table
    void ClearAppSpecificShortcuts();

    // Function to add a new single key to key remapping
    bool AddSingleKeyRemap(const DWORD& originalKey, const KeyShortcutTextUnion& newRemapKey);

    // Function to add a new single key to unicode string remapping
    bool AddSingleKeyToTextRemap(const DWORD originalKey, const std::wstring& text);

    // Text expansion CRUD uses the stable GUID as the rule identity.
    bool AddTextExpansion(const TextExpansionRule& rule);
    bool UpdateTextExpansion(std::wstring_view id, const std::wstring& sourceText, const Shortcut& activation, const std::wstring& replacementText, bool enabled);
    bool DeleteTextExpansion(std::wstring_view id);
    bool SetTextExpansionEnabled(std::wstring_view id, bool enabled);

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

    // Stores text expansions in profile order. Duplicate content is allowed; GUIDs are unique.
    TextExpansionTable textExpansions;

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
    SettingsWriter settingsWriter;
    SettingsReloadNotifier settingsReloadNotifier;
    SettingsPathProvider settingsPathProvider;

    bool configurationNameResolved = false;

    bool LoadSingleKeyToTextRemaps(const json::JsonObject& jsonData);
    bool LoadTextExpansions(const json::JsonObject& jsonData);
    bool LoadShortcutRemaps(const json::JsonObject& jsonData, const std::wstring& objectName);
    bool LoadAppSpecificShortcutRemaps(const json::JsonObject& remapShortcutsData);
};
