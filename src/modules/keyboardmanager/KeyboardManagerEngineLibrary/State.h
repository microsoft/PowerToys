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

    // Dual-key ("Alone") runtime bookkeeping. Down and key-up arrive as separate hook
    // invocations, so we must remember, across invocations, whether an alone-mapped key that
    // is currently held is still a tap candidate (no other key intervened) or has already
    // started a combination (its original key-down was injected as a real modifier/key).
    // Only accessed from the (serialized) low-level keyboard hook thread.
    std::unordered_set<DWORD> alonePendingKeys;      // held, still a tap candidate
    std::unordered_set<DWORD> aloneCombinationKeys;  // real key-down injected (combination started)

public:
    // Function to get the iterator of a single key remap given the source key. Returns nullopt if it isn't remapped
    std::optional<SingleKeyRemapTable::iterator> GetSingleKeyRemap(const DWORD& originalKey);

    // Function to get a unicode string remap given the source key. Returns nullopt if it isn't remapped
    std::optional<std::wstring> GetSingleKeyToTextRemapEvent(const DWORD originalKey) const;

    // Function to get the iterator of an "Alone" single key remap given the source key. Returns nullopt if it isn't remapped
    std::optional<SingleKeyRemapTable::iterator> GetSingleKeyAloneRemap(const DWORD& originalKey);

    // Dual-key ("Alone") runtime state transitions (see member declarations above).
    // Mark an alone-mapped key as held and still a tap candidate.
    void SetAlonePending(const DWORD key);
    bool IsAlonePending(const DWORD key) const;
    // Promote a pending alone key into a started combination (its real key-down was injected).
    void SetAloneCombination(const DWORD key);
    bool IsAloneCombination(const DWORD key) const;
    // Forget all alone runtime state for a key (on its key-up, once resolved).
    void ClearAloneKeyState(const DWORD key);
    // Snapshot of currently pending (tap-candidate) alone keys, for flushing when another key arrives.
    std::vector<DWORD> GetPendingAloneKeys() const;
    // Cheap check for whether any alone key is currently held as a tap candidate. Used by the mouse
    // hook to early-out before doing any work on the common (no alone key held) path.
    bool HasPendingAloneKeys() const;

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