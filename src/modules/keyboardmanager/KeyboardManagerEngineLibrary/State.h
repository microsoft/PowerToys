#pragma once
#include <keyboardmanager/common/MappingConfiguration.h>
#include <keyboardmanager/common/MouseButton.h>

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

    // Function to get a mouse button remap given the source button. Returns nullopt if it isn't remapped
    std::optional<MouseButtonRemapTable::iterator> GetMouseButtonRemap(const MouseButton& originalButton);

    // Function to get an app-specific mouse button remap given the source button and app name. Returns nullopt if it isn't remapped
    std::optional<MouseButtonRemapTable::iterator> GetAppSpecificMouseButtonRemap(const MouseButton& originalButton, const std::wstring& appName);

    // Function to get a mouse button target given the source key. Returns nullopt if it isn't remapped
    std::optional<MouseButton> GetKeyToMouseRemap(const DWORD& originalKey);

    // Function to get an app-specific key-to-mouse remap given the source key and app name. Returns nullopt if it isn't remapped
    std::optional<MouseButton> GetAppSpecificKeyToMouseRemap(const DWORD& originalKey, const std::wstring& appName);

    bool CheckShortcutRemapInvoked(const std::optional<std::wstring>& appName);

    // Function to get the source and target of a shortcut remap given the source shortcut. Returns nullopt if it isn't remapped
    ShortcutRemapTable& GetShortcutRemapTable(const std::optional<std::wstring>& appName);

    std::vector<Shortcut>& GetSortedShortcutRemapVector(const std::optional<std::wstring>& appName);

    // Sets the activated target application in app-specific shortcut
    void SetActivatedApp(const std::wstring& appName);

    // Gets the activated target application in app-specific shortcut
    std::wstring GetActivatedApp();
};