#pragma once
#include <keyboardmanager/common/MappingConfiguration.h>
#include <unordered_set>

class State : public MappingConfiguration
{
private:
    // Stores the activated target application in app-specific shortcut
    std::wstring activatedAppSpecificShortcutTarget;

    // Source keys whose single-key remap key-down injection was blocked, so the original
    // key-down was passed through to the foreground app. The matching key-up must be
    // passed through too; otherwise the physical key is stranded DOWN. Only accessed from
    // the (serialized) low-level keyboard hook thread.
    std::unordered_set<DWORD> singleKeyRemapInjectionFailedKeys;

public:
    // Function to get the iterator of a single key remap given the source key. Returns nullopt if it isn't remapped
    std::optional<SingleKeyRemapTable::iterator> GetSingleKeyRemap(const DWORD& originalKey);

    // Function to get a unicode string remap given the source key. Returns nullopt if it isn't remapped
    std::optional<std::wstring> GetSingleKeyToTextRemapEvent(const DWORD originalKey) const;

    bool CheckShortcutRemapInvoked(const std::optional<std::wstring>& appName);

    // Function to get the source and target of a shortcut remap given the source shortcut. Returns nullopt if it isn't remapped
    ShortcutRemapTable& GetShortcutRemapTable(const std::optional<std::wstring>& appName);

    std::vector<Shortcut>& GetSortedShortcutRemapVector(const std::optional<std::wstring>& appName);

    // Sets the activated target application in app-specific shortcut
    void SetActivatedApp(const std::wstring& appName);

    // Gets the activated target application in app-specific shortcut
    std::wstring GetActivatedApp();

    // Records (failed == true) or clears (failed == false) that the single-key remap
    // key-down injection for sourceKey was blocked and the original key-down was passed
    // through to the foreground app.
    void SetSingleKeyRemapInjectionFailed(const DWORD sourceKey, const bool failed);

    // Returns true and clears the marker if sourceKey's single-key remap key-down
    // injection was previously blocked, indicating that its key-up should be passed
    // through as well.
    bool ConsumeSingleKeyRemapInjectionFailed(const DWORD sourceKey);
};