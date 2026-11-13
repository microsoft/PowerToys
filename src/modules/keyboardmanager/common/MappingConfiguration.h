#pragma once

#include <common/utils/json.h>

#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/common/Shortcut.h>
#include <keyboardmanager/common/RemapShortcut.h>

using SingleKeyRemapTable = std::unordered_map<DWORD, KeyShortcutTextUnion>;
using SingleKeyToTextRemapTable = SingleKeyRemapTable;
// Maps a source key to an ORDERED sequence of key taps to send (down+up each), in order.
// Distinct from a shortcut (simultaneous chord) and from a single-key remap (#5679).
using SingleKeyToSequenceRemapTable = std::unordered_map<DWORD, std::vector<DWORD>>;
using ShortcutRemapTable = std::map<Shortcut, RemapShortcut>;
using AppSpecificShortcutRemapTable = std::map<std::wstring, ShortcutRemapTable>;

class MappingConfiguration
{
public:
    ~MappingConfiguration() = default;

    // Load the configuration.
    bool LoadSettings();

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

    // Function to clear the single-key-to-sequence (ordered key taps) remapping table
    void ClearSingleKeyToSequenceRemaps();

    // Function to clear the App specific shortcut remapping table
    void ClearAppSpecificShortcuts();

    // Function to add a new single key to key remapping
    bool AddSingleKeyRemap(const DWORD& originalKey, const KeyShortcutTextUnion& newRemapKey);

    // Function to add a new single key to unicode string remapping
    bool AddSingleKeyToTextRemap(const DWORD originalKey, const std::wstring& text);

    // Function to add a new "Alone" single key remapping (dual-key / Karabiner to_if_alone):
    // the action applied only when originalKey is tapped alone; in combination the original key passes through.
    bool AddSingleKeyAloneRemap(const DWORD& originalKey, const KeyShortcutTextUnion& aloneRemapKey);

    // Function to add a new single-key-to-sequence remapping: pressing originalKey sends `sequence`
    // as discrete key taps (down+up each), in order. Rejects a duplicate source key or empty sequence.
    bool AddSingleKeyToSequenceRemap(const DWORD& originalKey, const std::vector<DWORD>& sequence);

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

    // Stores single-key-to-sequence remappings (source key -> ordered list of key taps, #5679).
    SingleKeyToSequenceRemapTable singleKeyToSequenceReMap;

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
    // aloneSingleKeyReMap / singleKeyToSequenceReMap, routing by the optional per-entry "condition" field. Public so the
    // dual-key (tap-alone) condition round-trip can be unit tested without touching disk.
    bool LoadSingleKeyRemaps(const json::JsonObject& jsonData);

    // Converts between a key sequence and its settings string: the VK codes in order, ';'-separated
    // (e.g. Esc then IME-Off = "27;26"). Stored in the separate inProcessSequence array, so the
    // same ';' format as a shortcut means "one after another" here, not "at the same time". ParseKeySequence skips
    // empty elements and throws on an element that is not a decimal key code (1-255, or the KBM
    // codes for Disabled and Win); the loader then skips that remap.
    static std::wstring KeySequenceToString(const std::vector<DWORD>& sequence);
    static std::vector<DWORD> ParseKeySequence(const std::wstring& value);

private:
    bool configurationNameResolved = false;

    bool LoadSingleKeyToTextRemaps(const json::JsonObject& jsonData);
    bool LoadShortcutRemaps(const json::JsonObject& jsonData, const std::wstring& objectName);
    bool LoadAppSpecificShortcutRemaps(const json::JsonObject& remapShortcutsData);
};