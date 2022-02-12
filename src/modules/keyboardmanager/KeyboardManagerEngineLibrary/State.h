#pragma once
#include <keyboardmanager/common/MappingConfiguration.h>

struct RemappedKeyEvent
{
    // Stores a key or shortcut to be injected.
    KeyShortcutUnion key{ KeyboardManagerConstants::VK_NULL };

    // Stores true when the key should be pressed (instead of being released).
    bool isKeyDown{};
};

struct SingleKeyRemapResult
{
    // Stores true if the original key should be marked as handled.
    bool shouldEatOriginalKey{};

    // Stores a list of key events to be injected for achieving the remapping.
    std::vector<RemappedKeyEvent> remappedKeyEvents;
};

class State : public MappingConfiguration
{
private:
    // Stores the activated target application in app-specific shortcut
    std::wstring activatedAppSpecificShortcutTarget;

    // Stores the key code of a dual-mapped key being pressed.
    DWORD pendingPressedKeyCode{};

    // Stores the key events to be injected when another key is pressed while the dual-mapped key keeps pressed.
    std::vector<RemappedKeyEvent> pendingCombinationRemappedKeyEvents{};

public:
    // Function to get key events for a single key remap given the source key event.
    SingleKeyRemapResult GetSingleKeyRemapResult(DWORD originalKey, bool isKeyDown);

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