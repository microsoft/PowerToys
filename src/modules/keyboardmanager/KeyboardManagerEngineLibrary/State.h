#pragma once
#include <keyboardmanager/common/MappingConfiguration.h>

class State : public MappingConfiguration
{
private:
    // Stores the activated target application in app-specific shortcut
    std::wstring activatedAppSpecificShortcutTarget;

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
};